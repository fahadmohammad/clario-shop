using System.Net;
using System.Net.Http.Json;
using ClarioShop.IntegrationTests.Infrastructure;

namespace ClarioShop.IntegrationTests.Features;

[Collection(ApiCollection.Name)]
public sealed class PlanGatingTests(ClarioApiFactory api)
{
    private static CancellationToken Cancel => TestContext.Current.CancellationToken;

    [Fact]
    public async Task CreateLocation_OnlinePlan_ReturnsForbiddenWithReason()
    {
        var tenant = await api.CreateTenantAsync("plan-loc", "Online");
        using var owner = await OwnerClientAsync(tenant);

        var response = await owner.PostAsJsonAsync("/api/inventory/locations", new { name = "Dhanmondi", code = "DHK-02" }, Cancel);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<PlanProblem>(Cancel);
        Assert.Equal(("plan_feature_unavailable", "MultipleLocations"), (problem!.Code, problem.Feature));
        Assert.Equal("This shop's plan (Online) does not include more than one location.", problem.Detail);
    }

    [Fact]
    public async Task ListTransfers_OnlinePlan_ReturnsForbidden()
    {
        var tenant = await api.CreateTenantAsync("plan-trf", "Online");
        using var owner = await OwnerClientAsync(tenant);

        var response = await owner.GetAsync("/api/inventory/transfers", Cancel);

        await AssertPlanForbiddenAsync(response, "MultipleLocations");
    }

    [Fact]
    public async Task CreateCart_OnlinePlan_IsAllowed()
    {
        var tenant = await api.CreateTenantAsync("plan-cart", "Online");
        using var guest = api.CreateTenantClient(tenant.Identifier);

        var response = await guest.PostAsync("/api/ordering/carts", null, Cancel);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }

    [Fact]
    public async Task CreateCart_PosPlan_ReturnsForbidden()
    {
        var tenant = await api.CreateTenantAsync("plan-pcart", "Pos");
        using var guest = api.CreateTenantClient(tenant.Identifier);

        var response = await guest.PostAsync("/api/ordering/carts", null, Cancel);

        await AssertPlanForbiddenAsync(response, "OnlineStore");
    }

    [Fact]
    public async Task Checkout_PosPlan_ReturnsForbiddenBeforeValidation()
    {
        var tenant = await api.CreateTenantAsync("plan-chk", "Pos");
        using var guest = api.CreateTenantClient(tenant.Identifier);

        var response = await guest.PostAsJsonAsync("/api/ordering/checkout", new { cartId = Guid.NewGuid() }, Cancel);

        await AssertPlanForbiddenAsync(response, "OnlineStore");
    }

    [Fact]
    public async Task ListOrders_PosPlan_ReturnsForbidden()
    {
        var tenant = await api.CreateTenantAsync("plan-ord", "Pos");
        using var owner = await OwnerClientAsync(tenant);

        var response = await owner.GetAsync("/api/ordering/orders", Cancel);

        await AssertPlanForbiddenAsync(response, "OnlineStore");
    }

    [Fact]
    public async Task ListOrders_PosPlanWithoutToken_ReturnsUnauthorized()
    {
        var tenant = await api.CreateTenantAsync("plan-anon", "Pos");
        using var guest = api.CreateTenantClient(tenant.Identifier);

        var response = await guest.GetAsync("/api/ordering/orders", Cancel);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task OrderPayments_PosPlan_ReturnsForbidden()
    {
        var tenant = await api.CreateTenantAsync("plan-pay", "Pos");
        using var owner = await OwnerClientAsync(tenant);

        var payments = await owner.GetAsync($"/api/payments/orders/{Guid.NewGuid()}", Cancel);
        var session = await owner.PostAsync($"/api/payments/orders/{Guid.NewGuid()}/gateway-sessions", null, Cancel);

        await AssertPlanForbiddenAsync(payments, "OnlineStore");
        await AssertPlanForbiddenAsync(session, "OnlineStore");
    }

    [Fact]
    public async Task MyAccount_PosPlan_ReturnsForbidden()
    {
        var tenant = await api.CreateTenantAsync("plan-me", "Pos");
        using var shopper = api.CreateTenantClient(tenant.Identifier, await api.SignInShopperAsync(tenant.Identifier, "01712-345678"));

        var profile = await shopper.GetAsync("/api/customers/me", Cancel);
        var orders = await shopper.GetAsync("/api/ordering/me/orders", Cancel);

        await AssertPlanForbiddenAsync(profile, "OnlineStore");
        await AssertPlanForbiddenAsync(orders, "OnlineStore");
    }

    [Fact]
    public async Task CreateLocation_PosPlan_IsAllowed()
    {
        var tenant = await api.CreateTenantAsync("plan-ploc", "Pos");
        using var owner = await OwnerClientAsync(tenant);

        var response = await owner.PostAsJsonAsync("/api/inventory/locations", new { name = "Dhanmondi", code = "DHK-02" }, Cancel);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }

    [Fact]
    public async Task CreateTenant_WithoutPlan_GetsRetailSuite()
    {
        var tenant = await api.CreateTenantAsync("plan-def");
        using var platform = api.CreatePlatformClient();

        var tenants = await platform.GetFromJsonAsync<List<TenantItem>>("/api/platform/tenants", Cancel);

        Assert.Equal("RetailSuite", tenants!.Single(item => item.Identifier == tenant.Identifier).Plan);
    }

    [Fact]
    public async Task ChangePlan_RetailSuiteToOnlineAndBack_GatesFromTheNextRequest()
    {
        var tenant = await api.CreateTenantAsync("plan-chg");
        using var owner = await OwnerClientAsync(tenant);
        using var platform = api.CreatePlatformClient();
        var first = await owner.PostAsJsonAsync("/api/inventory/locations", new { name = "Branch 2", code = "B-02" }, Cancel);

        var downgrade = await platform.PutAsJsonAsync($"/api/platform/tenants/{tenant.Identifier}/plan", new { plan = "Online" }, Cancel);
        var whileOnline = await owner.PostAsJsonAsync("/api/inventory/locations", new { name = "Branch 3", code = "B-03" }, Cancel);
        var upgrade = await platform.PutAsJsonAsync($"/api/platform/tenants/{tenant.Identifier}/plan", new { plan = "RetailSuite" }, Cancel);
        var afterUpgrade = await owner.PostAsJsonAsync("/api/inventory/locations", new { name = "Branch 3", code = "B-03" }, Cancel);

        Assert.Equal(HttpStatusCode.Created, first.StatusCode);
        Assert.Equal("Online", (await downgrade.Content.ReadFromJsonAsync<PlanResponse>(Cancel))!.Plan);
        await AssertPlanForbiddenAsync(whileOnline, "MultipleLocations");
        await upgrade.EnsureSuccessWithBodyAsync();
        Assert.Equal(HttpStatusCode.Created, afterUpgrade.StatusCode);
    }

    [Fact]
    public async Task ChangePlan_UnknownTenant_ReturnsNotFound()
    {
        using var platform = api.CreatePlatformClient();

        var response = await platform.PutAsJsonAsync("/api/platform/tenants/no-such-shop/plan", new { plan = "Online" }, Cancel);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task ChangePlan_UnknownPlan_ReturnsBadRequest()
    {
        var tenant = await api.CreateTenantAsync("plan-bad");
        using var platform = api.CreatePlatformClient();

        var response = await platform.PutAsJsonAsync($"/api/platform/tenants/{tenant.Identifier}/plan", new { plan = "Gold" }, Cancel);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task ChangePlan_WithoutPlatformKey_ReturnsUnauthorized()
    {
        var tenant = await api.CreateTenantAsync("plan-key");
        using var client = api.CreateClient();

        var response = await client.PutAsJsonAsync($"/api/platform/tenants/{tenant.Identifier}/plan", new { plan = "Online" }, Cancel);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Theory]
    [InlineData("Online", new[] { "OnlineStore" })]
    [InlineData("Pos", new[] { "Pos", "MultipleLocations" })]
    [InlineData("RetailSuite", new[] { "OnlineStore", "Pos", "MultipleLocations" })]
    public async Task Storefront_EachPlan_ListsItsFeatures(string plan, string[] expected)
    {
        var tenant = await api.CreateTenantAsync("plan-sf", plan);
        using var guest = api.CreateTenantClient(tenant.Identifier);

        var storefront = await guest.GetFromJsonAsync<StorefrontFeatures>("/api/settings/storefront", Cancel);

        Assert.Equal(expected, storefront!.Features);
    }

    private static async Task AssertPlanForbiddenAsync(HttpResponseMessage response, string feature)
    {
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<PlanProblem>(Cancel);
        Assert.Equal(("plan_feature_unavailable", feature), (problem!.Code, problem.Feature));
    }

    private async Task<HttpClient> OwnerClientAsync(TestTenant tenant) =>
        api.CreateTenantClient(tenant.Identifier, await api.SignInAsync(tenant.Identifier, tenant.OwnerEmail, tenant.OwnerPassword));

    private sealed record PlanProblem(string Title, string Detail, string Code, string Feature);

    private sealed record PlanResponse(string Identifier, string Plan);

    private sealed record TenantItem(string Identifier, string Plan);

    private sealed record StorefrontFeatures(IReadOnlyList<string> Features);
}
