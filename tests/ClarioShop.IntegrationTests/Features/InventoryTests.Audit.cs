using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace ClarioShop.IntegrationTests.Features;

/// <summary>Every stock adjustment writes an audit row: who, from which device, and stock on hand before and after.</summary>
public sealed partial class InventoryTests
{
    [Fact]
    public async Task Adjustment_WritesAuditRowWithBeforeAfterUserAndDevice()
    {
        var (tenant, variantId) = await StockedVariantAsync("audit-adj", quantity: 10);
        using var staff = await OwnerClientAsync(tenant);
        staff.DefaultRequestHeaders.Add("X-Device-Id", "till-01");

        var adjustment = await PostAsync<AdjustmentResponse>(staff, "/api/inventory/adjustments", new
        {
            reason = "damaged",
            note = "Dropped crate",
            lines = new[] { new { variantId, quantityChange = -3 } },
        });
        var log = await staff.GetFromJsonAsync<AuditPage>($"/api/inventory/audit-log?entityId={adjustment.Id}", Cancel);

        var entry = Assert.Single(log!.Items);
        Assert.Equal(("inventory.stock.adjusted", "StockAdjustment", "till-01"), (entry.Action, entry.EntityType, entry.Device));
        Assert.NotNull(entry.UserId);
        Assert.Equal(10m, OnHandIn(entry.Before!.Value, variantId));
        Assert.Equal(7m, OnHandIn(entry.After!.Value, variantId));
        Assert.Equal(("Damaged", "Dropped crate"), (entry.After.Value.GetProperty("reason").GetString(), entry.After.Value.GetProperty("note").GetString()));
    }

    [Fact]
    public async Task Adjustment_Refused_WritesNoAuditRow()
    {
        var (tenant, variantId) = await StockedVariantAsync("audit-adj-409", quantity: 2);
        using var staff = await OwnerClientAsync(tenant);

        var response = await staff.PostAsJsonAsync("/api/inventory/adjustments", new
        {
            reason = "theft",
            lines = new[] { new { variantId, quantityChange = -5 } },
        }, Cancel);
        var log = await staff.GetFromJsonAsync<AuditPage>("/api/inventory/audit-log?action=inventory.stock.adjusted", Cancel);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal(0, log!.TotalCount);
    }

    [Fact]
    public async Task InventoryAuditLog_OwnerOnly_InventoryClerkForbidden()
    {
        var tenant = await api.CreateTenantAsync("audit-inv-auth");
        using var owner = await OwnerClientAsync(tenant);
        await CreateUserAsync(owner, "clerk@shop.test", "Inventory Clerk");
        using var clerk = api.CreateTenantClient(tenant.Identifier, await api.SignInAsync(tenant.Identifier, "clerk@shop.test", "Staff-pass-123"));

        var byOwner = await owner.GetAsync("/api/inventory/audit-log", Cancel);
        var byClerk = await clerk.GetAsync("/api/inventory/audit-log", Cancel);

        Assert.Equal((HttpStatusCode.OK, HttpStatusCode.Forbidden), (byOwner.StatusCode, byClerk.StatusCode));
    }

    private static decimal OnHandIn(JsonElement values, Guid variantId) =>
        values.GetProperty("levels").EnumerateArray()
            .Single(level => level.GetProperty("variantId").GetGuid() == variantId)
            .GetProperty("onHand").GetDecimal();

    private sealed record AuditItem(string Action, string EntityType, Guid EntityId, JsonElement? Before, JsonElement? After, Guid? UserId, string? Device);

    private sealed record AuditPage(IReadOnlyList<AuditItem> Items, int TotalCount);
}
