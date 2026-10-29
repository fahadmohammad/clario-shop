using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using ClarioShop.IntegrationTests.Infrastructure;

namespace ClarioShop.IntegrationTests.Features;

/// <summary>Product images: upload, gallery order, main image, removal and serving.</summary>
public sealed partial class CatalogTests
{
    private static readonly byte[] PngHeader = [0x89, (byte)'P', (byte)'N', (byte)'G', 0x0D, 0x0A, 0x1A, 0x0A];

    [Fact]
    public async Task UploadImages_ProductPageShowsGalleryAndCardShowsMainImage()
    {
        var tenant = await api.CreateTenantAsync("img-flow");
        using var staff = await OwnerClientAsync(tenant);
        using var shopper = api.CreateTenantClient(tenant.Identifier);
        var product = await PostAsync<ProductResponse>(staff, "/api/catalog/products", new
        {
            name = "Hilsa Fish",
            variants = new[] { new { sku = "HILSA-1KG", name = "1 kg", price = 1400m } },
        });

        var front = await UploadAsync(staff, product.Id, Png(1), "Whole hilsa");
        var side = await UploadAsync(staff, product.Id, Png(2), null);
        (await staff.PostAsync($"/api/catalog/products/{product.Id}/publish", null, Cancel)).EnsureSuccessStatusCode();

        var page = await shopper.GetFromJsonAsync<ImagePage>($"/api/catalog/products/{product.Slug}", Cancel);
        var list = await shopper.GetFromJsonAsync<ImageList>("/api/catalog/products", Cancel);
        Assert.Equal([new ImageRef(front.Url, "Whole hilsa"), new ImageRef(side.Url, string.Empty)], page!.Images);
        Assert.Equal(front.Url, Assert.Single(list!.Items).ImageUrl);
        Assert.Equal(("image/png", Png(1).Length), (front.ContentType, front.SizeBytes));
    }

    [Fact]
    public async Task GetImage_Anonymous_ServesTheUploadedBytesForCaching()
    {
        var tenant = await api.CreateTenantAsync("img-serve");
        using var staff = await OwnerClientAsync(tenant);
        using var shopper = api.CreateTenantClient(tenant.Identifier);
        var product = await CreateDraftAsync(staff, "IMG-SERVE");
        var image = await UploadAsync(staff, product.Id, Png(3), null, contentType: "application/octet-stream");

        var response = await shopper.GetAsync(image.Url, Cancel);

        await response.EnsureSuccessWithBodyAsync();
        Assert.Equal("image/png", response.Content.Headers.ContentType?.MediaType);
        Assert.Equal(Png(3), await response.Content.ReadAsByteArrayAsync(Cancel));
        Assert.True(response.Headers.CacheControl is { Public: true, MaxAge: not null });
    }

    [Fact]
    public async Task GetImage_FromAnotherShop_ReturnsNotFound()
    {
        var shopA = await api.CreateTenantAsync("img-iso-a");
        var shopB = await api.CreateTenantAsync("img-iso-b");
        using var staffA = await OwnerClientAsync(shopA);
        var product = await CreateDraftAsync(staffA, "IMG-ISO");
        var image = await UploadAsync(staffA, product.Id, Png(4), null);
        using var shopperB = api.CreateTenantClient(shopB.Identifier);

        var response = await shopperB.GetAsync(image.Url, Cancel);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Theory]
    [InlineData("svg")]
    [InlineData("empty")]
    [InlineData("too-large")]
    [InlineData("missing")]
    public async Task UploadImage_NotAnAcceptableImage_ReturnsBadRequest(string kind)
    {
        var tenant = await api.CreateTenantAsync("img-bad");
        using var staff = await OwnerClientAsync(tenant);
        var product = await CreateDraftAsync(staff, "IMG-BAD");
        using var form = new MultipartFormDataContent();
        byte[]? bytes = kind switch
        {
            "svg" => "<svg xmlns=\"http://www.w3.org/2000/svg\"><script>alert(1)</script></svg>"u8.ToArray(),
            "empty" => [],
            "too-large" => [.. PngHeader, .. new byte[(5 * 1024 * 1024) + 1]],
            _ => null,
        };
        if (bytes is not null)
        {
            var file = new ByteArrayContent(bytes);
            file.Headers.ContentType = new MediaTypeHeaderValue("image/png");
            form.Add(file, "file", "photo.png");
        }
        else
        {
            form.Add(new StringContent("No file here"), "altText");
        }

        var response = await staff.PostAsync($"/api/catalog/products/{product.Id}/images", form, Cancel);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        Assert.Empty((await GetDetailsAsync(staff, product.Id)).Images);
    }

    [Fact]
    public async Task UploadImage_ArchivedProduct_ReturnsConflict()
    {
        var tenant = await api.CreateTenantAsync("img-archived");
        using var staff = await OwnerClientAsync(tenant);
        var product = await CreateDraftAsync(staff, "IMG-ARCH");
        (await staff.PostAsync($"/api/catalog/products/{product.Id}/archive", null, Cancel)).EnsureSuccessStatusCode();

        var response = await staff.PostAsync($"/api/catalog/products/{product.Id}/images", ImageForm(Png(5), null), Cancel);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task UploadImage_WithoutToken_ReturnsUnauthorized()
    {
        var tenant = await api.CreateTenantAsync("img-anon");
        using var staff = await OwnerClientAsync(tenant);
        using var shopper = api.CreateTenantClient(tenant.Identifier);
        var product = await CreateDraftAsync(staff, "IMG-ANON");

        var response = await shopper.PostAsync($"/api/catalog/products/{product.Id}/images", ImageForm(Png(6), null), Cancel);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task UploadImages_Concurrently_KeepsEveryImage()
    {
        var tenant = await api.CreateTenantAsync("img-race");
        using var staff = await OwnerClientAsync(tenant);
        var product = await CreateDraftAsync(staff, "IMG-RACE");

        var responses = await Task.WhenAll(Enumerable.Range(10, 4).Select(i =>
            staff.PostAsync($"/api/catalog/products/{product.Id}/images", ImageForm(Png(i), null), Cancel)));

        Assert.All(responses, response => Assert.Equal(HttpStatusCode.Created, response.StatusCode));
        Assert.Equal(4, (await GetDetailsAsync(staff, product.Id)).Images.Count);
    }

    [Fact]
    public async Task SetMainImage_MovesItFirstAndReorderSetsTheGallery()
    {
        var tenant = await api.CreateTenantAsync("img-order");
        using var staff = await OwnerClientAsync(tenant);
        var product = await CreateDraftAsync(staff, "IMG-ORDER");
        var first = await UploadAsync(staff, product.Id, Png(20), null);
        var second = await UploadAsync(staff, product.Id, Png(21), null);
        var third = await UploadAsync(staff, product.Id, Png(22), null);

        var afterMain = await PostAsync<List<ImageInfo>>(staff, $"/api/catalog/products/{product.Id}/images/{third.Id}/main", new { });
        var afterReorder = await PutAsync<List<ImageInfo>>(
            staff, $"/api/catalog/products/{product.Id}/images/order", new { imageIds = new[] { second.Id, third.Id, first.Id } });

        Assert.Equal([third.Id, first.Id, second.Id], afterMain.Select(i => i.Id));
        Assert.Equal([second.Id, third.Id, first.Id], afterReorder.Select(i => i.Id));
        Assert.Equal([second.Id, third.Id, first.Id], (await GetDetailsAsync(staff, product.Id)).Images.Select(i => i.Id));
    }

    [Fact]
    public async Task ReorderImages_MissingAnImage_ReturnsBadRequest()
    {
        var tenant = await api.CreateTenantAsync("img-order-bad");
        using var staff = await OwnerClientAsync(tenant);
        var product = await CreateDraftAsync(staff, "IMG-ORDER-BAD");
        var first = await UploadAsync(staff, product.Id, Png(30), null);
        await UploadAsync(staff, product.Id, Png(31), null);

        var response = await staff.PutAsJsonAsync($"/api/catalog/products/{product.Id}/images/order", new { imageIds = new[] { first.Id } }, Cancel);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task RemoveImage_DeletesItsFileAndNextBecomesMain()
    {
        var tenant = await api.CreateTenantAsync("img-remove");
        using var staff = await OwnerClientAsync(tenant);
        var product = await CreateDraftAsync(staff, "IMG-REMOVE");
        var first = await UploadAsync(staff, product.Id, Png(40), null);
        var second = await UploadAsync(staff, product.Id, Png(41), null);
        var filesBefore = Directory.GetFiles(api.FilesRoot, $"{first.Id:N}.*", SearchOption.AllDirectories);

        var response = await staff.DeleteAsync($"/api/catalog/products/{product.Id}/images/{first.Id}", Cancel);

        await response.EnsureSuccessWithBodyAsync();
        var left = (await response.Content.ReadFromJsonAsync<List<ImageInfo>>(Cancel))!;
        Assert.Equal([second.Id], left.Select(i => i.Id));
        Assert.Single(filesBefore);
        Assert.False(File.Exists(filesBefore[0]));
        Assert.Equal(HttpStatusCode.NotFound, (await staff.GetAsync(first.Url, Cancel)).StatusCode);
    }

    private async Task<ProductResponse> CreateDraftAsync(HttpClient staff, string sku) =>
        await PostAsync<ProductResponse>(staff, "/api/catalog/products", new
        {
            name = $"Product {sku}",
            variants = new[] { new { sku, name = string.Empty, price = 100m } },
        });

    private static async Task<ImageInfo> UploadAsync(HttpClient staff, Guid productId, byte[] bytes, string? altText, string contentType = "image/png")
    {
        var response = await staff.PostAsync($"/api/catalog/products/{productId}/images", ImageForm(bytes, altText, contentType), Cancel);
        await response.EnsureSuccessWithBodyAsync();
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<ImageInfo>(Cancel))!;
    }

    private static MultipartFormDataContent ImageForm(byte[] bytes, string? altText, string contentType = "image/png")
    {
        var file = new ByteArrayContent(bytes);
        file.Headers.ContentType = new MediaTypeHeaderValue(contentType);
        var form = new MultipartFormDataContent { { file, "file", "photo.png" } };
        if (altText is not null)
        {
            form.Add(new StringContent(altText), "altText");
        }

        return form;
    }

    /// <summary>A PNG signature followed by a marker byte, so each test image has its own content.</summary>
    private static byte[] Png(int marker) => [.. PngHeader, 0, 0, 0, 0x0D, (byte)marker];

    private static async Task<ImageGallery> GetDetailsAsync(HttpClient staff, Guid productId) =>
        (await staff.GetFromJsonAsync<ImageGallery>($"/api/catalog/products/{productId}", Cancel))!;

    private sealed record ImageInfo(Guid Id, string Url, string AltText, string ContentType, long SizeBytes);

    private sealed record ImageGallery(Guid Id, IReadOnlyList<ImageInfo> Images);

    private sealed record ImageRef(string Url, string AltText);

    private sealed record ImagePage(Guid Id, IReadOnlyList<ImageRef> Images);

    private sealed record ImageCard(Guid Id, string? ImageUrl);

    private sealed record ImageList(IReadOnlyList<ImageCard> Items);
}
