using System.Net;
using System.Net.Http.Json;
using ClarioShop.IntegrationTests.Infrastructure;

namespace ClarioShop.IntegrationTests.Features;

/// <summary>Brands, the storefront brand filter, and the back-office product list.</summary>
public sealed partial class CatalogTests
{
    [Fact]
    public async Task Storefront_FilterByBrand_ShowsOnlyThatBrandWithItsName()
    {
        var tenant = await api.CreateTenantAsync("brand-filter");
        using var staff = await OwnerClientAsync(tenant);
        using var shopper = api.CreateTenantClient(tenant.Identifier);
        var pran = await PostAsync<BrandItem>(staff, "/api/catalog/brands", new { name = "Pran" });
        var aci = await PostAsync<BrandItem>(staff, "/api/catalog/brands", new { name = "ACI Pure" });
        var juice = await CreatePublishedAsync(staff, "Mango Juice", "JUICE-1", pran.Id);
        await CreatePublishedAsync(staff, "Iodized Salt", "SALT-1", aci.Id);
        await CreatePublishedAsync(staff, "Loose Lentils", "LENTIL-1", brandId: null);

        var list = await shopper.GetFromJsonAsync<BrandedList>("/api/catalog/products?brand=pran", Cancel);
        var page = await shopper.GetFromJsonAsync<BrandedPage>($"/api/catalog/products/{juice.Slug}", Cancel);
        var brands = await shopper.GetFromJsonAsync<List<BrandItem>>("/api/catalog/brands", Cancel);

        var card = Assert.Single(list!.Items);
        Assert.Equal(("Mango Juice", "Pran"), (card.Name, card.Brand));
        Assert.Equal(new BrandRef("Pran", "pran"), page!.Brand);
        Assert.Equal(["ACI Pure", "Pran"], brands!.Select(b => b.Name));
    }

    [Fact]
    public async Task CreateBrand_SameNameInAnotherCase_ReturnsConflict()
    {
        var tenant = await api.CreateTenantAsync("brand-dup");
        using var staff = await OwnerClientAsync(tenant);
        await PostAsync<BrandItem>(staff, "/api/catalog/brands", new { name = "Square" });

        var response = await staff.PostAsJsonAsync("/api/catalog/brands", new { name = " SQUARE " }, Cancel);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task CreateBrand_SameNameConcurrently_CreatesOneAndRefusesTheRest()
    {
        var tenant = await api.CreateTenantAsync("brand-race");
        using var staff = await OwnerClientAsync(tenant);

        var responses = await Task.WhenAll(Enumerable.Range(0, 6).Select(i =>
            staff.PostAsJsonAsync("/api/catalog/brands", new { name = i % 2 == 0 ? "Fresh" : "FRESH" }, Cancel)));

        Assert.Equal(1, responses.Count(r => r.StatusCode == HttpStatusCode.Created));
        Assert.All(responses.Where(r => r.StatusCode != HttpStatusCode.Created), r => Assert.Equal(HttpStatusCode.Conflict, r.StatusCode));
        var brands = await staff.GetFromJsonAsync<List<BrandItem>>("/api/catalog/brands", Cancel);
        Assert.Single(brands!);
    }

    [Fact]
    public async Task RenameBrand_KeepsSlugAndRenamesOnStorefront()
    {
        var tenant = await api.CreateTenantAsync("brand-rename");
        using var staff = await OwnerClientAsync(tenant);
        using var shopper = api.CreateTenantClient(tenant.Identifier);
        var brand = await PostAsync<BrandItem>(staff, "/api/catalog/brands", new { name = "Pran" });
        await CreatePublishedAsync(staff, "Mango Juice", "JUICE-1", brand.Id);

        var renamed = await PutAsync<BrandItem>(staff, $"/api/catalog/brands/{brand.Id}", new { name = "PRAN Foods" });

        Assert.Equal(("PRAN Foods", "pran"), (renamed.Name, renamed.Slug));
        var list = await shopper.GetFromJsonAsync<BrandedList>("/api/catalog/products?brand=pran", Cancel);
        Assert.Equal("PRAN Foods", Assert.Single(list!.Items).Brand);
    }

    [Fact]
    public async Task RenameBrand_ToAnotherBrandsName_ReturnsConflict()
    {
        var tenant = await api.CreateTenantAsync("brand-rename-409");
        using var staff = await OwnerClientAsync(tenant);
        await PostAsync<BrandItem>(staff, "/api/catalog/brands", new { name = "Pran" });
        var other = await PostAsync<BrandItem>(staff, "/api/catalog/brands", new { name = "RFL" });

        var taken = await staff.PutAsJsonAsync($"/api/catalog/brands/{other.Id}", new { name = "pran" }, Cancel);
        var ownCase = await staff.PutAsJsonAsync($"/api/catalog/brands/{other.Id}", new { name = "rfl" }, Cancel);
        var unknown = await staff.PutAsJsonAsync($"/api/catalog/brands/{Guid.CreateVersion7()}", new { name = "X" }, Cancel);

        Assert.Equal(
            (HttpStatusCode.Conflict, HttpStatusCode.OK, HttpStatusCode.NotFound),
            (taken.StatusCode, ownCase.StatusCode, unknown.StatusCode));
    }

    [Fact]
    public async Task CreateProduct_UnknownBrand_ReturnsValidationProblem()
    {
        var tenant = await api.CreateTenantAsync("brand-unknown");
        using var staff = await OwnerClientAsync(tenant);

        var response = await staff.PostAsJsonAsync("/api/catalog/products", new
        {
            name = "Sugar",
            brandId = Guid.CreateVersion7(),
            variants = new[] { new { sku = "SUGAR-1KG", price = 140m } },
        }, Cancel);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task CreateBrand_ByShopper_ReturnsUnauthorized()
    {
        var tenant = await api.CreateTenantAsync("brand-anon");
        using var shopper = api.CreateTenantClient(tenant.Identifier);

        var response = await shopper.PostAsJsonAsync("/api/catalog/brands", new { name = "Pran" }, Cancel);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task ListAllProducts_IncludesDraftsAndArchivedNewestFirst()
    {
        var tenant = await api.CreateTenantAsync("bo-list");
        using var staff = await OwnerClientAsync(tenant);
        var published = await CreatePublishedAsync(staff, "Mango Juice", "JUICE-1", brandId: null);
        var archived = await CreateProductAsync(staff, "Old Soap", "SOAP-OLD");
        await PostAsync<ProductDetails>(staff, $"/api/catalog/products/{archived.Id}/archive", new { });
        var draft = await CreateProductAsync(staff, "New Biscuit", "BISCUIT-1", price: 30m);

        var list = await staff.GetFromJsonAsync<BackOfficeList>("/api/catalog/back-office/products", Cancel);

        Assert.Equal(3, list!.TotalCount);
        Assert.Equal([draft.Id, archived.Id, published.Id], list.Items.Select(p => p.Id));
        Assert.Equal(("Draft", 1, 30m), (list.Items[0].Status, list.Items[0].VariantCount, list.Items[0].PriceFrom));
        Assert.Equal("Archived", list.Items[1].Status);
    }

    [Theory]
    [InlineData("?status=draft", "New Biscuit")]
    [InlineData("?status=Published", "Mango Juice")]
    [InlineData("?search=soap-old", "Old Soap")]
    [InlineData("?search=biscu", "New Biscuit")]
    public async Task ListAllProducts_Filtered_ReturnsMatchingProduct(string filter, string expected)
    {
        var tenant = await api.CreateTenantAsync("bo-filter");
        using var staff = await OwnerClientAsync(tenant);
        await CreatePublishedAsync(staff, "Mango Juice", "JUICE-1", brandId: null);
        var archived = await CreateProductAsync(staff, "Old Soap", "SOAP-OLD");
        await PostAsync<ProductDetails>(staff, $"/api/catalog/products/{archived.Id}/archive", new { });
        await CreateProductAsync(staff, "New Biscuit", "BISCUIT-1");

        var list = await staff.GetFromJsonAsync<BackOfficeList>($"/api/catalog/back-office/products{filter}", Cancel);

        Assert.Equal(expected, Assert.Single(list!.Items).Name);
    }

    [Fact]
    public async Task ListAllProducts_FilteredByBrand_ReturnsThatBrandsProducts()
    {
        var tenant = await api.CreateTenantAsync("bo-brand");
        using var staff = await OwnerClientAsync(tenant);
        var brand = await PostAsync<BrandItem>(staff, "/api/catalog/brands", new { name = "Pran" });
        var juice = await CreatePublishedAsync(staff, "Mango Juice", "JUICE-1", brand.Id);
        await CreateProductAsync(staff, "New Biscuit", "BISCUIT-1");

        var list = await staff.GetFromJsonAsync<BackOfficeList>($"/api/catalog/back-office/products?brandId={brand.Id}", Cancel);

        var row = Assert.Single(list!.Items);
        Assert.Equal((juice.Id, (Guid?)brand.Id), (row.Id, row.BrandId));
    }

    [Theory]
    [InlineData("?status=Sold")]
    [InlineData("?pageSize=500")]
    public async Task ListAllProducts_InvalidQuery_ReturnsValidationProblem(string query)
    {
        var tenant = await api.CreateTenantAsync("bo-400");
        using var staff = await OwnerClientAsync(tenant);

        var response = await staff.GetAsync($"/api/catalog/back-office/products{query}", Cancel);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task ListAllProducts_ByShopper_ReturnsUnauthorized()
    {
        var tenant = await api.CreateTenantAsync("bo-anon");
        using var shopper = api.CreateTenantClient(tenant.Identifier);

        var response = await shopper.GetAsync("/api/catalog/back-office/products", Cancel);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    private static async Task<ProductResponse> CreatePublishedAsync(HttpClient staff, string name, string sku, Guid? brandId)
    {
        var product = await PostAsync<ProductResponse>(staff, "/api/catalog/products", new
        {
            name,
            brandId,
            variants = new[] { new { sku, name = string.Empty, price = 50m } },
        });
        (await staff.PostAsync($"/api/catalog/products/{product.Id}/publish", null, Cancel)).EnsureSuccessStatusCode();
        return product;
    }

    private sealed record BrandItem(Guid Id, string Name, string Slug);

    private sealed record BrandRef(string Name, string Slug);

    private sealed record BrandedPage(Guid Id, string Name, BrandRef? Brand);

    private sealed record BrandedCard(Guid Id, string Name, string? Brand);

    private sealed record BrandedList(IReadOnlyList<BrandedCard> Items, int TotalCount);

    private sealed record BackOfficeRow(Guid Id, string Name, string Status, Guid? BrandId, int VariantCount, decimal? PriceFrom);

    private sealed record BackOfficeList(IReadOnlyList<BackOfficeRow> Items, int TotalCount);
}
