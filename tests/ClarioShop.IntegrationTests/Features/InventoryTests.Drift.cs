using System.Net;
using System.Net.Http.Json;
using ClarioShop.IntegrationTests.Infrastructure;
using ClarioShop.Inventory.Contracts;
using ClarioShop.Inventory.Contracts.Events;
using ClarioShop.Inventory.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace ClarioShop.IntegrationTests.Features;

/// <summary>
/// The drift check recomputes stock levels from the ledger and active reservations, corrects and records the ones
/// that disagree; a stock level crossing below zero publishes <see cref="StockWentNegative"/>.
/// </summary>
public sealed partial class InventoryTests
{
    [Fact]
    public async Task DriftCheck_LevelsMatchLedger_FindsNoDrift()
    {
        var (tenant, variantId) = await StockedVariantAsync("drift-none", quantity: 10);
        await ReserveAsync(tenant, variantId, 2);
        using var staff = await OwnerClientAsync(tenant);

        var check = await PostAsync<DriftCheckResponse>(staff, "/api/inventory/drift-checks", new { });

        Assert.Equal((1, 0), (check.CheckedLevels, check.Drifts.Count));
    }

    [Fact]
    public async Task DriftCheck_LevelOffFromLedger_CorrectsLevelAndRecordsDrift()
    {
        var (tenant, variantId) = await StockedVariantAsync("drift-fix", quantity: 10);
        await ReserveAsync(tenant, variantId, 2);
        await TamperLevelAsync(tenant, variantId, onHand: 13, reserved: 0);
        using var staff = await OwnerClientAsync(tenant);

        var check = await PostAsync<DriftCheckResponse>(staff, "/api/inventory/drift-checks", new { });
        var level = await GetStockAsync(staff, variantId);
        var recorded = await staff.GetFromJsonAsync<DriftPage>($"/api/inventory/drifts?variantId={variantId}", Cancel);

        var drift = Assert.Single(check.Drifts);
        Assert.Equal((13m, 10m, 0m, 2m), (drift.RecordedOnHand, drift.LedgerOnHand, drift.RecordedReserved, drift.HeldReserved));
        Assert.Equal((10m, 2m, 8m), (level.OnHand, level.Reserved, level.Available));
        Assert.Equal(drift.Id, Assert.Single(recorded!.Items).Id);
        Assert.Equal(1, await InventoryOutboxCountAsync(tenant, nameof(StockDriftDetected), variantId));
    }

    [Fact]
    public async Task DriftCheck_RunAgainAfterCorrection_FindsNoDrift()
    {
        var (tenant, variantId) = await StockedVariantAsync("drift-again", quantity: 10);
        await TamperLevelAsync(tenant, variantId, onHand: 7, reserved: 0);
        using var staff = await OwnerClientAsync(tenant);
        await PostAsync<DriftCheckResponse>(staff, "/api/inventory/drift-checks", new { });

        var again = await PostAsync<DriftCheckResponse>(staff, "/api/inventory/drift-checks", new { });

        Assert.Empty(again.Drifts);
    }

    [Fact]
    public async Task DriftCheck_LevelRowMissing_RecreatesItFromLedger()
    {
        var (tenant, variantId) = await StockedVariantAsync("drift-lost", quantity: 6);
        await using (var scope = await api.CreateTenantScopeAsync(tenant.Identifier))
        {
            var db = scope.ServiceProvider.GetRequiredService<InventoryDbContext>();
            await db.StockLevels.Where(level => level.VariantId == variantId).ExecuteDeleteAsync(Cancel);
        }

        using var staff = await OwnerClientAsync(tenant);

        var check = await PostAsync<DriftCheckResponse>(staff, "/api/inventory/drift-checks", new { });
        var level = await GetStockAsync(staff, variantId);

        var drift = Assert.Single(check.Drifts);
        Assert.Equal((0m, 6m), (drift.RecordedOnHand, drift.LedgerOnHand));
        Assert.Equal(6m, level.OnHand);
    }

    [Fact]
    public async Task DriftCheck_WhileSalesAreSaved_NeverMistakesThemForDrift()
    {
        var (tenant, variantId) = await StockedVariantAsync("drift-race", quantity: 300);
        using var staff = await OwnerClientAsync(tenant);
        using var salesDone = CancellationTokenSource.CreateLinkedTokenSource(Cancel);

        async Task SellAsync()
        {
            for (var i = 0; i < 200; i++)
            {
                await RecordPosSaleAsync(tenant, variantId, quantity: 1);
            }

            await salesDone.CancelAsync();
        }

        async Task CheckUntilSalesAreDoneAsync()
        {
            while (!salesDone.IsCancellationRequested)
            {
                await PostAsync<DriftCheckResponse>(staff, "/api/inventory/drift-checks", new { });
            }
        }

        await Task.WhenAll(SellAsync(), CheckUntilSalesAreDoneAsync(), CheckUntilSalesAreDoneAsync());

        var recorded = await staff.GetFromJsonAsync<DriftPage>($"/api/inventory/drifts?variantId={variantId}", Cancel);
        Assert.Equal((0, 100m), (recorded!.TotalCount, (await GetStockAsync(staff, variantId)).OnHand));
    }

    [Fact]
    public async Task DriftEndpoints_RequireInventoryPermissions()
    {
        var tenant = await api.CreateTenantAsync("drift-auth");
        using var owner = await OwnerClientAsync(tenant);
        await CreateUserAsync(owner, "orders@shop.test", "Order Handler");
        await CreateUserAsync(owner, "clerk@shop.test", "Inventory Clerk");
        using var handler = api.CreateTenantClient(tenant.Identifier, await api.SignInAsync(tenant.Identifier, "orders@shop.test", "Staff-pass-123"));
        using var clerk = api.CreateTenantClient(tenant.Identifier, await api.SignInAsync(tenant.Identifier, "clerk@shop.test", "Staff-pass-123"));
        using var anonymous = api.CreateTenantClient(tenant.Identifier);

        async Task<(HttpStatusCode, HttpStatusCode)> StatusesAsync(HttpClient client) =>
            ((await client.PostAsync("/api/inventory/drift-checks", null, Cancel)).StatusCode, (await client.GetAsync("/api/inventory/drifts", Cancel)).StatusCode);

        Assert.Equal(
            [(HttpStatusCode.Unauthorized, HttpStatusCode.Unauthorized), (HttpStatusCode.Forbidden, HttpStatusCode.Forbidden), (HttpStatusCode.OK, HttpStatusCode.OK)],
            [await StatusesAsync(anonymous), await StatusesAsync(handler), await StatusesAsync(clerk)]);
    }

    [Fact]
    public async Task ListStockDrifts_InvalidPaging_ReturnsValidationProblem()
    {
        var tenant = await api.CreateTenantAsync("drift-bad");
        using var staff = await OwnerClientAsync(tenant);

        var response = await staff.GetAsync("/api/inventory/drifts?pageSize=201", Cancel);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task StockGoingBelowZero_PublishesStockWentNegativeOnceWhenItCrosses()
    {
        var (tenant, variantId) = await StockedVariantAsync("neg-event", quantity: 3);

        await RecordPosSaleAsync(tenant, variantId, quantity: 2);
        var beforeCrossing = await InventoryOutboxCountAsync(tenant, nameof(StockWentNegative), variantId);
        await RecordPosSaleAsync(tenant, variantId, quantity: 2);
        await RecordPosSaleAsync(tenant, variantId, quantity: 1);

        Assert.Equal((0, 1), (beforeCrossing, await InventoryOutboxCountAsync(tenant, nameof(StockWentNegative), variantId)));
        var published = await api.Events.WaitForAsync<StockWentNegative>(e => e.VariantId == variantId, TimeSpan.FromSeconds(15));
        Assert.Equal(("SKU-NEG-EVENT", "MAIN", -1m), (published.Sku, published.LocationCode, published.OnHand));
    }

    private async Task ReserveAsync(TestTenant tenant, Guid variantId, decimal quantity)
    {
        await using var scope = await api.CreateTenantScopeAsync(tenant.Identifier);
        var reservations = scope.ServiceProvider.GetRequiredService<IInventoryReservations>();
        Assert.True((await reservations.ReserveAsync(Request(variantId, quantity), Cancel)).IsSuccess);
    }

    /// <summary>Changes a stock level behind the ledger's back, as a bug or a hand-edited database would.</summary>
    private async Task TamperLevelAsync(TestTenant tenant, Guid variantId, decimal onHand, decimal reserved)
    {
        await using var scope = await api.CreateTenantScopeAsync(tenant.Identifier);
        var db = scope.ServiceProvider.GetRequiredService<InventoryDbContext>();
        await db.Database.ExecuteSqlAsync(
            $"""UPDATE inventory."StockLevels" SET "OnHand" = {onHand}, "Reserved" = {reserved} WHERE "VariantId" = {variantId}""",
            Cancel);
    }

    /// <summary>Events of a type Inventory wrote for the variant, delivered or not.</summary>
    private async Task<int> InventoryOutboxCountAsync(TestTenant tenant, string eventType, Guid variantId)
    {
        await using var scope = await api.CreateTenantScopeAsync(tenant.Identifier);
        var messages = await scope.ServiceProvider.GetRequiredService<InventoryDbContext>().OutboxMessages.AsNoTracking()
            .Select(message => new { message.Type, message.Content })
            .ToListAsync(Cancel);
        return messages.Count(message => message.Type.Contains(eventType, StringComparison.Ordinal)
            && message.Content.Contains(variantId.ToString(), StringComparison.OrdinalIgnoreCase));
    }

    private sealed record DriftItem(Guid Id, Guid CheckId, Guid VariantId, Guid LocationId, decimal RecordedOnHand, decimal LedgerOnHand, decimal RecordedReserved, decimal HeldReserved);

    private sealed record DriftCheckResponse(Guid CheckId, int CheckedLevels, IReadOnlyList<DriftItem> Drifts);

    private sealed record DriftPage(IReadOnlyList<DriftItem> Items, int TotalCount);
}
