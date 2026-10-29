# Clario Shop

White-label retail platform for Bangladeshi businesses: an online store and a supershop POS sharing one central inventory, sold to many clients (tenants), each with its own branding, domain and database.

- **Architecture doc:** https://claude.ai/artifact/V7d9eMFQFez4F9RtaTCfY6 (snapshot: [docs/architecture.md](docs/architecture.md))
- **Working rules for contributors and Claude:** [CLAUDE.md](CLAUDE.md)

## Status: Phase 0 done, Phase 1 in progress

| Area | State |
| --- | --- |
| Modular monolith on .NET 10, vertical slices, module boundaries enforced by tests | Done |
| Multi-tenancy: host-name resolution, one PostgreSQL database per tenant, per-tenant migrations | Done |
| Tenant provisioning API (database, schemas, system roles, owner account); plans (Online, POS, Retail Suite) the platform admin sets per shop, whose features gate endpoints with a 403 that says what the plan lacks: online store (carts, checkout, orders, their payments, shopper accounts), POS, more than one location; the storefront settings list the shop's features | Done |
| Identity: OpenIddict token server (staff email + password, shopper phone OTP linked to the customer by phone, 15-minute access tokens, rotating refresh tokens stored per tenant), permission-based RBAC, seeded Owner, Catalog Manager and Inventory Clerk roles; staff roles can be changed (the shop's last active Owner keeps the role), and every staff account created and role change is in Identity's audit log | Done (SSO follows) |
| Catalog: categories, products, variants with barcodes and sold-by-weight, publish, storefront reads; edit and archive products, add, rename or take variants off sale (Inventory tracks added variants), price changes published as `ProductPriceChanged` and kept in Catalog's audit log; brands with a storefront brand filter and a back-office product list; variant attributes (Size, Colour …) whose values each variant picks and the product page offers; product image galleries (JPEG, PNG, WebP or AVIF, checked by content) with a main image, stored through `IFileStorage` (local disk until object storage is configured) | Done (search follows) |
| Inventory: default location per tenant, append-only stock ledger, stock levels with optimistic concurrency, 15-minute checkout reservations, purchase receipts, adjustments with reason codes, a reorder level per variant (or a store-wide default), stock transfers between locations (one document, `TransferOut` and `TransferIn` saved together), a nightly drift check that recomputes stock levels from the ledger and records and corrects any that drifted, a `StockWentNegative` event when stock crosses below zero; an audit log of every adjustment (who, from which device, stock on hand before and after) | Done (POS sales follow) |
| Customers: one customer per Bangladeshi mobile number (any format), concurrency-safe find-or-create for checkout, addresses with one default, staff search; signed-in shoppers manage their own profile and saved addresses | Done (loyalty later) |
| Ordering: anonymous carts priced from Catalog, cash-on-delivery checkout that reserves stock and finds or creates the customer, price snapshots on order lines, per-tenant order numbers, idempotent per cart; delivery zone fees (free over a threshold) and VAT from Settings, stored on the order; staff order lifecycle (confirm, pack, ship, deliver, cancel, return) with Inventory selling, releasing or reversing the stock, and an Order Handler role; signed-in shoppers see their own orders and can check out to a saved address | Done (prepaid checkout follows) |
| Payments: payments and payment attempts per order (one order may have several), cash collected on delivery recorded by staff or riders for shipped or delivered orders, never more than is owed even when recorded concurrently; Ordering shows each order's amount paid and payment status (Unpaid, PartiallyPaid, Paid); SSLCommerz gateway client (payment sessions, transaction validation) using each store's own merchant account; prepaid checkout with SSLCommerz: the order waits in PendingPayment, the shopper pays on the gateway's page, and the IPN (stored raw) settles the payment only once the validation API confirms it, after which the order is confirmed and its stock sold; a reconciliation job every 15 minutes looks pending online payments up at the gateway by transaction id, settling missed IPNs the same way and failing payments the gateway has nothing paid for once the shopper stopped trying, which cancels the order and releases its stock | Done (refunds follow) |
| Settings: per-tenant store profile, theme tokens, invoice prefix, VAT rate, currency format, languages and delivery zones; public storefront settings; cached contract for other modules, invalidated on change; SSLCommerz merchant account per store, its password encrypted with the Data Protection API (key ring in the platform database) and never returned; the store's own text for each order SMS and email, placeholders checked on save, reset to the built-in text at any time | Done (SMS credentials follow) |
| Notifications: shoppers get an SMS (and an email when they gave an address) when a cash-on-delivery order is placed and when any order is confirmed, shipped and delivered, in the store's default language, from the store's own templates (edited in Settings) or the built-in Bangla and English ones; every message is logged once per event, so a redelivered event never sends twice; failed sends are retried with growing delays (up to 5 tries) by a job that never sends one try twice; stock alerts to the store: when stock goes below zero, and once per drift check that corrected stock levels, the store's contact phone (when it is a mobile number) and contact email from Settings get an SMS and an email in the store's language; staff see the log by order, kind or status; SMS and email go through logging senders until a gateway is chosen | Done |
| Reporting: its own copy of every order, built from Ordering's events (applied once, in order of when they happened, even when redelivered, late or handled at once); sales per store day in Bangladesh time (orders confirmed that day, net of cancellations and returns), orders placed in a period by current status, and the best-selling variants by revenue or quantity; inventory reports asked of Inventory through its contract: stock value at weighted average receipt cost per location and variant, stock at or below its reorder level, and stock below zero | Done |
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
  "ownerPassword": "a-strong-password-1",
  "plan": "Online"
}
```

This creates the `clarioshop_rahman_store` database with every module's schema, the system roles and the owner account. Requests to `shop.rahmanstore.com.bd` are then served from that database. `plan` is `Online`, `Pos` or `RetailSuite` (the default); change it later with `PUT /api/platform/tenants/rahman-store/plan` and `{ "plan": "RetailSuite" }`.
