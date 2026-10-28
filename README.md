# Clario Shop

White-label retail platform for Bangladeshi businesses: an online store and a supershop POS sharing one central inventory, sold to many clients (tenants), each with its own branding, domain and database.

- **Architecture doc:** https://claude.ai/artifact/V7d9eMFQFez4F9RtaTCfY6 (snapshot: [docs/architecture.md](docs/architecture.md))
- **Working rules for contributors and Claude:** [CLAUDE.md](CLAUDE.md)

## Status: Phase 0 done, Phase 1 in progress

| Area | State |
| --- | --- |
| Modular monolith on .NET 10, vertical slices, module boundaries enforced by tests | Done |
| Multi-tenancy: host-name resolution, one PostgreSQL database per tenant, per-tenant migrations | Done |
| Tenant provisioning API (database, schemas, system roles, owner account) | Done |
| Identity: OpenIddict token server (staff email + password, shopper phone OTP linked to the customer by phone, 15-minute access tokens, rotating refresh tokens stored per tenant), permission-based RBAC, seeded Owner, Catalog Manager and Inventory Clerk roles | Done (SSO follows) |
| Catalog: categories, products, variants with barcodes and sold-by-weight, publish, storefront reads | Done |
| Inventory: default location per tenant, append-only stock ledger, stock levels with optimistic concurrency, 15-minute checkout reservations, purchase receipts, adjustments with reason codes | Done (transfers, POS sales and the nightly drift check follow) |
| Customers: one customer per Bangladeshi mobile number (any format), concurrency-safe find-or-create for checkout, addresses with one default, staff search | Done (loyalty later) |
| Ordering: anonymous carts priced from Catalog, cash-on-delivery checkout that reserves stock and finds or creates the customer, price snapshots on order lines, per-tenant order numbers, idempotent per cart; delivery zone fees (free over a threshold) and VAT from Settings, stored on the order; staff order lifecycle (confirm, pack, ship, deliver, cancel, return) with Inventory selling, releasing or reversing the stock, and an Order Handler role | Done (prepaid checkout follows) |
| Settings: per-tenant store profile, theme tokens, invoice prefix, VAT rate, currency format, languages and delivery zones; public storefront settings; cached contract for other modules, invalidated on change | Done (payment and SMS credentials, notification templates follow) |
| Transactional outbox and inbox, RabbitMQ transport (in-process fallback) | Done |
| Aspire AppHost: PostgreSQL, Redis, RabbitMQ, generated secrets | Done |
| CI: build (warnings as errors), format check, unit, architecture and integration tests | Done |

## Run it locally

Requirements: .NET 10 SDK and Docker.

```bash
dotnet run --project src/Host/ClarioShop.AppHost
```

The Aspire dashboard opens with PostgreSQL, Redis, RabbitMQ and the API. In Development the API:

- migrates the platform database and every tenant database,
- seeds a **demo shop** at `localhost` and `demo.localhost` with owner `owner@demo.local`. The password is the `demo-owner-password` parameter in the dashboard's **Parameters** tab,
- serves the API reference at `/scalar`.

`src/Host/ClarioShop.Api/ClarioShop.Api.http` walks through signing in and creating, publishing and browsing a product.

### Without Docker

Point the API at any PostgreSQL server:

```bash
export ConnectionStrings__postgres="Host=localhost;Username=postgres;Password=postgres"
export ConnectionStrings__platformdb="Host=localhost;Username=postgres;Password=postgres;Database=clarioshop_platform"
export Platform__DemoTenant__OwnerPassword="choose-a-password-1"
dotnet run --project src/Host/ClarioShop.Api
```

Without RabbitMQ the outbox delivers events in-process; without Redis, caching is in-memory only.

## Tests

```bash
dotnet test --solution ClarioShop.slnx
```

Integration tests start PostgreSQL with Testcontainers. To use an existing server instead:

```bash
CLARIOSHOP_TEST_POSTGRES="Host=localhost;Username=postgres;Password=postgres" dotnet test --solution ClarioShop.slnx
```

## Creating a client shop

```http
POST /api/platform/tenants
X-Platform-Key: <platform-api-key parameter>

{
  "identifier": "rahman-store",
  "name": "Rahman Super Store",
  "hosts": ["shop.rahmanstore.com.bd"],
  "ownerEmail": "owner@rahmanstore.com.bd",
  "ownerName": "Abdur Rahman",
  "ownerPassword": "a-strong-password-1"
}
```

This creates the `clarioshop_rahman_store` database with every module's schema, the system roles and the owner account. Requests to `shop.rahmanstore.com.bd` are then served from that database.
