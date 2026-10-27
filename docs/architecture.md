<!--
Snapshot of the Clario Shop architecture doc (Claude Docs, revision 23), exported for sessions that
cannot reach Claude Docs. The live document is the source of truth:
https://claude.ai/artifact/V7d9eMFQFez4F9RtaTCfY6
If this copy and the live doc disagree, the live doc wins; refresh this file when the doc changes.
-->

# Clario Shop — Architecture

2026-10-27 · Fahad

## Overview

Clario Shop is a white-label retail platform, sold to many single-business clients in Bangladesh: an online store and a supershop POS that share one central inventory.

- **Clients (tenants):** one business each, with one or more physical locations. Each client gets its own branding, domain, database and payment credentials.
- **Channels a client can buy:** Online Store (web + Flutter app), POS (desktop counter app), or both on one inventory.
- **People:** shoppers (online), counter staff, branch managers, the business owner and back-office staff, and the platform operator (you).
- **Not in scope:** third-party sellers, commissions or payouts. This is not a marketplace.

**MVP scope:** catalog, single-location inventory (multi-location ready in the data model), online store with COD and SSLCommerz, staff roles, basic reports, per-client branding. POS follows as the second release.

## Key decisions

A modular monolith on .NET 10 with database-per-tenant, a Next.js web front end, a Flutter mobile app and a .NET desktop POS.

| Area | Decision | Why |
| --- | --- | --- |
| Architecture | Modular monolith, modules split by business capability | One deployable for a solo developer; module boundaries let any module become a service later |
| Code structure | Vertical slices inside each module, clean architecture layering (Domain, Application, Infrastructure, Api) | Each feature lives in one folder; domain stays free of infrastructure |
| Backend | .NET 10, ASP.NET Core Minimal APIs, EF Core | Your main stack; long-term support release |
| Database | PostgreSQL, one database per tenant, one schema per module | Strong isolation between clients; per-client backup and restore |
| Messaging | RabbitMQ with a transactional outbox | Modules talk through events; no event is lost if the broker is down |
| Cache | Redis | Catalog and session cache, rate limiting for OTP and login |
| Auth | OpenIddict (open source), permission + policy based RBAC | Email, phone OTP and Google SSO under one token server; no per-revenue licence |
| Search | Meilisearch | Typo-tolerant product search for storefront and POS lookup |
| Files | S3-compatible storage (Cloudflare R2 or MinIO) + CDN | Product images resized and served cheaply |
| Jobs | Hangfire | SMS, email, reports, payment reconciliation off the request path |
| Web | Next.js + Tailwind + shadcn/ui | Server rendering for SEO; runtime theming per tenant |
| Mobile | Flutter, build flavours per client | One codebase, a separate store listing per client |
| POS | .NET desktop app with local SQLite, offline first | Works through internet and power cuts; direct printer, drawer and scale control |
| Local dev | .NET Aspire | Starts Postgres, Redis, RabbitMQ and the API with one command |

MediatR and MassTransit moved to commercial licences in 2025. The plan uses plain handler interfaces and the RabbitMQ client (or Wolverine) to avoid that dependency.

## Module boundaries

Twelve modules run in one API process; each owns its tables and exposes only a Contracts project, so any module can later become its own service.

**System at a glance** (diagram in the original: every channel calls one API, and one Inventory module owns all stock)

| Layer | Parts |
| --- | --- |
| Apps | Storefront (Next.js, SSR) · Back office (Next.js admin) · Mobile app (Flutter, per client) — REST + JWT · POS till (Avalonia + SQLite) — batch sync |
| `ClarioShop.Api`: modular monolith on .NET 10 (modules talk via Contracts + events) | Catalog (products, variants) · **Inventory (one stock ledger)** · Ordering (orders, returns) · Payments (gateways, refunds) · Customers (profiles, loyalty) · POS (tills, shifts, sync) · Reviews (ratings, Q and A) · Identity (OpenIddict, RBAC) · Notifications (SMS, email, push) · Reporting (sales, stock reports) · Settings (branding, config) · Platform (tenants, plans) |
| Backing services | PostgreSQL (DB per tenant) · Redis (cache, limits) · RabbitMQ (outbox events) · Meilisearch (search) · R2 storage (images, CDN) · External (payment, SMS) |

All four apps call the same API; Inventory (highlighted) is the single owner of stock for both online orders and POS sales.

**How modules talk**

- **Synchronous** (in-process call through a Contracts interface) only when the caller needs an answer now: checkout asking Inventory to reserve stock, Ordering reading prices from Catalog.
- **Asynchronous** (integration event via outbox and RabbitMQ) for everything that reacts to a change: `PaymentSucceeded`, `OrderConfirmed`, `PosSaleRecorded`, `ProductPriceChanged`.
- **Never:** one module reading another module's tables, or sharing a DbContext.

## Multi-tenancy and white-label

Each client is a tenant with its own PostgreSQL database; a small shared Platform database holds the tenant registry, plans and your billing.

**Tenant resolution** (Finbuckle.MultiTenant):

1. Web and mobile: by host name (`shop.clientname.com.bd` or `clientname.yourplatform.com`).
2. POS: by a device registration token issued when the till is set up.
3. Admin API calls: by the `tenant` claim in the access token, checked against the host.

**Per-tenant database.** A connection string per tenant, stored encrypted in the Platform database. EF Core migrations run per tenant on deploy through a migration runner. A large client can move to its own server by changing only its connection string.

**Per-tenant configuration** (Settings module, cached in Redis):

- Branding: name, logo, favicon, colour tokens, fonts, email templates
- Payment credentials: SSLCommerz and bKash merchant keys, so money goes straight to the client
- SMS sender ID and gateway keys, email sender
- Delivery zones and fees, VAT rate, invoice prefix, currency and number formatting, languages (Bangla, English)

**Feature modules per plan.** Each plan turns modules on or off with a feature flag checked at the API and in the UI:

| Plan | Includes |
| --- | --- |
| Online | Storefront, mobile app, orders, single location, reports |
| POS | POS, back-office inventory, purchases, single or multiple locations |
| Retail Suite | Online + POS on one inventory, multiple locations, loyalty |

**Platform admin (you):** create a tenant (provisions its database, seeds roles and an owner account), set its plan, suspend or extend its licence, and see usage across tenants.

## Core data model

The sellable unit everywhere is the **variant** (one SKU, one barcode); orders, POS sales and stock all point at a variant ID, never at a product.

| Module (schema) | Main entities | Notes |
| --- | --- | --- |
| Catalog | Category (tree), Brand, Product, Variant, VariantBarcode, ProductImage, Attribute, AttributeValue, PriceList, PriceListItem | Product = shared content; Variant = size/colour/weight with its own SKU and price. Several barcodes per variant (manufacturer + in-house). Weighed items flagged `SoldByWeight` with a unit (kg). |
| Inventory | Location, StockMovement, StockLevel, StockReservation, Supplier, PurchaseOrder, GoodsReceipt, StockAdjustment, StockTransfer | Location = warehouse or shop. StockLevel is a projection of movements (see Inventory design). |
| Customers | Customer, Address, LoyaltyAccount, LoyaltyTransaction | One customer record shared by online and POS, matched by phone number. |
| Ordering | Cart, CartItem, Order, OrderLine, OrderStatusHistory, Shipment, ReturnRequest, Coupon, CouponRedemption | Order lines copy name, SKU and price at purchase time, so later catalog changes never alter history. |
| Payments | Payment, PaymentAttempt, Refund, GatewayWebhookLog | One order can have several payments (part COD, part bKash). Webhooks stored raw for audit. |
| POS | Register (till), Device, Shift, CashMovement, Sale, SaleLine, SalePayment, HeldBill | Sale ID is a GUID created on the till, so offline sales never clash. |
| Reviews | Review, ReviewImage, ProductQuestion, Answer | Verified purchase flag set from Ordering. |
| Identity | User, Role, Permission, RolePermission, UserLocation, ExternalLogin, OtpCode | UserLocation scopes branch staff to their locations. |
| Settings | TenantSetting, Theme, DeliveryZone, TaxRate, NotificationTemplate | Read-mostly, cached in Redis. |

**Conventions on every table:** `Id` (GUID v7, sortable), `CreatedAt`, `CreatedBy`, `UpdatedAt`, `UpdatedBy`, `RowVersion` for optimistic concurrency, soft delete (`IsDeleted`) on master data only. Money stored as `numeric(18,2)` with BDT as the currency. All timestamps stored in UTC and shown in Asia/Dhaka.

## Inventory design

Stock is an append-only ledger of movements per variant per location; the on-hand number is derived from it, never overwritten.

**StockMovement** (insert only): `VariantId`, `LocationId`, `Quantity` (signed, decimal for weighed items), `Type`, `ReferenceType` + `ReferenceId` (the order, sale, receipt or adjustment that caused it), `OccurredAt`, `RecordedAt`, `UserId`, `UnitCost`.

| Movement type | Sign | Created by |
| --- | --- | --- |
| PurchaseReceipt | + | Goods received from a supplier |
| OnlineSale | − | Order confirmed as paid or COD-accepted |
| PosSale | − | POS sale synced from a till |
| CustomerReturn | + | Return accepted (restockable items only) |
| Adjustment | ± | Damage, expiry, theft, count correction, with a reason code |
| TransferOut / TransferIn | − / + | Stock moved between locations |

**StockLevel** (one row per variant per location): `OnHand`, `Reserved`, `Available = OnHand − Reserved`, `RowVersion`. Updated in the same transaction as each movement. A nightly job recomputes it from the ledger and flags any drift.

**Reservations for online orders.** Checkout creates a StockReservation with a 15-minute expiry and raises `Reserved`. Payment success turns it into an OnlineSale movement; failure or expiry releases it. Two shoppers can never both buy the last unit.

**Concurrency.** The StockLevel update uses optimistic concurrency (`RowVersion`) with a short retry; hot items can switch to `SELECT … FOR UPDATE` on the single row.

**POS sales are never blocked by stock.** The till may sell stock the server thinks is gone (a miscount or a delayed receipt). The movement is still recorded, stock can go negative, and the owner sees a negative-stock alert to investigate. Refusing a sale at a busy counter is worse than a temporary negative number.

**Locations from day one.** Every client gets one default location, hidden in the UI until the multi-location module is on. No migration is needed to add branches later.

## Event flows

Modules change their own data in one transaction, write the event to an outbox table in that same transaction, and a background dispatcher publishes it to RabbitMQ; consumers are idempotent (an inbox table records handled message IDs).

**Online order, prepaid (SSLCommerz or bKash)**

1. Ordering creates the Order (`PendingPayment`) and asks Inventory to reserve stock (in-process call through the Inventory contract, since checkout needs an immediate answer).
2. Payments creates a payment session and redirects the shopper to the gateway.
3. The gateway calls the IPN webhook. Payments validates it with the gateway's validation API, never trusting the redirect alone, and records `PaymentSucceeded`.
4. Ordering consumes `PaymentSucceeded`, sets the order to `Confirmed`, publishes `OrderConfirmed`.
5. Inventory consumes `OrderConfirmed`, turns the reservation into an OnlineSale movement.
6. Notification consumes `OrderConfirmed`, sends SMS and email.
7. A reconciliation job checks gateway transactions every 15 minutes, catching missed webhooks.

**Online order, cash on delivery**

1. Ordering creates the Order as `Placed`, Inventory reserves stock.
2. Staff confirm by phone or automatically after rules (order value, repeat customer); the order moves to `Confirmed` and the OnlineSale movement is written.
3. On delivery the courier or rider records cash collected; Payments records a COD payment.

**POS sale**

1. The till records the Sale locally in SQLite with its own GUID and a local sequence number.
2. The sync agent sends unsynced sales to `POST /pos/sync/sales` in batches, whenever online.
3. The POS module stores each sale once (the GUID makes repeats harmless), then publishes `PosSaleRecorded`.
4. Inventory writes PosSale movements; Customers adds loyalty points; Reporting updates daily totals.

**Returns**

- Online: ReturnRequest approved → `ReturnAccepted` → Inventory writes CustomerReturn (if restockable) → Payments issues the refund.
- POS: return scanned against the original receipt on the till → synced like a sale with negative lines → same downstream handling.

**Order states:** prepaid orders start at PendingPayment, COD orders at Placed; both then run Confirmed → Packed → Shipped → Delivered, with Cancelled and Returned as exits. Every change is written to OrderStatusHistory.

## POS architecture

The POS is an offline-first .NET desktop app: it sells from a local SQLite copy of the catalog and syncs with the server whenever the connection is up.

**UI framework.** Avalonia UI (recommended): modern styling, MVVM, runs on Windows and Linux tills. WPF is the fallback if a hardware vendor ships only Windows drivers with WPF samples.

**Local store (SQLite via EF Core):** catalog snapshot (variants, barcodes, prices, tax), cashiers and PINs (hashed), open shift, sales and held bills, and an outbox of unsynced records.

**Sync protocol**

- **Down (server → till):** `GET /pos/sync/catalog?since=<cursor>` returns changed variants, prices, barcodes and users since the last cursor. Pulled every 2 minutes and on demand; first run downloads the full snapshot.
- **Up (till → server):** sales, returns, shift opens and closes, cash movements, each with a client GUID. Sent in batches; the server answers with the IDs it accepted. Accepted records are marked synced locally.
- **Conflicts:** prices and catalog are server-wins; sales are append-only, so they never conflict. Stock is not checked on the till.
- **Status bar:** shows online/offline and the count of unsynced sales, so the owner knows when a till has been offline too long.

**Hardware**

| Device | Integration |
| --- | --- |
| Barcode scanner (USB/Bluetooth) | Keyboard wedge: a focused input captures scans, Enter ends a scan |
| Thermal receipt printer (80 mm) | ESC/POS commands over USB or network (ESCPOS_NET library) |
| Cash drawer | Opened by a pulse through the receipt printer |
| Weighing-scale labels | EAN-13 starting with a configurable prefix (commonly 20–29): item code + weight or price decoded on scan |
| Customer display | Optional second window on a second screen |

**Device registration.** An admin generates a one-time code in the back office; entering it on the till binds the device to a tenant, location and register and issues a long-lived device token. Cashiers then sign in with a PIN.

**Updates.** Velopack for silent auto-update from your server, with a version check at sync so very old tills are told to update.

## Identity and RBAC

One OpenIddict token server per deployment issues tenant-scoped tokens; authorization is permission-based, enforced by ASP.NET Core policies.

**Sign-in methods**

| Who | Methods | Token |
| --- | --- | --- |
| Shoppers (web, app) | Phone + OTP (primary in Bangladesh), email + password, Google SSO, Facebook SSO | Access 15 min + rotating refresh token |
| Staff (back office) | Email + password, optional TOTP two-factor | Access 15 min + refresh, shorter idle timeout |
| Cashiers (POS) | PIN on a registered device | Device token + cashier session, works offline |
| Platform admin (you) | Email + password + mandatory two-factor | Separate audience, never valid on tenant APIs |

OTP: 6 digits, 3-minute expiry, 5 attempts, rate limited per phone and per IP in Redis.

**Permissions.** Fine-grained strings grouped by module, for example `catalog.products.edit`, `inventory.adjust`, `orders.refund`, `pos.discount.override`, `reports.sales.view`. Roles are named bundles of permissions; each tenant gets seeded roles (Owner, Manager, Inventory Clerk, Order Handler, Cashier, Supervisor) and can create its own.

**Policies.** Each endpoint declares one permission, e.g. `.RequirePermission("orders.refund")`. A policy provider builds policies from the permission name; a handler checks the user's permissions (cached in Redis, invalidated on role change). Resource rules sit on top as extra requirements:

- **Location scope:** a manager with `UserLocation` rows sees only those locations' stock, orders and reports.
- **Limits:** a cashier's discount above a tenant-set percentage needs a supervisor PIN (`pos.discount.override`).
- **Plan gate:** an endpoint in a module the tenant's plan does not include returns 403 with a clear reason.

**Audit.** Sensitive actions (refunds, adjustments, price changes, role changes, voids) write an AuditLog row: who, what, before and after values, when, from which device.

## Frontend and mobile

Two Next.js apps share one design system; the Flutter app calls the same public API as the storefront.

| App | Users | Stack | Rendering |
| --- | --- | --- | --- |
| Storefront | Shoppers | Next.js (App Router), TypeScript, Tailwind, shadcn/ui | Server-rendered product and category pages for SEO; client islands for cart and checkout |
| Back office | Owner and staff | Next.js, TanStack Query, TanStack Table, react-hook-form + zod | Client-side app behind login |
| Mobile app | Shoppers | Flutter, Riverpod, Dio, go_router | Native, per-client build flavour |
| POS | Cashiers | Avalonia (.NET) | Native desktop |

**Shared design system.** One `packages/ui` library: shadcn/ui components themed through CSS variables (colour, radius, font). The same token names are exported as JSON for Flutter, so web and mobile look like one brand.

**White-label theming at runtime.** The storefront resolves the tenant from the host, loads its theme tokens (cached at the edge), and injects them as CSS variables in the root layout. One build serves every client; no rebuild for a new client.

**Design standard to aim for.**

- Mobile-first: most shoppers in Bangladesh browse on phones over mobile data, so images are AVIF/WebP via the CDN, pages stay light, and the checkout fits one hand.
- Bangla and English with next-intl; Bangla fonts (e.g. Hind Siliguri) loaded only when Bangla is chosen.
- Accessibility to WCAG 2.2 AA: contrast, keyboard paths, focus states, labelled forms.
- Core Web Vitals budget on product pages: LCP under 2.5 s on a mid-range Android over 4G.
- Motion kept subtle (Framer Motion), skeleton loaders instead of spinners.

**Flutter flavours.** Each client gets a flavour: app name, bundle ID, icon, splash, theme tokens and API host. A CI job builds and signs each flavour for its own Play Store and App Store listing.

## Infrastructure and operations

Start on one or two VPS machines with Docker Compose; move to Kubernetes only when client count or load demands it.

**Hosting (first 10–20 clients).** One app server running the API, storefront and back office in containers behind Caddy or Traefik (automatic TLS per client domain); one database server with PostgreSQL, Redis and RabbitMQ; Cloudflare in front for DNS, CDN and R2 image storage.

**Observability.** OpenTelemetry in the API and POS sync endpoints, sending traces, metrics and logs to Grafana (Tempo, Prometheus, Loki) or Seq for a lighter start. Every log line carries `TenantId`, `UserId` and a correlation ID. Health checks for database, Redis, RabbitMQ and outbox lag; uptime alerts to your phone.

**Backups.** Nightly `pg_dump` per tenant database to object storage, 30 days kept, plus WAL archiving for point-in-time recovery. A monthly restore test into a scratch database.

**Testing**

- Unit tests on domain rules (pricing, discounts, stock maths, order state changes).
- Integration tests per vertical slice with Testcontainers (real PostgreSQL, Redis, RabbitMQ).
- Architecture tests (NetArchTest) that fail the build when one module references another's internals.
- End-to-end tests (Playwright) on the money paths: browse → cart → checkout → pay, and a POS sale → sync → stock change.

**CI/CD (GitHub Actions).** Build and test on every pull request; on merge to main, build images, push to a registry, deploy to staging, run tenant migrations, then promote to production by a manual approval. POS and Flutter builds run in their own workflows.

**Environments.** Local (Aspire), staging (one demo tenant with seeded data, doubles as your sales demo), production.

## Solution structure

One monorepo; each backend module is a set of projects with a public `Contracts` project, and nothing else in a module is visible to other modules.

```
clario-shop/
  src/
    Host/
      ClarioShop.Api/              # composition root: modules, auth, tenancy, OpenAPI + Scalar
      ClarioShop.AppHost/          # .NET Aspire: PostgreSQL, Redis, RabbitMQ, API
      ClarioShop.ServiceDefaults/  # OpenTelemetry, health checks, resilience
    BuildingBlocks/
      ClarioShop.SharedKernel/     # Entity, Result, Error, IIntegrationEvent, ITenantContext
      ClarioShop.Infrastructure/   # tenancy, outbox/inbox, RabbitMQ, endpoint discovery, permissions
    Modules/
      Platform/ClarioShop.Platform/              # tenant registry + provisioning (shared platform DB)
      Identity/ClarioShop.Identity(.Contracts)/  # staff, roles, permissions, tokens
      Catalog/ClarioShop.Catalog(.Contracts)/    # categories, products, variants
      Inventory/  Ordering/  Payments/  Customers/  Pos/  ...   # later phases
  apps/                          # later: storefront, backoffice (Next.js), mobile (Flutter), pos (Avalonia)
  tests/
    ClarioShop.ArchitectureTests/  # module-boundary rules (NetArchTest)
    ClarioShop.Catalog.Tests/      # domain unit tests
    ClarioShop.IntegrationTests/   # real HTTP pipeline on PostgreSQL
  .claude/                       # dotnet-claude-kit: skills, agents, rules, hooks
```

**Inside a module: one folder per feature (vertical slice).**

```
ClarioShop.Catalog/
  Domain/
    Product.cs  Variant.cs  Category.cs  Slug.cs
  Features/
    Products/
      CreateProduct.cs        # Command + Response + Validator + Handler in one static class
      PublishProduct.cs
      GetProductBySlug.cs
      ListProducts.cs
      ProductEndpoints.cs     # IEndpointGroup: routes + RequirePermission / AllowAnonymous
  Persistence/
    CatalogDbContext.cs  Configurations/  Migrations/
  CatalogModule.cs            # AddCatalogModule()
```

**Rules (enforced by architecture tests)**

- A module references only other modules' `Contracts`, never their internals or DbContext.
- Each module has its own DbContext and PostgreSQL schema in the tenant database; no cross-schema joins or foreign keys.
- Cross-module reads go through a Contracts query interface; cross-module writes go through events.
- Handlers are plain classes behind a small `ICommandHandler<TCommand, TResult>` interface, with validation as an endpoint filter.

## MVP roadmap

Build in four phases, each ending at a gate; the online store ships to a paying client before POS work starts, unless the first pilot is a supershop (see Open questions).

**Roadmap** (diagram in the original: a paying online client comes before POS work starts; not to scale)

| Phase | Scope | Gate at the end |
| --- | --- | --- |
| 0 · Foundation | Aspire, CI, staging; tenancy, DB per tenant; identity + RBAC; catalog module; outbox + events | One slice end to end |
| **1 · Online MVP** | Storefront, back office; cart and checkout; COD + SSLCommerz; inventory ledger; orders, basic reports; per-client branding | **First client live** |
| 2 · POS | Offline till; sync, shifts, receipts; scanner, printer; purchases, GRN; barcode labels | POS stable at a pilot shop |
| 3 · Growth | Flutter app; multiple locations; loyalty points; courier integrations; billing to clients | — |

Phase 1 is the sellable MVP. Phase 0's gate is one feature (create product → see it on the storefront) working end to end through every layer, which proves the architecture before breadth is added.

## Open questions

- Domain for Clario Shop (the product name is decided).
- First pilot client: an online-only shop or a supershop? This decides whether POS moves ahead of the storefront.
- NBR requirement: do target supershop clients need an approved EFD/SDC device on POS receipts, and which vendor APIs are available?
- SMS gateway choice (cost per SMS, Bangla support, masking/sender ID approval time).
- Courier integrations for the first release: none, or one of Pathao, Steadfast, RedX?
- Pricing model to clients: monthly per plan, per location, per till, or setup fee + monthly?
- Hosting: local Bangladeshi data centre or a regional cloud (Singapore) for lower cost and better tooling?
