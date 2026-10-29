using System.Net;
using System.Net.Http.Json;
using ClarioShop.IntegrationTests.Infrastructure;
using ClarioShop.Inventory.Contracts;
using ClarioShop.Inventory.Domain;
using ClarioShop.Inventory.Persistence;
using ClarioShop.Inventory.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace ClarioShop.IntegrationTests.Features;

/// <summary>
/// Inventory reports: Reporting asks Inventory through <see cref="IInventoryQueries"/> for stock value at weighted
/// average receipt cost, stock at or below its reorder level, and stock below zero.
/// </summary>
public sealed partial class InventoryTests
{
    [Fact]
    public async Task StockValuation_ValuesStockAtWeightedAverageReceiptCost()
    {
        var tenant = await api.CreateTenantAsync("val-avg");
        using var staff = await OwnerClientAsync(tenant);
        var rice = await CreateVariantAsync(staff, "Miniket Rice", "RICE-5KG");
        var tea = await CreateVariantAsync(staff, "Ispahani Tea", "TEA-400G");
        await ReceiveAsync(staff, rice, quantity: 10, unitCost: 100m);
        await ReceiveAsync(staff, rice, quantity: 20, unitCost: 120m);
        await ReceiveAsync(staff, tea, quantity: 3, unitCost: 45.5m);
        await AdjustAsync(staff, rice, "Damaged", -6);

        var report = await staff.GetFromJsonAsync<StockValuation>("/api/reports/inventory/valuation", Cancel);

        // Rice: (10 × 100 + 20 × 120) / 30 = 113.333…, 24 on hand → 2720. Tea: 3 × 45.50 = 136.50.
        Assert.Equal((2856.5m, 0, 2), (report!.TotalValue, report.UncostedItems, report.TotalCount));
        Assert.Equal(
            [("RICE-5KG", 24m, (decimal?)113.33m, (decimal?)2720m), ("TEA-400G", 3m, 45.5m, 136.5m)],
            report.Items.Select(item => (item.Sku, item.OnHand, item.UnitCost, item.Value)));
        var main = Assert.Single(report.Locations);
        Assert.Equal(("MAIN", 2856.5m), (main.LocationCode, main.Value));
    }

    [Fact]
    public async Task StockValuation_StockNeverReceivedWithACost_IsListedLastAsUncosted()
    {
        var tenant = await api.CreateTenantAsync("val-uncost");
        using var staff = await OwnerClientAsync(tenant);
        var soap = await CreateVariantAsync(staff, "Lux Soap", "SOAP-LUX");
        var found = await CreateVariantAsync(staff, "Found Item", "FOUND-1");
        await ReceiveAsync(staff, soap, quantity: 4, unitCost: 45m);
        await AdjustAsync(staff, found, "CountCorrection", 5);

        var report = await staff.GetFromJsonAsync<StockValuation>("/api/reports/inventory/valuation", Cancel);

        Assert.Equal((180m, 1), (report!.TotalValue, report.UncostedItems));
        Assert.Equal(
            [("SOAP-LUX", (decimal?)180m), ("FOUND-1", null)],
            report.Items.Select(item => (item.Sku, item.Value)));
    }

    [Fact]
    public async Task StockValuation_PerLocation_ValuesEachLocationAndFiltersToOne()
    {
        var tenant = await api.CreateTenantAsync("val-loc");
        using var staff = await OwnerClientAsync(tenant);
        var oil = await CreateVariantAsync(staff, "Rupchanda Oil", "OIL-5L");
        var branch = await PostAsync<LocationResponse>(staff, "/api/inventory/locations", new { name = "Dhanmondi", code = "DHK-02" });
        await ReceiveAsync(staff, oil, quantity: 2, unitCost: 800m);
        await PostAsync<ReceiptResponse>(staff, "/api/inventory/receipts", new
        {
            locationId = branch.Id,
            lines = new[] { new { variantId = oil, quantity = 1m, unitCost = 900m } },
        });

        var all = await staff.GetFromJsonAsync<StockValuation>("/api/reports/inventory/valuation", Cancel);
        var atBranch = await staff.GetFromJsonAsync<StockValuation>($"/api/reports/inventory/valuation?locationId={branch.Id}", Cancel);

        // One average over both receipts: (2 × 800 + 900) / 3 = 833.333…
        Assert.Equal([("MAIN", 1666.67m), ("DHK-02", 833.33m)], all!.Locations.Select(location => (location.LocationCode, location.Value)));
        Assert.Equal(2500m, all.TotalValue);
        Assert.Equal((833.33m, 1), (atBranch!.TotalValue, atBranch.TotalCount));
        Assert.Equal("DHK-02", Assert.Single(atBranch.Locations).LocationCode);
    }

    [Fact]
    public async Task LowStock_ListsAvailableAtOrBelowEachItemsReorderLevel()
    {
        var tenant = await api.CreateTenantAsync("low");
        using var staff = await OwnerClientAsync(tenant);
        var belowDefault = await CreateVariantAsync(staff, "Low by default", "A-LOW");
        var belowOwn = await CreateVariantAsync(staff, "Low by its own level", "B-OWN");
        var enough = await CreateVariantAsync(staff, "Plenty", "C-PLENTY");
        var reserved = await CreateVariantAsync(staff, "Low once reserved", "D-RESERVED");
        var raisedOff = await CreateVariantAsync(staff, "Level set to zero", "E-ZERO");
        await ReceiveAsync(staff, belowDefault, quantity: 5, unitCost: 10m);
        await ReceiveAsync(staff, belowOwn, quantity: 8, unitCost: 10m);
        await ReceiveAsync(staff, enough, quantity: 6, unitCost: 10m);
        await ReceiveAsync(staff, reserved, quantity: 7, unitCost: 10m);
        await ReceiveAsync(staff, raisedOff, quantity: 1, unitCost: 10m);
        await SetReorderLevelAsync(staff, belowOwn, 10);
        await SetReorderLevelAsync(staff, raisedOff, 0);
        await using (var scope = await api.CreateTenantScopeAsync(tenant.Identifier))
        {
            var reservations = scope.ServiceProvider.GetRequiredService<IInventoryReservations>();
            Assert.True((await reservations.ReserveAsync(Request(reserved, 3), Cancel)).IsSuccess);
        }

        var report = await staff.GetFromJsonAsync<StockAlertPage>("/api/reports/inventory/low-stock", Cancel);

        // The default level is 5; D has 7 on hand but 3 reserved.
        Assert.Equal(
            [("D-RESERVED", 4m, 5m), ("A-LOW", 5m, 5m), ("B-OWN", 8m, 10m)],
            report!.Items.Select(item => (item.Sku, item.Available, item.ReorderLevel)));
        Assert.Equal(3, report.TotalCount);
    }

    [Fact]
    public async Task NegativeStock_ListsOnlyStockBelowZero()
    {
        var tenant = await api.CreateTenantAsync("neg");
        using var staff = await OwnerClientAsync(tenant);
        var sold = await CreateVariantAsync(staff, "Sold beyond stock", "SOLD-OVER");
        var fine = await CreateVariantAsync(staff, "In stock", "IN-STOCK");
        await ReceiveAsync(staff, sold, quantity: 1, unitCost: 10m);
        await ReceiveAsync(staff, fine, quantity: 1, unitCost: 10m);
        await RecordPosSaleAsync(tenant, sold, quantity: 3);

        var report = await staff.GetFromJsonAsync<StockAlertPage>("/api/reports/inventory/negative-stock", Cancel);

        var item = Assert.Single(report!.Items);
        Assert.Equal(("SOLD-OVER", -2m), (item.Sku, item.OnHand));
    }

    [Fact]
    public async Task InventoryReports_NeedReportsInventoryView()
    {
        var tenant = await api.CreateTenantAsync("rep-inv-auth");
        using var owner = await OwnerClientAsync(tenant);
        await CreateUserAsync(owner, "clerk@shop.test", "Inventory Clerk");
        using var clerk = api.CreateTenantClient(tenant.Identifier, await api.SignInAsync(tenant.Identifier, "clerk@shop.test", "Staff-pass-123"));
        using var anonymous = api.CreateTenantClient(tenant.Identifier);
        string[] reports = ["/api/reports/inventory/valuation", "/api/reports/inventory/low-stock", "/api/reports/inventory/negative-stock"];

        var statuses = new List<(HttpStatusCode Anonymous, HttpStatusCode Clerk, HttpStatusCode Owner)>();
        foreach (var report in reports)
        {
            statuses.Add(((await anonymous.GetAsync(report, Cancel)).StatusCode, (await clerk.GetAsync(report, Cancel)).StatusCode, (await owner.GetAsync(report, Cancel)).StatusCode));
        }

        Assert.All(statuses, status => Assert.Equal((HttpStatusCode.Unauthorized, HttpStatusCode.Forbidden, HttpStatusCode.OK), status));
    }

    [Theory]
    [InlineData("/api/reports/inventory/valuation?pageSize=0")]
    [InlineData("/api/reports/inventory/low-stock?page=0")]
    [InlineData("/api/reports/inventory/negative-stock?pageSize=201")]
    [InlineData("/api/reports/inventory/valuation?locationId=0190f4a0-0000-7000-8000-000000000000")]
    [InlineData("/api/reports/inventory/low-stock?locationId=0190f4a0-0000-7000-8000-000000000000")]
    public async Task InventoryReports_InvalidPagingOrUnknownLocation_ReturnValidationProblem(string url)
    {
        var tenant = await api.CreateTenantAsync("rep-inv-bad");
        using var staff = await OwnerClientAsync(tenant);

        var response = await staff.GetAsync(url, Cancel);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task SetReorderLevel_ByInventoryClerk_IsSavedAndCanBeCleared()
    {
        var tenant = await api.CreateTenantAsync("reorder");
        using var owner = await OwnerClientAsync(tenant);
        var variantId = await CreateVariantAsync(owner, "Teer Sugar", "SUGAR-1KG");
        await CreateUserAsync(owner, "clerk@shop.test", "Inventory Clerk");
        using var clerk = api.CreateTenantClient(tenant.Identifier, await api.SignInAsync(tenant.Identifier, "clerk@shop.test", "Staff-pass-123"));

        var set = await PutReorderLevelAsync(clerk, variantId, 12.5m);
        var cleared = await PutReorderLevelAsync(clerk, variantId, null);

        Assert.Equal((HttpStatusCode.OK, HttpStatusCode.OK), (set.StatusCode, cleared.StatusCode));
        Assert.Equal(12.5m, (await set.Content.ReadFromJsonAsync<ReorderLevelResponse>(Cancel))!.ReorderLevel);
        Assert.Null((await cleared.Content.ReadFromJsonAsync<ReorderLevelResponse>(Cancel))!.ReorderLevel);
    }

    [Fact]
    public async Task SetReorderLevel_NegativeOrUnknownVariant_IsRefused()
    {
        var tenant = await api.CreateTenantAsync("reorder-bad");
        using var staff = await OwnerClientAsync(tenant);
        var variantId = await CreateVariantAsync(staff, "Teer Sugar", "SUGAR-1KG");

        var negative = await PutReorderLevelAsync(staff, variantId, -1);
        var unknown = await PutReorderLevelAsync(staff, Guid.CreateVersion7(), 5);

        Assert.Equal((HttpStatusCode.BadRequest, HttpStatusCode.NotFound), (negative.StatusCode, unknown.StatusCode));
    }

    private static async Task AdjustAsync(HttpClient staff, Guid variantId, string reason, decimal quantityChange) =>
        await PostAsync<AdjustmentResponse>(staff, "/api/inventory/adjustments", new
        {
            reason,
            note = "Report test",
            lines = new[] { new { variantId, quantityChange } },
        });

    private static async Task SetReorderLevelAsync(HttpClient staff, Guid variantId, decimal? level) =>
        await (await PutReorderLevelAsync(staff, variantId, level)).EnsureSuccessWithBodyAsync();

    private static Task<HttpResponseMessage> PutReorderLevelAsync(HttpClient staff, Guid variantId, decimal? level) =>
        staff.PutAsJsonAsync($"/api/inventory/items/{variantId}/reorder-level", new { reorderLevel = level }, Cancel);

    /// <summary>A till sale the ledger had no stock for; POS writes these directly once it exists.</summary>
    private async Task RecordPosSaleAsync(TestTenant tenant, Guid variantId, decimal quantity)
    {
        await using var scope = await api.CreateTenantScopeAsync(tenant.Identifier);
        var db = scope.ServiceProvider.GetRequiredService<InventoryDbContext>();
        var saved = await ConcurrencyRetry.ExecuteAsync<bool>(db, async ct =>
        {
            var now = DateTimeOffset.UtcNow;
            var location = await db.Locations.Where(l => l.IsDefault).Select(l => l.Id).SingleAsync(ct);
            var levels = await db.LoadOrCreateAsync(location, [variantId], now, ct);
            var sale = StockMovement.Record(variantId, location, MovementType.PosSale, -quantity, new StockReference("PosSale", Guid.CreateVersion7()), now, now, null).Value;
            db.Movements.Add(sale);
            levels[variantId].Apply(sale);
            await db.SaveChangesAsync(ct);
            return true;
        }, Cancel);
        Assert.True(saved.IsSuccess);
    }

    private sealed record ReorderLevelResponse(Guid VariantId, string Sku, decimal? ReorderLevel);
}
