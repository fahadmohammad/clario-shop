# Clario Shop — Phase 1 Roadmap

The ordered work queue for the backend of the sellable MVP (Phase 1 in the
[architecture doc](https://claude.ai/artifact/V7d9eMFQFez4F9RtaTCfY6)). The roadmap runner
(`.claude/skills/roadmap-runner/SKILL.md`) works through it top to bottom, **one step per pull request**:
it fixes and merges the open step's PR first, then starts the first unchecked step.

How the boxes work:

- `- [ ]` not started · `- [~]` in progress (its PR is open) · `- [x]` done (its PR is merged) · `- [!]` blocked (needs a human; see the note under the step)
- A step's own PR flips its box to `[x]` and adds the PR link, so `main` always shows what is done.
- To reorder, add, split or skip steps, edit this file on `main`; the runner always reads the current version.

Ground rules for every step: follow `CLAUDE.md` and the architecture doc; ship tests with the code (unit tests on
domain rules, integration tests through HTTP or the module's Contracts, architecture tests for any new module);
leave `scripts/verify.sh` green. Where a vendor is not chosen yet (SMS gateway, email provider, object storage,
couriers), build against an interface with a logging/fake implementation and a typed options section, so the
choice is configuration later, never a blocker now.

## Steps

### 1. customers
- [x] **Customers module** — #5
  - `Customer` (matched by normalized Bangladeshi phone number, unique), `Address` (multiple per customer, one default).
  - Contracts: `ICustomerDirectory.FindOrCreateByPhoneAsync(phone, name)` and `GetAsync(id)` for checkout; `CustomerCreated` event.
  - Staff endpoints: search/list customers, view one with addresses. Permissions `customers.view`, `customers.edit`.
  - Done when: phone normalization unit-tested (`01712-345678`, `+8801712345678`, `8801712345678` are one customer), find-or-create is idempotent under concurrency, module is in the architecture tests.

### 2. ordering-checkout
- [x] **Ordering module: cart and cash-on-delivery checkout** — #6
  - `Cart`/`CartItem` (anonymous cart id for guests), add/update/remove items; prices always read from `ICatalogQueries` (only sellable variants).
  - `POST /api/ordering/checkout` (COD): finds or creates the customer via `ICustomerDirectory`, creates `Order` in `Placed` with `OrderLine`s that copy name, SKU and price, reserves stock via `IInventoryReservations` (insufficient stock → 409, nothing persisted), writes `OrderStatusHistory`, publishes `OrderPlaced`.
  - Order number per tenant (readable, sequential, e.g. `CS-000123`).
  - Done when: integration tests cover happy path, out-of-stock, unpublished variant, price snapshot unaffected by later price changes, idempotent checkout (same cart twice → one order).

### 3. ordering-lifecycle
- [x] **Order lifecycle and stock hand-off** — #7
  - Staff transitions `Placed → Confirmed → Packed → Shipped → Delivered`, plus `Cancelled` (before Shipped) and `Returned`; every change in `OrderStatusHistory`; invalid transitions are 409.
  - Events `OrderConfirmed`, `OrderCancelled`, `OrderShipped`, `OrderDelivered`. Inventory consumes `OrderConfirmed` (commit reservation → `OnlineSale` movements) and `OrderCancelled` (release).
  - Confirming after the 15-minute reservation expired: Ordering re-reserves through `IInventoryReservations` before confirming; if stock is gone the confirm fails with 409 and a clear message.
  - Permissions `orders.view`, `orders.manage`; seeded role **Order Handler**.
  - Done when: end-to-end test place → confirm → stock leaves the ledger; cancel → stock returns; event consumers idempotent.

### 4. settings
- [x] **Settings module (per-tenant configuration)** — #9
  - Store profile (name, contact phone/email, address), branding/theme tokens (colours, radius, fonts, logo URL), invoice prefix, VAT rate, currency formatting, languages.
  - `DeliveryZone` (name, areas, fee, free-delivery threshold).
  - Contracts `ISettingsQueries` (cached with `HybridCache`, invalidated on change); public anonymous `GET /api/settings/storefront` for theme tokens.
  - Permission `settings.edit`.

### 5. checkout-pricing
- [x] **Delivery fees and VAT on orders** — #10
  - Checkout takes a delivery zone, adds the zone fee (or free over threshold) and VAT from `ISettingsQueries`; order stores subtotal, delivery fee, VAT, total (all `numeric(18,2)`, rounding rules unit-tested).
  - Coupon groundwork is out of scope.

### 6a. token-server
- [x] **OpenIddict token server** — #11
  - Replace the Phase 0 HMAC issuer with OpenIddict (open source) in the Identity module: staff email + password flow keeps working, refresh tokens (rotating), access tokens 15 min.
  - Token and authorization entries live in the tenant's `identity` schema, so a refresh token only works on the shop that issued it.
  - Done when: existing integration tests still pass on the new token server; refresh, rotation (a redeemed refresh token is rejected) and cross-tenant refresh are tested.

### 6b. phone-otp
- [x] **Shopper sign-in with phone OTP** — #12
  - Shopper sign-in with phone + OTP as an OpenIddict grant: 6 digits, 3-minute expiry, 5 attempts, rate limited per phone and per IP (Redis via HybridCache/distributed cache; in-memory fallback in tests). OTP sent through an `ISmsSender` with a logging implementation.
  - Shopper accounts link to the Customers module by phone (via Contracts), never by reading its tables.
  - Done when: OTP flow, expiry, attempt limit and rate limit are tested.

### 7. customer-accounts
- [x] **Shopper self-service** — #13
  - Signed-in shopper: my profile, my addresses (CRUD), my orders (list and detail), checkout using a saved address.
  - Permissions/policies so a shopper only ever sees their own data.

### 8. payments-cod
- [x] **Payments module: COD collection** — #14
  - `Payment`, `PaymentAttempt`; record cash collected on delivery (staff/rider endpoint); publishes `PaymentSucceeded`; Ordering shows payment status.
  - One order may have several payments (part COD, part prepaid) — model it now.
  - Permission `payments.manage`.

### 9a. gateway-credentials
- [x] **SSLCommerz merchant credentials and gateway client** — #15
  - Per-tenant merchant credentials in Settings (store id, store password, sandbox or live), the password encrypted with the Data Protection API (purpose bound to the tenant); staff endpoints never return it. Data Protection keys persisted in the Platform database.
  - Contracts `IPaymentGatewayCredentials` for Payments (not cached, so secrets never reach Redis).
  - Gateway behind `IPaymentGateway` (create a session, validate a transaction) with the SSLCommerz implementation; tests use a stub HTTP handler (no network).

### 9b. prepaid-checkout
- [x] **Prepaid checkout with SSLCommerz** — #16
  - Prepaid orders start in `PendingPayment`; Payments creates a gateway session and returns the redirect URL.
  - IPN webhook endpoint (anonymous, tenant from host): store raw payload in `GatewayWebhookLog`, validate with the gateway validation API (never trust the redirect), then `PaymentSucceeded`. Ordering confirms; Inventory commits.
  - Tests use a fake `IPaymentGateway` (no network).

### 9c. payment-reconciliation
- [x] **Failed payments and reconciliation of missed gateway webhooks** — #17
  - Look a transaction up at the gateway by transaction id; a failed, cancelled or expired payment publishes `PaymentFailed`, Ordering cancels the order and Inventory releases the stock (never on the shopper's redirect or an IPN alone).
  - Background job every 15 minutes: for pending gateway payments, query the gateway by transaction id and settle them exactly as the IPN would.

### 10a. notifications
- [x] **Notifications module** — #18
  - Consumes `OrderPlaced`, `OrderConfirmed`, `OrderShipped`, `OrderDelivered`; sends SMS (`ISmsSender`) and email (`IEmailSender`) from built-in Bangla and English templates in the store's default language, logging implementations by default.
  - `NotificationLog` for audit and retries; idempotent per event.

### 10b. notification-templates
- [x] **Per-tenant notification templates** — #19
  - `NotificationTemplate` in Settings (per event, channel and language; the architecture doc puts it there): staff list, edit and reset to the built-in default (`settings.edit`), placeholders validated on save.
  - Notifications renders a tenant's own template when it has one, the built-in default otherwise, read through `ISettingsQueries` (cached, invalidated on change).

### 11a. sales-reports
- [x] **Sales reports** — #20
  - Reporting module builds its own projections from Ordering's events (never reads other schemas): daily sales totals, orders by status, top variants.
  - Permission `reports.sales.view`.

### 11b. inventory-reports
- [x] **Inventory reports** — #21
  - Inventory reports via an `IInventoryQueries` contract: stock value at cost, low and negative stock.
  - Permission `reports.inventory.view`.

### 12a. stock-alerts
- [x] **Stock drift check and negative-stock event** — #22
  - Nightly drift check: recompute `StockLevel` from the ledger, record and alert on differences.
  - Negative-stock alerts surfaced through an event Notifications can consume.

### 12b. stock-transfers
- [x] **Stock transfers between locations** — #23
  - Stock transfers between locations (`TransferOut`/`TransferIn`, one document).

### 12c. audit-log
- [x] **Audit log** — #24
  - `AuditLog` (who, what, before/after, when) for adjustments, price changes and role changes — a small per-module table, not a shared one.
  - Done here: Inventory audits stock adjustments; Identity audits staff accounts created and role changes (new endpoint to change a user's roles). Catalog has no price change yet, so auditing it moves to step 13.

### 12d. staff-alerts
- [x] **Stock alerts to staff** — #25
  - Notifications consumes `StockWentNegative` and `StockDriftDetected` and tells the store's contact (email and SMS from Settings) in the store's language.
  - Done here: one alert per negative-stock event and one per drift check (not per corrected level); built-in Bangla and English text, not editable per store yet.

### 13a. catalog-editing
- [x] **Edit products and variants, audited price changes** — #26
  - Edit and archive a product; add a variant (Inventory tracks it by consuming `VariantAdded`), rename it, replace its barcodes or take it off sale.
  - Price changes publish `ProductPriceChanged` and write a row to Catalog's own `AuditLog` (the 12c pattern: `AuditTrail` and `MapAuditLog` from Infrastructure), readable behind a `catalog.audit.view` permission.

### 13b. brands
- [x] **Brands and the back-office product list** — #27
  - `Brand` (replacing the free-text brand on products; existing names migrate to brands), staff create and rename; storefront filter by brand.
  - Staff product list with drafts and archived products, for the back office.

### 13c. attributes
- [~] **Variant attributes**
  - Attributes and values (e.g. Size: S, M, L; Colour: Red, Blue), staff-managed; each variant picks at most one value per attribute; the storefront product page shows them so shoppers can choose.

### 13d. product-images
- [ ] **Product images**
  - `ProductImage` (upload via an `IFileStorage` abstraction for S3-compatible storage; local-disk implementation for dev/tests), ordering and a main image.

### 14. plans-and-scopes
- [ ] **Plan gating and location scope**
  - Platform: tenant plan (Online, POS, Retail Suite) with module feature flags; endpoints of a module outside the plan return 403 with a clear reason.
  - Identity `UserLocation`: managers limited to their locations' stock, orders and reports.

### 15. search
- [ ] **Product search with Meilisearch**
  - Per-tenant index fed from Catalog events; typo-tolerant storefront search endpoint with category/brand filters; falls back to the database search when Meilisearch is not configured. AppHost adds Meilisearch.

### 16. phase1-hardening
- [ ] **Phase 1 hardening**
  - OpenAPI descriptions complete for every endpoint; rate limits on public endpoints; health checks for outbox lag; an end-to-end integration test of the full money path (browse → cart → checkout → pay → confirm → stock change → notification).
  - Update README status table for Phase 1.
