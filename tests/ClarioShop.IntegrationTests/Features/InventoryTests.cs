using System.Net;
using System.Net.Http.Json;
using ClarioShop.IntegrationTests.Infrastructure;
using ClarioShop.Inventory.Contracts;
using ClarioShop.Inventory.Persistence;
using ClarioShop.Inventory.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;

namespace ClarioShop.IntegrationTests.Features;

[Collection(ApiCollection.Name)]
public sealed partial class InventoryTests(ClarioApiFactory api)
{
    private static CancellationToken Cancel => TestContext.Current.CancellationToken;

    [Fact]
    public async Task NewTenant_HasExactlyOneDefaultLocation()
    {
        var tenant = await api.CreateTenantAsync("loc");
        using var staff = await OwnerClientAsync(tenant);

        var branch = await PostAsync<LocationResponse>(staff, "/api/inventory/locations", new { name = "Dhanmondi branch", code = "dhk-02" });
        var locations = await staff.GetFromJsonAsync<List<LocationItem>>("/api/inventory/locations", Cancel);

        Assert.Equal("DHK-02", branch.Code);
        Assert.False(branch.IsDefault);
        var main = Assert.Single(locations!, l => l.IsDefault);
        Assert.Equal("MAIN", main.Code);
        Assert.Equal(2, locations!.Count);
    }

    [Fact]
    public async Task Database_RejectsSecondDefaultLocation()
    {
        var tenant = await api.CreateTenantAsync("loc-db");
        await using var scope = await api.CreateTenantScopeAsync(tenant.Identifier);
        var db = scope.ServiceProvider.GetRequiredService<InventoryDbContext>();

        var insert = () => db.Database.ExecuteSqlAsync(
            $"""INSERT INTO inventory."Locations" ("Id", "Name", "Code", "IsDefault", "IsActive", "CreatedAt") VALUES ({Guid.CreateVersion7()}, 'Second', 'TWO', TRUE, TRUE, now())""",
            Cancel);

        var error = await Assert.ThrowsAsync<PostgresException>(insert);
        Assert.Equal(PostgresErrorCodes.UniqueViolation, error.SqlState);
    }

    [Fact]
    public async Task ProductCreated_StartsTrackingVariantsWithZeroStock()
    {
        var tenant = await api.CreateTenantAsync("track");
        using var staff = await OwnerClientAsync(tenant);

        var product = await CreateProductAsync(staff, "Pran Mango Juice", "JUICE-MANGO-250", "JUICE-MANGO-1L");

        var stock = await WaitForStockAsync(staff, product.Id, expectedRows: 2);
        Assert.All(stock, row =>
        {
            Assert.Equal("Pran Mango Juice", row.ProductName);
            Assert.Equal("MAIN", row.LocationCode);
            Assert.Equal(0, row.OnHand);
        });
        Assert.Equal(["JUICE-MANGO-1L", "JUICE-MANGO-250"], stock.Select(row => row.Sku).Order());
    }

    [Fact]
    public async Task PurchaseReceipt_AddsStockAndWritesLedgerWithCost()
    {
        var tenant = await api.CreateTenantAsync("recv");
        using var staff = await OwnerClientAsync(tenant);
        var variantId = await CreateVariantAsync(staff, "Teer Sugar 1kg", "SUGAR-TEER-1KG");

        var receipt = await ReceiveAsync(staff, variantId, quantity: 24, unitCost: 118.5m);

        Assert.Equal(2844m, receipt.TotalCost);
        Assert.Equal(24, Assert.Single(receipt.Levels).OnHand);
        var ledger = await staff.GetFromJsonAsync<MovementPage>($"/api/inventory/movements?variantId={variantId}", Cancel);
        var movement = Assert.Single(ledger!.Items);
        Assert.Equal("PurchaseReceipt", movement.Type);
        Assert.Equal(24, movement.Quantity);
        Assert.Equal(118.5m, movement.UnitCost);
        Assert.Equal(receipt.Id, movement.ReferenceId);
        Assert.NotNull(movement.UserId);
    }

    [Fact]
    public async Task PurchaseReceipt_UnknownVariant_ReturnsValidationProblem()
    {
        var tenant = await api.CreateTenantAsync("recv-unk");
        using var staff = await OwnerClientAsync(tenant);

        var response = await staff.PostAsJsonAsync("/api/inventory/receipts", new
        {
            lines = new[] { new { variantId = Guid.CreateVersion7(), quantity = 1, unitCost = 10 } },
        }, Cancel);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Adjustment_DamagedStock_ReducesOnHandWithReasonInLedger()
    {
        var tenant = await api.CreateTenantAsync("adj");
        using var staff = await OwnerClientAsync(tenant);
        var variantId = await CreateVariantAsync(staff, "Eggs (dozen)", "EGG-12");
        await ReceiveAsync(staff, variantId, quantity: 10, unitCost: 140m);

        var adjustment = await PostAsync<AdjustmentResponse>(staff, "/api/inventory/adjustments", new
        {
            reason = "damaged",
            note = "Dropped crate",
            lines = new[] { new { variantId, quantityChange = -3 } },
        });

        Assert.Equal("Damaged", adjustment.Reason);
        Assert.Equal(7, Assert.Single(adjustment.Levels).OnHand);
        var ledger = await staff.GetFromJsonAsync<MovementPage>($"/api/inventory/movements?variantId={variantId}", Cancel);
        var latest = ledger!.Items[0];
        Assert.Equal(("Adjustment", "Damaged", -3m), (latest.Type, latest.Reason, latest.Quantity));
    }

    [Theory]
    [InlineData("""{ "reason": "Damaged", "lines": [ { "variantId": "VARIANT", "quantityChange": 2 } ] }""", HttpStatusCode.BadRequest)]
    [InlineData("""{ "reason": "Other", "lines": [ { "variantId": "VARIANT", "quantityChange": 2 } ] }""", HttpStatusCode.BadRequest)]
    [InlineData("""{ "reason": "Lost", "lines": [ { "variantId": "VARIANT", "quantityChange": -1 } ] }""", HttpStatusCode.BadRequest)]
    [InlineData("""{ "reason": "Theft", "lines": [ { "variantId": "VARIANT", "quantityChange": -50 } ] }""", HttpStatusCode.Conflict)]
    public async Task Adjustment_BreakingReasonOrStockRules_IsRefused(string body, HttpStatusCode expected)
    {
        var tenant = await api.CreateTenantAsync("adj-bad");
        using var staff = await OwnerClientAsync(tenant);
        var variantId = await CreateVariantAsync(staff, "Lux Soap", "SOAP-LUX");
        await ReceiveAsync(staff, variantId, quantity: 5, unitCost: 45m);

        var response = await staff.PostAsync(
            "/api/inventory/adjustments",
            new StringContent(body.Replace("VARIANT", variantId.ToString(), StringComparison.Ordinal), System.Text.Encoding.UTF8, "application/json"),
            Cancel);

        Assert.Equal(expected, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        Assert.Equal(5, (await GetStockAsync(staff, variantId)).OnHand);
    }

    [Fact]
    public async Task Reserve_LastUnit_SecondShopperIsRefused()
    {
        var (tenant, variantId) = await StockedVariantAsync("res-last", quantity: 1);
        await using var scope = await api.CreateTenantScopeAsync(tenant.Identifier);
        var reservations = scope.ServiceProvider.GetRequiredService<IInventoryReservations>();

        var first = await reservations.ReserveAsync(Request(variantId, 1), Cancel);
        var second = await reservations.ReserveAsync(Request(variantId, 1), Cancel);

        Assert.True(first.IsSuccess);
        Assert.InRange(first.Value.ExpiresAt, DateTimeOffset.UtcNow.AddMinutes(14), DateTimeOffset.UtcNow.AddMinutes(16));
        Assert.Equal("inventory.insufficient_stock", second.Error.Code);
    }

    [Fact]
    public async Task Reserve_SameOrderTwice_ReturnsTheSameReservation()
    {
        var (tenant, variantId) = await StockedVariantAsync("res-idem", quantity: 5);
        await using var scope = await api.CreateTenantScopeAsync(tenant.Identifier);
        var reservations = scope.ServiceProvider.GetRequiredService<IInventoryReservations>();
        var request = Request(variantId, 2);

        var first = await reservations.ReserveAsync(request, Cancel);
        var again = await reservations.ReserveAsync(request, Cancel);

        Assert.Equal(first.Value.ReservationId, again.Value.ReservationId);
        Assert.Equal(2, (await LevelAsync(tenant, variantId)).Reserved);
    }

    [Fact]
    public async Task Reserve_MultipleLinesOneShort_ReservesNothing()
    {
        var tenant = await api.CreateTenantAsync("res-atomic");
        using var staff = await OwnerClientAsync(tenant);
        var rice = await CreateVariantAsync(staff, "Miniket Rice", "RICE-MINIKET-5KG");
        var oil = await CreateVariantAsync(staff, "Rupchanda Oil", "OIL-RUP-5L");
        await ReceiveAsync(staff, rice, quantity: 10, unitCost: 300m);
        await ReceiveAsync(staff, oil, quantity: 1, unitCost: 850m);
        await using var scope = await api.CreateTenantScopeAsync(tenant.Identifier);
        var reservations = scope.ServiceProvider.GetRequiredService<IInventoryReservations>();

        var result = await reservations.ReserveAsync(
            new ReserveStockRequest(Guid.CreateVersion7(), [new ReservationLine(rice, 2), new ReservationLine(oil, 2)]),
            Cancel);

        Assert.Equal("inventory.insufficient_stock", result.Error.Code);
        Assert.Equal(0, (await LevelAsync(tenant, rice)).Reserved);
    }

    [Fact]
    public async Task Commit_TurnsReservationIntoOnlineSale()
    {
        var (tenant, variantId) = await StockedVariantAsync("res-commit", quantity: 5);
        await using var scope = await api.CreateTenantScopeAsync(tenant.Identifier);
        var reservations = scope.ServiceProvider.GetRequiredService<IInventoryReservations>();
        var orderId = Guid.CreateVersion7();
        var reservation = await reservations.ReserveAsync(new ReserveStockRequest(orderId, [new ReservationLine(variantId, 2)]), Cancel);

        var committed = await reservations.CommitAsync(reservation.Value.ReservationId, Cancel);
        var committedAgain = await reservations.CommitAsync(reservation.Value.ReservationId, Cancel);

        Assert.True(committed.IsSuccess);
        Assert.True(committedAgain.IsSuccess);
        var level = await LevelAsync(tenant, variantId);
        Assert.Equal((3m, 0m), (level.OnHand, level.Reserved));
        using var staff = await OwnerClientAsync(tenant);
        var ledger = await staff.GetFromJsonAsync<MovementPage>($"/api/inventory/movements?variantId={variantId}", Cancel);
        var sale = Assert.Single(ledger!.Items, m => m.Type == "OnlineSale");
        Assert.Equal((-2m, "Order", orderId), (sale.Quantity, sale.ReferenceType, sale.ReferenceId));
    }

    [Fact]
    public async Task Release_GivesStockBack()
    {
        var (tenant, variantId) = await StockedVariantAsync("res-release", quantity: 1);
        await using var scope = await api.CreateTenantScopeAsync(tenant.Identifier);
        var reservations = scope.ServiceProvider.GetRequiredService<IInventoryReservations>();
        var reservation = await reservations.ReserveAsync(Request(variantId, 1), Cancel);

        var released = await reservations.ReleaseAsync(reservation.Value.ReservationId, Cancel);
        var commitAfterRelease = await reservations.CommitAsync(reservation.Value.ReservationId, Cancel);
        var nextShopper = await reservations.ReserveAsync(Request(variantId, 1), Cancel);

        Assert.True(released.IsSuccess);
        Assert.Equal("inventory.reservation_released", commitAfterRelease.Error.Code);
        Assert.True(nextShopper.IsSuccess);
    }

    [Fact]
    public async Task Expiry_AfterFifteenMinutes_ReleasesHeldStock()
    {
        var (tenant, variantId) = await StockedVariantAsync("res-expire", quantity: 3);
        Guid reservationId;
        await using (var scope = await api.CreateTenantScopeAsync(tenant.Identifier))
        {
            var reservations = scope.ServiceProvider.GetRequiredService<IInventoryReservations>();
            reservationId = (await reservations.ReserveAsync(Request(variantId, 3), Cancel)).Value.ReservationId;
        }

        await using (var scope = await api.CreateTenantScopeAsync(tenant.Identifier))
        {
            var expiry = scope.ServiceProvider.GetRequiredService<ReservationExpiry>();
            Assert.Equal(0, await expiry.ExpireDueAsync(DateTimeOffset.UtcNow.AddMinutes(14), variantIds: null, Cancel));
            Assert.Equal(1, await expiry.ExpireDueAsync(DateTimeOffset.UtcNow.AddMinutes(16), variantIds: null, Cancel));
        }

        var level = await LevelAsync(tenant, variantId);
        Assert.Equal((3m, 0m), (level.OnHand, level.Reserved));
        await using (var scope = await api.CreateTenantScopeAsync(tenant.Identifier))
        {
            var reservations = scope.ServiceProvider.GetRequiredService<IInventoryReservations>();
            Assert.Equal("inventory.reservation_expired", (await reservations.CommitAsync(reservationId, Cancel)).Error.Code);
        }
    }

    [Fact]
    public async Task Reserve_ManyConcurrentShoppers_NeverOversells()
    {
        const int Stock = 5;
        const int Shoppers = 12;
        var (tenant, variantId) = await StockedVariantAsync("res-race", quantity: Stock);

        var attempts = Enumerable.Range(0, Shoppers).Select(async _ =>
        {
            await using var scope = await api.CreateTenantScopeAsync(tenant.Identifier);
            var reservations = scope.ServiceProvider.GetRequiredService<IInventoryReservations>();
            return await reservations.ReserveAsync(Request(variantId, 1), Cancel);
        });
        var results = await Task.WhenAll(attempts);

        Assert.Equal(Stock, results.Count(r => r.IsSuccess));
        Assert.All(results.Where(r => r.IsFailure), r => Assert.Equal("inventory.insufficient_stock", r.Error.Code));
        var level = await LevelAsync(tenant, variantId);
        Assert.Equal((5m, 5m), (level.OnHand, level.Reserved));
    }

    [Fact]
    public async Task Ledger_RefusesUpdatesAndDeletes()
    {
        var (tenant, variantId) = await StockedVariantAsync("ledger", quantity: 2);
        await using var scope = await api.CreateTenantScopeAsync(tenant.Identifier);
        var db = scope.ServiceProvider.GetRequiredService<InventoryDbContext>();

        var update = await Assert.ThrowsAsync<PostgresException>(() =>
            db.Database.ExecuteSqlAsync($"""UPDATE inventory."StockMovements" SET "Quantity" = 100 WHERE "VariantId" = {variantId}""", Cancel));
        var delete = await Assert.ThrowsAsync<PostgresException>(() =>
            db.Database.ExecuteSqlAsync($"""DELETE FROM inventory."StockMovements" WHERE "VariantId" = {variantId}""", Cancel));

        Assert.Equal(PostgresErrorCodes.RestrictViolation, update.SqlState);
        Assert.Equal(PostgresErrorCodes.RestrictViolation, delete.SqlState);
    }

    [Fact]
    public async Task InventoryClerk_CanReceiveButCatalogManagerCannot()
    {
        var tenant = await api.CreateTenantAsync("inv-perm");
        using var owner = await OwnerClientAsync(tenant);
        var variantId = await CreateVariantAsync(owner, "Ispahani Tea", "TEA-ISP-400G");
        await CreateUserAsync(owner, "clerk@shop.test", "Inventory Clerk");
        await CreateUserAsync(owner, "catalog@shop.test", "Catalog Manager");
        using var clerk = api.CreateTenantClient(tenant.Identifier, await api.SignInAsync(tenant.Identifier, "clerk@shop.test", "Staff-pass-123"));
        using var catalog = api.CreateTenantClient(tenant.Identifier, await api.SignInAsync(tenant.Identifier, "catalog@shop.test", "Staff-pass-123"));
        var body = new { lines = new[] { new { variantId, quantity = 1, unitCost = 250 } } };

        var byClerk = await clerk.PostAsJsonAsync("/api/inventory/receipts", body, Cancel);
        var byCatalog = await catalog.PostAsJsonAsync("/api/inventory/receipts", body, Cancel);

        Assert.Equal(HttpStatusCode.Created, byClerk.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, byCatalog.StatusCode);
    }

    [Fact]
    public async Task Stock_WithoutToken_ReturnsUnauthorized()
    {
        var tenant = await api.CreateTenantAsync("inv-anon");
        using var shopper = api.CreateTenantClient(tenant.Identifier);

        var response = await shopper.GetAsync("/api/inventory/stock", Cancel);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    private static ReserveStockRequest Request(Guid variantId, decimal quantity) =>
        new(Guid.CreateVersion7(), [new ReservationLine(variantId, quantity)]);

    private async Task<(TestTenant Tenant, Guid VariantId)> StockedVariantAsync(string prefix, decimal quantity)
    {
        var tenant = await api.CreateTenantAsync(prefix);
        using var staff = await OwnerClientAsync(tenant);
        var variantId = await CreateVariantAsync(staff, $"Item {prefix}", $"SKU-{prefix.ToUpperInvariant()}");
        await ReceiveAsync(staff, variantId, quantity, unitCost: 10m);
        return (tenant, variantId);
    }

    private async Task<StockRow> LevelAsync(TestTenant tenant, Guid variantId)
    {
        using var staff = await OwnerClientAsync(tenant);
        return await GetStockAsync(staff, variantId);
    }

    private static async Task<StockRow> GetStockAsync(HttpClient staff, Guid variantId)
    {
        var page = await staff.GetFromJsonAsync<StockPage>($"/api/inventory/stock?variantId={variantId}", Cancel);
        return Assert.Single(page!.Items);
    }

    private static async Task<IReadOnlyList<StockRow>> WaitForStockAsync(HttpClient staff, Guid productId, int expectedRows)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(Cancel);
        timeout.CancelAfter(TimeSpan.FromSeconds(15));
        while (true)
        {
            var page = await staff.GetFromJsonAsync<StockPage>("/api/inventory/stock", timeout.Token);
            var rows = page!.Items.Where(row => row.ProductName == "Pran Mango Juice").ToList();
            if (rows.Count == expectedRows)
            {
                return rows;
            }

            await Task.Delay(TimeSpan.FromMilliseconds(100), timeout.Token);
        }
    }

    private static async Task<ReceiptResponse> ReceiveAsync(HttpClient staff, Guid variantId, decimal quantity, decimal unitCost) =>
        await PostAsync<ReceiptResponse>(staff, "/api/inventory/receipts", new
        {
            supplierName = "City Group",
            supplierReference = "CH-1001",
            lines = new[] { new { variantId, quantity, unitCost } },
        });

    private static async Task<Guid> CreateVariantAsync(HttpClient staff, string name, string sku) =>
        (await CreateProductAsync(staff, name, sku)).VariantIds[0];

    private static async Task<CreatedProduct> CreateProductAsync(HttpClient staff, string name, params string[] skus)
    {
        var product = await PostAsync<ProductResponse>(staff, "/api/catalog/products", new
        {
            name,
            variants = skus.Select(sku => new { sku, name = sku, price = 100m }).ToArray(),
        });

        // Read variant ids back through Catalog's public page (publish first so it is visible).
        (await staff.PostAsync($"/api/catalog/products/{product.Id}/publish", null, Cancel)).EnsureSuccessStatusCode();
        var page = await staff.GetFromJsonAsync<ProductPage>($"/api/catalog/products/{product.Slug}", Cancel);
        return new CreatedProduct(product.Id, page!.Variants.Select(v => v.Id).ToList());
    }

    private static async Task CreateUserAsync(HttpClient owner, string email, string role) =>
        (await owner.PostAsJsonAsync("/api/identity/users", new { email, fullName = role, password = "Staff-pass-123", roles = new[] { role } }, Cancel))
            .EnsureSuccessStatusCode();

    private async Task<HttpClient> OwnerClientAsync(TestTenant tenant) =>
        api.CreateTenantClient(tenant.Identifier, await api.SignInAsync(tenant.Identifier, tenant.OwnerEmail, tenant.OwnerPassword));

    private static async Task<T> PostAsync<T>(HttpClient client, string url, object body)
    {
        var response = await client.PostAsJsonAsync(url, body, Cancel);
        await response.EnsureSuccessWithBodyAsync();
        return (await response.Content.ReadFromJsonAsync<T>(Cancel))!;
    }

    private sealed record CreatedProduct(Guid Id, IReadOnlyList<Guid> VariantIds);

    private sealed record ProductResponse(Guid Id, string Slug);

    private sealed record ProductPage(IReadOnlyList<VariantItem> Variants);

    private sealed record VariantItem(Guid Id, string Sku);

    private sealed record LocationResponse(Guid Id, string Name, string Code, bool IsDefault);

    private sealed record LocationItem(Guid Id, string Name, string Code, bool IsDefault);

    private sealed record StockRow(Guid VariantId, string Sku, string ProductName, string LocationCode, decimal OnHand, decimal Reserved, decimal Available);

    private sealed record StockPage(IReadOnlyList<StockRow> Items, int TotalCount);

    private sealed record LevelItem(Guid VariantId, decimal OnHand, decimal Reserved, decimal Available);

    private sealed record ReceiptResponse(Guid Id, Guid LocationId, decimal TotalCost, IReadOnlyList<LevelItem> Levels);

    private sealed record AdjustmentResponse(Guid Id, string Reason, IReadOnlyList<LevelItem> Levels);

    private sealed record MovementItem(Guid Id, decimal Quantity, string Type, string? Reason, string ReferenceType, Guid ReferenceId, Guid? UserId, decimal? UnitCost);

    private sealed record MovementPage(IReadOnlyList<MovementItem> Items, int TotalCount);
}
