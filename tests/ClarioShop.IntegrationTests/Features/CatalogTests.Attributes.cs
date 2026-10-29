using System.Net;
using System.Net.Http.Json;
using ClarioShop.IntegrationTests.Infrastructure;

namespace ClarioShop.IntegrationTests.Features;

/// <summary>Variant attributes (Size, Colour …), the values variants pick, and how the storefront shows them.</summary>
public sealed partial class CatalogTests
{
    [Fact]
    public async Task Attributes_CreateAddAndRenameValues_ListsValuesInStaffOrder()
    {
        var tenant = await api.CreateTenantAsync("attr-crud");
        using var staff = await OwnerClientAsync(tenant);
        var size = await PostAsync<AttributeItem>(staff, "/api/catalog/attributes", new { name = "Size", values = new[] { "S", "M", "L" } });
        await PostAsync<AttributeItem>(staff, "/api/catalog/attributes", new { name = "Colour" });

        await PostAsync<AttributeItem>(staff, $"/api/catalog/attributes/{size.Id}/values", new { value = "XL" });
        var medium = size.Values.Single(v => v.Value == "M");
        await PutAsync<AttributeItem>(staff, $"/api/catalog/attributes/{size.Id}/values/{medium.Id}", new { value = "Medium" });
        await PutAsync<AttributeItem>(staff, $"/api/catalog/attributes/{size.Id}", new { name = "Shirt Size" });

        var attributes = (await staff.GetFromJsonAsync<List<AttributeItem>>("/api/catalog/attributes", Cancel))!;
        Assert.Equal(["Colour", "Shirt Size"], attributes.Select(a => a.Name));
        Assert.Equal(["S", "Medium", "L", "XL"], attributes[1].Values.Select(v => v.Value));
        Assert.Equal(medium.Id, attributes[1].Values[1].Id);
    }

    [Fact]
    public async Task Attributes_DuplicateNamesAndValues_AreRefused()
    {
        var tenant = await api.CreateTenantAsync("attr-dup");
        using var staff = await OwnerClientAsync(tenant);
        var size = await PostAsync<AttributeItem>(staff, "/api/catalog/attributes", new { name = "Size", values = new[] { "S", "M" } });

        var sameName = await staff.PostAsJsonAsync("/api/catalog/attributes", new { name = " SIZE " }, Cancel);
        var sameValue = await staff.PostAsJsonAsync($"/api/catalog/attributes/{size.Id}/values", new { value = "s" }, Cancel);
        var renamedOntoOther = await staff.PutAsJsonAsync(
            $"/api/catalog/attributes/{size.Id}/values/{size.Values[1].Id}", new { value = "S" }, Cancel);
        var twiceInRequest = await staff.PostAsJsonAsync("/api/catalog/attributes", new { name = "Colour", values = new[] { "Red", "red" } }, Cancel);
        var unknown = await staff.PostAsJsonAsync($"/api/catalog/attributes/{Guid.CreateVersion7()}/values", new { value = "XL" }, Cancel);

        Assert.Equal(
            (HttpStatusCode.Conflict, HttpStatusCode.Conflict, HttpStatusCode.Conflict, HttpStatusCode.BadRequest, HttpStatusCode.NotFound),
            (sameName.StatusCode, sameValue.StatusCode, renamedOntoOther.StatusCode, twiceInRequest.StatusCode, unknown.StatusCode));
    }

    [Fact]
    public async Task CreateAttribute_SameNameConcurrently_CreatesOneAndRefusesTheRest()
    {
        var tenant = await api.CreateTenantAsync("attr-race");
        using var staff = await OwnerClientAsync(tenant);

        var responses = await Task.WhenAll(Enumerable.Range(0, 6).Select(i =>
            staff.PostAsJsonAsync("/api/catalog/attributes", new { name = i % 2 == 0 ? "Size" : "SIZE", values = new[] { "S" } }, Cancel)));

        Assert.Equal(1, responses.Count(r => r.StatusCode == HttpStatusCode.Created));
        Assert.All(responses.Where(r => r.StatusCode != HttpStatusCode.Created), r => Assert.Equal(HttpStatusCode.Conflict, r.StatusCode));
    }

    [Fact]
    public async Task AddAttributeValue_SameValueConcurrently_AddsOneAndRefusesTheRest()
    {
        var tenant = await api.CreateTenantAsync("attr-value-race");
        using var staff = await OwnerClientAsync(tenant);
        var size = await PostAsync<AttributeItem>(staff, "/api/catalog/attributes", new { name = "Size", values = new[] { "S" } });

        var responses = await Task.WhenAll(Enumerable.Range(0, 6).Select(i =>
            staff.PostAsJsonAsync($"/api/catalog/attributes/{size.Id}/values", new { value = i % 2 == 0 ? "XL" : "xl" }, Cancel)));

        Assert.Equal(1, responses.Count(r => r.StatusCode == HttpStatusCode.Created));
        Assert.All(responses.Where(r => r.StatusCode != HttpStatusCode.Created), r => Assert.Equal(HttpStatusCode.Conflict, r.StatusCode));
    }

    [Fact]
    public async Task ProductPage_VariantsWithAttributes_ShowsOptionsAndEachVariantsValues()
    {
        var tenant = await api.CreateTenantAsync("attr-page");
        using var staff = await OwnerClientAsync(tenant);
        using var shopper = api.CreateTenantClient(tenant.Identifier);
        var size = await PostAsync<AttributeItem>(staff, "/api/catalog/attributes", new { name = "Size", values = new[] { "S", "M", "L" } });
        var colour = await PostAsync<AttributeItem>(staff, "/api/catalog/attributes", new { name = "Colour", values = new[] { "Red", "Blue" } });
        var (s, m, l) = (size.Values[0].Id, size.Values[1].Id, size.Values[2].Id);
        var (red, blue) = (colour.Values[0].Id, colour.Values[1].Id);

        var product = await PostAsync<ProductResponse>(staff, "/api/catalog/products", new
        {
            name = "Cotton Polo",
            variants = new object[]
            {
                new { sku = "POLO-M-RED", price = 650m, attributeValueIds = new[] { m, red } },
                new { sku = "POLO-S-BLUE", price = 600m, attributeValueIds = new[] { blue, s } },
                new { sku = "POLO-L-RED", price = 700m, attributeValueIds = new[] { l, red } },
            },
        });
        var details = await staff.GetFromJsonAsync<AttributedDetails>($"/api/catalog/products/{product.Id}", Cancel);
        var large = details!.Variants.Single(v => v.Sku == "POLO-L-RED");
        await PutAsync<AttributedVariant>(staff, $"/api/catalog/products/{product.Id}/variants/{large.Id}", new { isActive = false, attributeValueIds = large.AttributeValueIds });
        (await staff.PostAsync($"/api/catalog/products/{product.Id}/publish", null, Cancel)).EnsureSuccessStatusCode();
        await PutAsync<AttributeItem>(staff, $"/api/catalog/attributes/{colour.Id}/values/{red}", new { value = "Crimson" });

        var page = await shopper.GetFromJsonAsync<AttributedPage>($"/api/catalog/products/{product.Slug}", Cancel);

        Assert.Equal(
            ["Colour: Crimson, Blue", "Size: S, M"],
            page!.Options.Select(o => $"{o.Name}: {string.Join(", ", o.Values)}"));
        Assert.Equal(
            ["POLO-S-BLUE Colour=Blue Size=S", "POLO-M-RED Colour=Crimson Size=M"],
            page.Variants.Select(v => $"{v.Sku} {string.Join(' ', v.Attributes.Select(a => $"{a.Name}={a.Value}"))}"));
    }

    [Fact]
    public async Task ProductPage_NoAttributes_HasNoOptions()
    {
        var tenant = await api.CreateTenantAsync("attr-none");
        using var staff = await OwnerClientAsync(tenant);
        using var shopper = api.CreateTenantClient(tenant.Identifier);
        var product = await CreatePublishedAsync(staff, "Iodized Salt", "SALT-1", brandId: null);

        var page = await shopper.GetFromJsonAsync<AttributedPage>($"/api/catalog/products/{product.Slug}", Cancel);

        Assert.Empty(page!.Options);
        Assert.Empty(Assert.Single(page.Variants).Attributes);
    }

    [Fact]
    public async Task UpdateVariant_ReplacesAttributeValues()
    {
        var tenant = await api.CreateTenantAsync("attr-update");
        using var staff = await OwnerClientAsync(tenant);
        var size = await PostAsync<AttributeItem>(staff, "/api/catalog/attributes", new { name = "Size", values = new[] { "S", "M" } });
        var colour = await PostAsync<AttributeItem>(staff, "/api/catalog/attributes", new { name = "Colour", values = new[] { "Red" } });
        var product = await PostAsync<ProductResponse>(staff, "/api/catalog/products", new
        {
            name = "Cotton Polo",
            variants = new[] { new { sku = "POLO-1", price = 650m, attributeValueIds = new[] { size.Values[0].Id, colour.Values[0].Id } } },
        });
        var variantId = await FirstVariantIdAsync(staff, product.Id);

        var updated = await PutAsync<AttributedVariant>(
            staff, $"/api/catalog/products/{product.Id}/variants/{variantId}", new { isActive = true, attributeValueIds = new[] { size.Values[1].Id } });

        Assert.Equal([size.Values[1].Id], updated.AttributeValueIds);
        var details = await staff.GetFromJsonAsync<AttributedDetails>($"/api/catalog/products/{product.Id}", Cancel);
        Assert.Equal([size.Values[1].Id], Assert.Single(details!.Variants).AttributeValueIds);
    }

    [Fact]
    public async Task AddVariant_SameValuesAsAnotherVariant_ReturnsValidationProblem()
    {
        var tenant = await api.CreateTenantAsync("attr-same");
        using var staff = await OwnerClientAsync(tenant);
        var size = await PostAsync<AttributeItem>(staff, "/api/catalog/attributes", new { name = "Size", values = new[] { "S" } });
        var product = await PostAsync<ProductResponse>(staff, "/api/catalog/products", new
        {
            name = "Cotton Polo",
            variants = new[] { new { sku = "POLO-S", price = 650m, attributeValueIds = new[] { size.Values[0].Id } } },
        });

        var response = await staff.PostAsJsonAsync(
            $"/api/catalog/products/{product.Id}/variants", new { sku = "POLO-S-2", price = 650m, attributeValueIds = new[] { size.Values[0].Id } }, Cancel);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task CreateProduct_BadAttributeValues_ReturnsValidationProblem()
    {
        var tenant = await api.CreateTenantAsync("attr-bad");
        using var staff = await OwnerClientAsync(tenant);
        var size = await PostAsync<AttributeItem>(staff, "/api/catalog/attributes", new { name = "Size", values = new[] { "S", "M" } });

        var twoSizes = await staff.PostAsJsonAsync("/api/catalog/products", new
        {
            name = "Cotton Polo",
            variants = new[] { new { sku = "POLO-1", price = 650m, attributeValueIds = new[] { size.Values[0].Id, size.Values[1].Id } } },
        }, Cancel);
        var unknownValue = await staff.PostAsJsonAsync("/api/catalog/products", new
        {
            name = "Cotton Polo",
            variants = new[] { new { sku = "POLO-2", price = 650m, attributeValueIds = new[] { Guid.CreateVersion7() } } },
        }, Cancel);

        Assert.Equal((HttpStatusCode.BadRequest, HttpStatusCode.BadRequest), (twoSizes.StatusCode, unknownValue.StatusCode));
    }

    [Fact]
    public async Task Attributes_ByShopper_AreUnauthorized()
    {
        var tenant = await api.CreateTenantAsync("attr-anon");
        using var shopper = api.CreateTenantClient(tenant.Identifier);

        var create = await shopper.PostAsJsonAsync("/api/catalog/attributes", new { name = "Size" }, Cancel);
        var list = await shopper.GetAsync("/api/catalog/attributes", Cancel);

        Assert.Equal((HttpStatusCode.Unauthorized, HttpStatusCode.Unauthorized), (create.StatusCode, list.StatusCode));
    }

    private sealed record AttributeItem(Guid Id, string Name, IReadOnlyList<AttributeValueItem> Values);

    private sealed record AttributeValueItem(Guid Id, string Value);

    private sealed record AttributedVariant(Guid Id, string Sku, bool IsActive, IReadOnlyList<Guid> AttributeValueIds);

    private sealed record AttributedDetails(Guid Id, IReadOnlyList<AttributedVariant> Variants);

    private sealed record OptionItem(string Name, IReadOnlyList<string> Values);

    private sealed record VariantAttribute(string Name, string Value);

    private sealed record PageVariant(Guid Id, string Sku, IReadOnlyList<VariantAttribute> Attributes);

    private sealed record AttributedPage(Guid Id, IReadOnlyList<OptionItem> Options, IReadOnlyList<PageVariant> Variants);
}
