# Clario Shop — Modular Monolith

White-label retail platform: an online store (Next.js web + Flutter app) and a supershop POS (.NET desktop) sharing one central inventory, sold to many single-business clients (tenants) in Bangladesh.

**Source of truth for design:** the architecture doc — https://claude.ai/artifact/V7d9eMFQFez4F9RtaTCfY6 (snapshot in `docs/architecture.md` for sessions without Claude Docs access; the live doc wins if they differ). When this file and a dotnet-claude-kit skill or rule disagree, **this file wins**.

## Tech Stack

- **.NET 10** / C# 14, ASP.NET Core **Minimal APIs**
- **EF Core 10** + **PostgreSQL** (Npgsql) — one DbContext and one schema per module, **one database per tenant**
- **RabbitMQ** via `RabbitMQ.Client` + an in-house **transactional outbox/inbox** (no MassTransit, no MediatR — both are commercially licensed)
- **Redis** via `HybridCache` — tenant registry, catalog and permission caches
- **FluentValidation** for request validation
- **.NET Aspire** for local orchestration and service defaults (OpenTelemetry, health checks, resilience)
- **xUnit v3** + **Testcontainers** for tests, **NetArchTest** for module-boundary tests

## Solution Layout

```
src/
  Host/
    ClarioShop.Api/              # composition root: modules, auth, tenancy, OpenAPI
    ClarioShop.AppHost/          # Aspire orchestration (Postgres, Redis, RabbitMQ, Api)
    ClarioShop.ServiceDefaults/  # OpenTelemetry, health checks, resilience
  BuildingBlocks/
    ClarioShop.SharedKernel/     # Entity, Result, Error, IIntegrationEvent, ITenantContext — no business logic
    ClarioShop.Infrastructure/   # tenancy, outbox/inbox, messaging, endpoint discovery, auth plumbing
  Modules/
    <Module>/
      ClarioShop.<Module>.Contracts/   # PUBLIC: integration events, query interfaces, DTOs
      ClarioShop.<Module>/             # INTERNAL: Domain/, Features/, Persistence/, <Module>Module.cs
tests/
  ClarioShop.ArchitectureTests/
  ClarioShop.<Module>.Tests/
  ClarioShop.IntegrationTests/
```

Modules today: **Platform** (tenant registry), **Identity** (users, roles, permissions, tokens), **Catalog**, **Inventory** (locations, stock ledger, reservations), **Customers** (customers by phone, addresses), **Ordering** (carts, checkout, orders). Planned: Payments, Pos, Reviews, Notifications, Reporting, Settings.

## Module Rules (enforced by ClarioShop.ArchitectureTests)

1. A module references **only other modules' `.Contracts`** projects — never their internals, DbContext or tables.
2. Each module owns a **PostgreSQL schema** (`catalog`, `identity`, …) inside the tenant database. No cross-schema joins or foreign keys.
3. **Synchronous** cross-module calls are allowed **only through a Contracts interface**, and only when the caller needs an immediate answer (e.g. checkout reserving stock, ordering reading prices). This deliberately relaxes the kit's "events only" rule.
4. Anything that **reacts to a change** in another module uses an **integration event**, written to the module's outbox in the same transaction as the change.
5. Event consumers are **idempotent** (inbox table keyed by message id).
6. Shared kernel = primitives and contracts only. No business rules.

## Feature (Vertical Slice) Convention

One file per operation, a static class wrapping request, response, validator and handler:

```csharp
// Modules/Catalog/ClarioShop.Catalog/Features/Products/CreateProduct.cs
public static class CreateProduct
{
    public sealed record Command(string Name, ...);
    public sealed record Response(Guid Id, string Slug);
    internal sealed class Validator : AbstractValidator<Command> { ... }
    internal sealed class Handler(CatalogDbContext db, TimeProvider clock) : ICommandHandler<Command, Response> { ... }
}
```

- Endpoints live in one `*Endpoints.cs` per resource implementing `IEndpointGroup`; `app.MapEndpoints()` discovers them. **Never** map endpoints in `Program.cs`.
- Every endpoint declares auth explicitly: `.RequirePermission(Permissions.Catalog.ProductsEdit)` or `.AllowAnonymous()`.
- Handlers return `Result<T>`; endpoints turn errors into **ProblemDetails** via `result.ToHttpResult()`.
- No repository abstractions over EF Core — use the module DbContext directly.

## Multi-tenancy

- Tenants are resolved per request by **host name** (`X-Tenant` header allowed only in Development) through `TenantResolutionMiddleware` → scoped `ITenantContext`.
- The tenant registry (host → tenant id → connection string) lives in the **Platform** database and is cached.
- Module DbContexts get the **tenant's connection string** from `ITenantContext` — never from static configuration.
- Every request outside `/platform/*`, `/health*` and `/openapi*` **must** resolve a tenant, or it fails with 404.
- Migrations are applied per tenant by `TenantMigrator` (on startup in Development, by a deploy step in production).

## Auth and RBAC

- JWT bearer tokens. Phase 0 issues them from the Identity module (`POST /api/identity/token`); Phase 1 moves to **OpenIddict** (phone OTP, refresh tokens, Google SSO).
- Authorization is **permission-based**: permission strings like `catalog.products.edit`, bundled into roles. `PermissionPolicyProvider` builds a policy per permission; `PermissionAuthorizationHandler` checks the user's permissions.
- Tokens carry a `tenant` claim that must match the resolved tenant.

## Coding Standards

Follow `.claude/rules/*.md` (from dotnet-claude-kit) plus:

- `internal sealed` by default; only module registration and Contracts are `public`.
- `TimeProvider` for time, `Guid.CreateVersion7()` for ids, money as `decimal` (`numeric(18,2)`), all timestamps UTC.
- Propagate `CancellationToken` everywhere.
- No secrets in source. Local dev uses Aspire parameters / user secrets.

## Commands

```bash
dotnet build ClarioShop.slnx
dotnet test ClarioShop.slnx
dotnet run --project src/Host/ClarioShop.AppHost        # full local stack (needs Docker)
dotnet format ClarioShop.slnx --verify-no-changes

# migrations: one per module DbContext
dotnet ef migrations add <Name> \
  --project src/Modules/Catalog/ClarioShop.Catalog \
  --startup-project src/Host/ClarioShop.Api \
  --context CatalogDbContext --output-dir Persistence/Migrations
```

## dotnet-claude-kit

`.claude/skills`, `.claude/agents`, `.claude/rules`, `.claude/hooks` and `.claude/knowledge` are vendored from [dotnet-claude-kit](https://github.com/codewithmukesh/dotnet-claude-kit) (MIT, see `.claude/DOTNET-CLAUDE-KIT-LICENSE`). Useful skills here: `vertical-slice`, `ef-core`, `minimal-api`, `testing`, `error-handling`, `aspire`, `authentication`, `arch-check`, `build-fix`.

Where kit guidance conflicts with the rules above, ignore the kit on these points:
- MassTransit / MediatR / Wolverine samples → use our `ICommandHandler` + outbox + `RabbitMQ.Client`.
- "Modules never call each other" → Contracts interfaces are allowed for synchronous needs (rule 3).
- `architecture-advisor` → the architecture is decided; don't re-litigate it.

Roslyn MCP tools (`cwm-roslyn-navigator`) are configured in `.mcp.json`; install once with `dotnet tool install -g CWM.RoslynNavigator`.

## Workflow

- **Roadmap:** `docs/roadmap.md` is the ordered Phase 1 work queue, one step per PR. The `roadmap-runner` skill (`.claude/skills/roadmap-runner/SKILL.md`) fixes and merges the open step's PR, then starts the next step; a scheduled task runs it.
- Fresh containers: `bash scripts/cloud-setup.sh` (local PostgreSQL, dotnet-ef), then `bash scripts/verify.sh` runs the same checks as CI.

- Plan before any change touching 3+ files; build and test before claiming done.
- Commits: conventional prefixes (`feat:`, `fix:`, `chore:` …), feature branches, PRs into `main`.
