using System.Net;
using System.Net.Http.Json;
using ClarioShop.Catalog.Contracts.Events;
using ClarioShop.IntegrationTests.Infrastructure;

namespace ClarioShop.IntegrationTests.Features;

[Collection(ApiCollection.Name)]
public sealed partial class CatalogTests(ClarioApiFactory api)
{
    private static CancellationToken Cancel => TestContext.Current.CancellationToken;

    [Fact]
    public async Task ProductFlow_CreateAndPublish_ShowsOnStorefrontOnlyAfterPublishing()
    {
        var tenant = await api.CreateTenantAsync("flow");
        using var staff = await OwnerClientAsync(tenant);
        using var shopper = api.CreateTenantClient(tenant.Identifier);

        var category = await PostAsync<CategoryResponse>(staff, "/api/catalog/categories", new { name = "Rice & Grains" });
        var product = await PostAsync<ProductResponse>(staff, "/api/catalog/products", new
        {
            name = "Chinigura Aromatic Rice",
            description = "Premium aromatic rice.",
            categoryId = category.Id,
            brand = "Pran",
            variants = new object[]
            {
                new { sku = "RICE-CHINI-1KG", name = "1 kg", price = 165m, barcodes = new[] { "8941100500123" } },
                new { sku = "RICE-CHINI-5KG", name = "5 kg", price = 790m, compareAtPrice = 825m },
            },
        });

        Assert.Equal("chinigura-aromatic-rice", product.Slug);
        Assert.Equal(HttpStatusCode.NotFound, (await shopper.GetAsync($"/api/catalog/products/{product.Slug}", Cancel)).StatusCode);

        (await staff.PostAsync($"/api/catalog/products/{product.Id}/publish", null, Cancel)).EnsureSuccessStatusCode();

        var page = await shopper.GetFromJsonAsync<ProductPage>($"/api/catalog/products/{product.Slug}", Cancel);
        Assert.Equal("Rice & Grains", page!.Category!.Name);
        Assert.Equal([165m, 790m], page.Variants.Select(v => v.Price));

        var list = await shopper.GetFromJsonAsync<ProductList>("/api/catalog/products?search=chinigura&category=rice-grains", Cancel);
        var card = Assert.Single(list!.Items);
        Assert.Equal(165m, card.PriceFrom);
    }

    [Fact]
    public async Task CreateProduct_PublishesProductCreatedThroughOutbox()
    {
        var tenant = await api.CreateTenantAsync("evt");
        using var staff = await OwnerClientAsync(tenant);

        var product = await PostAsync<ProductResponse>(staff, "/api/catalog/products", new
        {
            name = "Teer Soybean Oil",
            variants = new[] { new { sku = "OIL-TEER-1L", name = "1 L", price = 189m } },
        });

        var received = await api.Events.WaitForAsync<ProductCreated>(e => e.ProductId == product.Id, TimeSpan.FromSeconds(15));
        Assert.Equal("OIL-TEER-1L", Assert.Single(received.Variants).Sku);
        Assert.Equal(1, api.Events.Count<ProductCreated>(e => e.ProductId == product.Id));
    }

    [Fact]
    public async Task Products_AreIsolatedBetweenTenants()
    {
        var shopA = await api.CreateTenantAsync("iso-a");
        var shopB = await api.CreateTenantAsync("iso-b");
        using var staffA = await OwnerClientAsync(shopA);

        var product = await PostAsync<ProductResponse>(staffA, "/api/catalog/products", new
        {
            name = "Fresh Milk",
            variants = new[] { new { sku = "MILK-1L", name = "1 L", price = 90m } },
        });
        (await staffA.PostAsync($"/api/catalog/products/{product.Id}/publish", null, Cancel)).EnsureSuccessStatusCode();

        using var shopperB = api.CreateTenantClient(shopB.Identifier);
        var listB = await shopperB.GetFromJsonAsync<ProductList>("/api/catalog/products", Cancel);
        Assert.Empty(listB!.Items);

        // Same SKU is fine in another shop: databases are separate.
        using var staffB = await OwnerClientAsync(shopB);
        var response = await staffB.PostAsJsonAsync("/api/catalog/products", new
        {
            name = "Fresh Milk",
            variants = new[] { new { sku = "MILK-1L", name = "1 L", price = 95m } },
        }, Cancel);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }

    [Fact]
    public async Task TokenFromAnotherTenant_IsForbidden()
    {
        var shopA = await api.CreateTenantAsync("tok-a");
        var shopB = await api.CreateTenantAsync("tok-b");
        var tokenA = await api.SignInAsync(shopA.Identifier, shopA.OwnerEmail, shopA.OwnerPassword);
        using var wrongShop = api.CreateTenantClient(shopB.Identifier, tokenA);

        var response = await wrongShop.PostAsJsonAsync("/api/catalog/categories", new { name = "Snacks" }, Cancel);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task CreateProduct_WithoutToken_ReturnsUnauthorized()
    {
        var tenant = await api.CreateTenantAsync("anon");
        using var shopper = api.CreateTenantClient(tenant.Identifier);

        var response = await shopper.PostAsJsonAsync("/api/catalog/products", new { name = "X", variants = Array.Empty<object>() }, Cancel);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task CatalogManager_CannotManageUsers()
    {
        var tenant = await api.CreateTenantAsync("perm");
        using var owner = await OwnerClientAsync(tenant);
        (await owner.PostAsJsonAsync("/api/identity/users", new
        {
            email = "manager@shop.test",
            fullName = "Catalog Person",
            password = "Manager-pass-1",
            roles = new[] { "Catalog Manager" },
        }, Cancel)).EnsureSuccessStatusCode();

        var managerToken = await api.SignInAsync(tenant.Identifier, "manager@shop.test", "Manager-pass-1");
        using var manager = api.CreateTenantClient(tenant.Identifier, managerToken);

        var canEditCatalog = await manager.PostAsJsonAsync("/api/catalog/categories", new { name = "Snacks" }, Cancel);
        var canManageUsers = await manager.PostAsJsonAsync("/api/identity/users", new
        {
            email = "x@shop.test",
            fullName = "X",
            password = "Xxxxxxx-1",
            roles = new[] { "Owner" },
        }, Cancel);

        Assert.Equal(HttpStatusCode.Created, canEditCatalog.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, canManageUsers.StatusCode);
    }

    [Theory]
    [InlineData("""{ "name": "No variants", "variants": [] }""")]
    [InlineData("""{ "name": "", "variants": [ { "sku": "A-1", "price": 10 } ] }""")]
    [InlineData("""{ "name": "Bad price", "variants": [ { "sku": "A-1", "price": 0 } ] }""")]
    [InlineData("""{ "name": "Bad sku", "variants": [ { "sku": "has space", "price": 10 } ] }""")]
    public async Task CreateProduct_InvalidInput_ReturnsValidationProblem(string body)
    {
        var tenant = await api.CreateTenantAsync("val");
        using var staff = await OwnerClientAsync(tenant);

        var response = await staff.PostAsync("/api/catalog/products", new StringContent(body, System.Text.Encoding.UTF8, "application/json"), Cancel);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task CreateProduct_SkuAlreadyUsed_ReturnsConflict()
    {
        var tenant = await api.CreateTenantAsync("sku");
        using var staff = await OwnerClientAsync(tenant);
        var request = new { name = "Sugar", variants = new[] { new { sku = "SUGAR-1KG", name = "1 kg", price = 140m } } };
        await PostAsync<ProductResponse>(staff, "/api/catalog/products", request);

        var response = await staff.PostAsJsonAsync("/api/catalog/products", request, Cancel);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    private async Task<HttpClient> OwnerClientAsync(TestTenant tenant) =>
        api.CreateTenantClient(tenant.Identifier, await api.SignInAsync(tenant.Identifier, tenant.OwnerEmail, tenant.OwnerPassword));

    private static async Task<T> PostAsync<T>(HttpClient client, string url, object body)
    {
        var response = await client.PostAsJsonAsync(url, body, Cancel);
        await response.EnsureSuccessWithBodyAsync();
        return (await response.Content.ReadFromJsonAsync<T>(Cancel))!;
    }

    private sealed record CategoryResponse(Guid Id, string Name, string Slug);

    private sealed record ProductResponse(Guid Id, string Slug, string Status);

    private sealed record ProductPage(Guid Id, string Name, string Slug, CategoryRef? Category, IReadOnlyList<VariantItem> Variants);

    private sealed record CategoryRef(string Name, string Slug);

    private sealed record VariantItem(Guid Id, string Sku, string Name, decimal Price, decimal? CompareAtPrice);

    private sealed record ProductList(IReadOnlyList<ProductCard> Items, int TotalCount);

    private sealed record ProductCard(Guid Id, string Name, string Slug, decimal PriceFrom);
}
