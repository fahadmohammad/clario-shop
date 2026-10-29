using System.Net;
using System.Net.Http.Json;
using ClarioShop.IntegrationTests.Infrastructure;

namespace ClarioShop.IntegrationTests.Features;

/// <summary>
/// A stock transfer moves available stock between two locations as one document: a TransferOut at the source and a
/// TransferIn at the destination, saved together or not at all.
/// </summary>
public sealed partial class InventoryTests
{
    [Fact]
    public async Task Transfer_AvailableStock_MovesItBetweenLocations()
    {
        var (tenant, variantId) = await StockedVariantAsync("xfer", quantity: 10);
        using var staff = await OwnerClientAsync(tenant);
        var (main, branch) = await MainAndBranchAsync(staff);

        var transfer = await PostAsync<TransferResponse>(staff, "/api/inventory/transfers", new
        {
            fromLocationId = main,
            toLocationId = branch,
            note = "Weekend restock",
            lines = new[] { new { variantId, quantity = 4 } },
        });

        Assert.Equal(6m, Assert.Single(transfer.From).OnHand);
        Assert.Equal(4m, Assert.Single(transfer.To).OnHand);
        Assert.Equal((6m, 4m), ((await StockAtAsync(staff, variantId, main)).OnHand, (await StockAtAsync(staff, variantId, branch)).OnHand));
    }

    [Fact]
    public async Task Transfer_WritesPairedLedgerEntriesUnderOneDocument()
    {
        var (tenant, variantId) = await StockedVariantAsync("xfer-ledger", quantity: 10);
        using var staff = await OwnerClientAsync(tenant);
        var (main, branch) = await MainAndBranchAsync(staff);

        var transfer = await TransferAsync(staff, main, branch, variantId, 3);
        var ledger = await staff.GetFromJsonAsync<MovementPage>($"/api/inventory/movements?variantId={variantId}", Cancel);

        var moved = ledger!.Items.Where(m => m.ReferenceId == transfer.Id).OrderBy(m => m.Quantity).ToList();
        Assert.Equal(2, moved.Count);
        Assert.Equal(("TransferOut", -3m, main), (moved[0].Type, moved[0].Quantity, moved[0].LocationId));
        Assert.Equal(("TransferIn", 3m, branch), (moved[1].Type, moved[1].Quantity, moved[1].LocationId));
        Assert.All(moved, m => Assert.Equal("StockTransfer", m.ReferenceType));
        Assert.All(moved, m => Assert.NotNull(m.UserId));
    }

    [Fact]
    public async Task Transfer_ListedAndShownWithLinesAndLocationCodes()
    {
        var (tenant, variantId) = await StockedVariantAsync("xfer-read", quantity: 10);
        using var staff = await OwnerClientAsync(tenant);
        var (main, branch) = await MainAndBranchAsync(staff);
        var transfer = await TransferAsync(staff, main, branch, variantId, 2.5m);

        var atBranch = await staff.GetFromJsonAsync<TransferPage>($"/api/inventory/transfers?locationId={branch}", Cancel);
        var forVariant = await staff.GetFromJsonAsync<TransferPage>($"/api/inventory/transfers?variantId={variantId}", Cancel);
        var detail = await staff.GetFromJsonAsync<TransferDetail>($"/api/inventory/transfers/{transfer.Id}", Cancel);

        var listed = Assert.Single(atBranch!.Items);
        Assert.Equal((transfer.Id, "MAIN", "DHK-02", 1, 2.5m), (listed.Id, listed.FromLocationCode, listed.ToLocationCode, listed.LineCount, listed.TotalQuantity));
        Assert.Equal(transfer.Id, Assert.Single(forVariant!.Items).Id);
        var line = Assert.Single(detail!.Lines);
        Assert.Equal(("MAIN", "DHK-02"), (detail.FromLocationCode, detail.ToLocationCode));
        Assert.Equal((variantId, "SKU-XFER-READ", 2.5m), (line.VariantId, line.Sku, line.Quantity));
    }

    [Fact]
    public async Task Transfer_UnknownId_ReturnsNotFound()
    {
        var tenant = await api.CreateTenantAsync("xfer-404");
        using var staff = await OwnerClientAsync(tenant);

        var response = await staff.GetAsync($"/api/inventory/transfers/{Guid.CreateVersion7()}", Cancel);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Transfer_MoreThanAvailable_ReturnsConflictAndMovesNothing()
    {
        var (tenant, variantId) = await StockedVariantAsync("xfer-short", quantity: 5);
        await ReserveAsync(tenant, variantId, 3);
        using var staff = await OwnerClientAsync(tenant);
        var (main, branch) = await MainAndBranchAsync(staff);

        var response = await PostTransferAsync(staff, main, branch, variantId, 3);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        var source = await StockAtAsync(staff, variantId, main);
        Assert.Equal((5m, 3m), (source.OnHand, source.Reserved));
        var transfers = await staff.GetFromJsonAsync<TransferPage>("/api/inventory/transfers", Cancel);
        Assert.Equal(0, transfers!.TotalCount);
    }

    [Theory]
    [InlineData("same-location")]
    [InlineData("unknown-location")]
    [InlineData("zero-quantity")]
    [InlineData("no-lines")]
    public async Task Transfer_InvalidRequest_ReturnsValidationProblem(string problem)
    {
        var (tenant, variantId) = await StockedVariantAsync($"xfer-{problem}", quantity: 5);
        using var staff = await OwnerClientAsync(tenant);
        var (main, branch) = await MainAndBranchAsync(staff);
        object body = problem switch
        {
            "same-location" => new { fromLocationId = main, toLocationId = main, lines = new[] { new { variantId, quantity = 1 } } },
            "unknown-location" => new { fromLocationId = main, toLocationId = Guid.CreateVersion7(), lines = new[] { new { variantId, quantity = 1 } } },
            "zero-quantity" => new { fromLocationId = main, toLocationId = branch, lines = new[] { new { variantId, quantity = 0 } } },
            _ => new { fromLocationId = main, toLocationId = branch, lines = Array.Empty<object>() },
        };

        var response = await staff.PostAsJsonAsync("/api/inventory/transfers", body, Cancel);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(5m, (await StockAtAsync(staff, variantId, main)).OnHand);
    }

    [Fact]
    public async Task Transfer_InventoryClerkAllowed_OrderHandlerForbidden_AnonymousUnauthorized()
    {
        var (tenant, variantId) = await StockedVariantAsync("xfer-auth", quantity: 5);
        using var owner = await OwnerClientAsync(tenant);
        var (main, branch) = await MainAndBranchAsync(owner);
        await CreateUserAsync(owner, "clerk@shop.test", "Inventory Clerk");
        await CreateUserAsync(owner, "orders@shop.test", "Order Handler");
        using var clerk = api.CreateTenantClient(tenant.Identifier, await api.SignInAsync(tenant.Identifier, "clerk@shop.test", "Staff-pass-123"));
        using var orders = api.CreateTenantClient(tenant.Identifier, await api.SignInAsync(tenant.Identifier, "orders@shop.test", "Staff-pass-123"));
        using var anonymous = api.CreateTenantClient(tenant.Identifier);

        var byClerk = await PostTransferAsync(clerk, main, branch, variantId, 1);
        var byOrders = await PostTransferAsync(orders, main, branch, variantId, 1);
        var byAnonymous = await PostTransferAsync(anonymous, main, branch, variantId, 1);

        Assert.Equal(
            (HttpStatusCode.Created, HttpStatusCode.Forbidden, HttpStatusCode.Unauthorized),
            (byClerk.StatusCode, byOrders.StatusCode, byAnonymous.StatusCode));
    }

    [Fact]
    public async Task Transfer_ManyConcurrentTransfers_NeverSendMoreThanAvailable()
    {
        const int Stock = 5;
        const int Transfers = 12;
        var (tenant, variantId) = await StockedVariantAsync("xfer-race", quantity: Stock);
        using var staff = await OwnerClientAsync(tenant);
        var (main, branch) = await MainAndBranchAsync(staff);
        var token = await api.SignInAsync(tenant.Identifier, tenant.OwnerEmail, tenant.OwnerPassword);

        var attempts = Enumerable.Range(0, Transfers).Select(async _ =>
        {
            using var client = api.CreateTenantClient(tenant.Identifier, token);
            return (await PostTransferAsync(client, main, branch, variantId, 1)).StatusCode;
        });
        var results = await Task.WhenAll(attempts);

        Assert.Equal(Stock, results.Count(status => status == HttpStatusCode.Created));
        Assert.All(results.Where(status => status != HttpStatusCode.Created), status => Assert.Equal(HttpStatusCode.Conflict, status));
        Assert.Equal((0m, 5m), ((await StockAtAsync(staff, variantId, main)).OnHand, (await StockAtAsync(staff, variantId, branch)).OnHand));
        var check = await PostAsync<DriftCheckResponse>(staff, "/api/inventory/drift-checks", new { });
        Assert.Empty(check.Drifts);
    }

    private static async Task<(Guid Main, Guid Branch)> MainAndBranchAsync(HttpClient staff)
    {
        var branch = await PostAsync<LocationResponse>(staff, "/api/inventory/locations", new { name = "Dhanmondi branch", code = "DHK-02" });
        var locations = await staff.GetFromJsonAsync<List<LocationItem>>("/api/inventory/locations", Cancel);
        return (locations!.Single(l => l.IsDefault).Id, branch.Id);
    }

    private static async Task<TransferResponse> TransferAsync(HttpClient staff, Guid from, Guid to, Guid variantId, decimal quantity)
    {
        var response = await PostTransferAsync(staff, from, to, variantId, quantity);
        await response.EnsureSuccessWithBodyAsync();
        return (await response.Content.ReadFromJsonAsync<TransferResponse>(Cancel))!;
    }

    private static Task<HttpResponseMessage> PostTransferAsync(HttpClient client, Guid from, Guid to, Guid variantId, decimal quantity) =>
        client.PostAsJsonAsync("/api/inventory/transfers", new
        {
            fromLocationId = from,
            toLocationId = to,
            lines = new[] { new { variantId, quantity } },
        }, Cancel);

    private static async Task<StockRow> StockAtAsync(HttpClient staff, Guid variantId, Guid locationId)
    {
        var page = await staff.GetFromJsonAsync<StockPage>($"/api/inventory/stock?variantId={variantId}&locationId={locationId}", Cancel);
        return Assert.Single(page!.Items);
    }

    private sealed record TransferResponse(Guid Id, Guid FromLocationId, Guid ToLocationId, IReadOnlyList<LevelItem> From, IReadOnlyList<LevelItem> To);

    private sealed record TransferItem(Guid Id, string FromLocationCode, string ToLocationCode, int LineCount, decimal TotalQuantity);

    private sealed record TransferPage(IReadOnlyList<TransferItem> Items, int TotalCount);

    private sealed record TransferLine(Guid VariantId, string Sku, decimal Quantity);

    private sealed record TransferDetail(Guid Id, string FromLocationCode, string ToLocationCode, IReadOnlyList<TransferLine> Lines);
}
