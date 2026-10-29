using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using ClarioShop.Catalog.Contracts.Events;
using ClarioShop.IntegrationTests.Infrastructure;

namespace ClarioShop.IntegrationTests.Features;

/// <summary>Editing and archiving products, adding and changing variants, and audited price changes.</summary>
public sealed partial class CatalogTests
{
    [Fact]
    public async Task UpdateProduct_RenamesAndKeepsSlug()
    {
        var tenant = await api.CreateTenantAsync("edit");
        using var staff = await OwnerClientAsync(tenant);
        var product = await CreateProductAsync(staff, "Pran Mango Juice", "JUICE-MANGO-250");
        var brand = await PostAsync<BrandItem>(staff, "/api/catalog/brands", new { name = "Pran" });

        var updated = await PutAsync<ProductDetails>(staff, $"/api/catalog/products/{product.Id}", new
        {
            name = "Pran Mango Juice Drink",
            description = "Made from ripe mangoes.",
            brandId = brand.Id,
        });

        Assert.Equal(("Pran Mango Juice Drink", "Made from ripe mangoes.", (Guid?)brand.Id), (updated.Name, updated.Description, updated.BrandId));
        Assert.Equal(product.Slug, updated.Slug);
        var loaded = await staff.GetFromJsonAsync<ProductDetails>($"/api/catalog/products/{product.Id}", Cancel);
        Assert.Equal(("Pran Mango Juice Drink", "Draft"), (loaded!.Name, loaded.Status));
    }

    [Fact]
    public async Task GetProduct_ByShopper_ReturnsUnauthorized()
    {
        var tenant = await api.CreateTenantAsync("get-anon");
        using var staff = await OwnerClientAsync(tenant);
        using var shopper = api.CreateTenantClient(tenant.Identifier);
        var product = await CreateProductAsync(staff, "Draft Biscuit", "BISCUIT-1");

        var response = await shopper.GetAsync($"/api/catalog/products/{product.Id}", Cancel);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task GetProduct_Unknown_ReturnsNotFound()
    {
        var tenant = await api.CreateTenantAsync("get-404");
        using var staff = await OwnerClientAsync(tenant);

        var response = await staff.GetAsync($"/api/catalog/products/{Guid.CreateVersion7()}", Cancel);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task ArchiveProduct_Published_LeavesStorefrontAndPublishesEvent()
    {
        var tenant = await api.CreateTenantAsync("archive");
        using var staff = await OwnerClientAsync(tenant);
        using var shopper = api.CreateTenantClient(tenant.Identifier);
        var product = await CreateProductAsync(staff, "Old Soap", "SOAP-OLD");
        (await staff.PostAsync($"/api/catalog/products/{product.Id}/publish", null, Cancel)).EnsureSuccessStatusCode();

        var archived = await PostAsync<ProductDetails>(staff, $"/api/catalog/products/{product.Id}/archive", new { });

        Assert.Equal("Archived", archived.Status);
        Assert.NotNull(archived.ArchivedAt);
        Assert.Equal(HttpStatusCode.NotFound, (await shopper.GetAsync($"/api/catalog/products/{product.Slug}", Cancel)).StatusCode);
        var list = await shopper.GetFromJsonAsync<ProductList>("/api/catalog/products", Cancel);
        Assert.Empty(list!.Items);
        await api.Events.WaitForAsync<ProductArchived>(e => e.ProductId == product.Id, TimeSpan.FromSeconds(15));
    }

    [Fact]
    public async Task ArchiveProduct_ThenEdit_ReturnsConflict()
    {
        var tenant = await api.CreateTenantAsync("archive-409");
        using var staff = await OwnerClientAsync(tenant);
        var product = await CreateProductAsync(staff, "Old Soap", "SOAP-OLD");
        await PostAsync<ProductDetails>(staff, $"/api/catalog/products/{product.Id}/archive", new { });
        var variantId = await FirstVariantIdAsync(staff, product.Id);

        var edit = await staff.PutAsJsonAsync($"/api/catalog/products/{product.Id}", new { name = "New Soap" }, Cancel);
        var price = await staff.PutAsJsonAsync($"/api/catalog/products/{product.Id}/variants/{variantId}/price", new { price = 50m }, Cancel);
        var archiveAgain = await staff.PostAsync($"/api/catalog/products/{product.Id}/archive", null, Cancel);

        Assert.Equal(
            (HttpStatusCode.Conflict, HttpStatusCode.Conflict, HttpStatusCode.Conflict),
            (edit.StatusCode, price.StatusCode, archiveAgain.StatusCode));
    }

    [Fact]
    public async Task AddVariant_InventoryStartsTrackingItWithZeroStock()
    {
        var tenant = await api.CreateTenantAsync("add-var");
        using var staff = await OwnerClientAsync(tenant);
        var product = await CreateProductAsync(staff, "Pran Mango Juice", "JUICE-MANGO-250");

        var added = await PostAsync<VariantDetails>(staff, $"/api/catalog/products/{product.Id}/variants", new
        {
            sku = "juice-mango-1l",
            name = "1 L",
            price = 120m,
            barcodes = new[] { "8941100500999" },
        });

        Assert.Equal(("JUICE-MANGO-1L", true), (added.Sku, added.IsActive));
        var received = await api.Events.WaitForAsync<VariantAdded>(e => e.VariantId == added.Id, TimeSpan.FromSeconds(15));
        Assert.Equal(("Pran Mango Juice", "JUICE-MANGO-1L", 120m), (received.ProductName, received.Sku, received.Price));
        var stock = await WaitForStockAsync(staff, added.Id);
        Assert.Equal(("JUICE-MANGO-1L", "Pran Mango Juice", 0m), (stock.Sku, stock.ProductName, stock.OnHand));
    }

    [Fact]
    public async Task AddVariant_SkuOfAnotherProduct_ReturnsConflict()
    {
        var tenant = await api.CreateTenantAsync("add-var-409");
        using var staff = await OwnerClientAsync(tenant);
        await CreateProductAsync(staff, "Sugar", "SUGAR-1KG");
        var product = await CreateProductAsync(staff, "Salt", "SALT-1KG");

        var response = await staff.PostAsJsonAsync($"/api/catalog/products/{product.Id}/variants", new { sku = "sugar-1kg", price = 50m }, Cancel);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task UpdateVariant_RenamesReplacesBarcodesAndTakesOffSale()
    {
        var tenant = await api.CreateTenantAsync("upd-var");
        using var staff = await OwnerClientAsync(tenant);
        using var shopper = api.CreateTenantClient(tenant.Identifier);
        var product = await CreateProductAsync(staff, "Chinigura Rice", "RICE-1KG");
        var second = await PostAsync<VariantDetails>(staff, $"/api/catalog/products/{product.Id}/variants", new { sku = "RICE-5KG", name = "5 kg", price = 790m });
        (await staff.PostAsync($"/api/catalog/products/{product.Id}/publish", null, Cancel)).EnsureSuccessStatusCode();

        var updated = await PutAsync<VariantDetails>(staff, $"/api/catalog/products/{product.Id}/variants/{second.Id}", new
        {
            name = "5 kg bag",
            barcodes = new[] { "111", "222" },
            isActive = false,
        });

        Assert.Equal(("5 kg bag", false), (updated.Name, updated.IsActive));
        Assert.Equal(["111", "222"], updated.Barcodes);
        var page = await shopper.GetFromJsonAsync<ProductPage>($"/api/catalog/products/{product.Slug}", Cancel);
        Assert.Equal(["RICE-1KG"], page!.Variants.Select(v => v.Sku));
    }

    [Fact]
    public async Task UpdateVariant_LastActiveVariantOfPublishedProduct_ReturnsValidationProblem()
    {
        var tenant = await api.CreateTenantAsync("upd-var-400");
        using var staff = await OwnerClientAsync(tenant);
        var product = await CreateProductAsync(staff, "Chinigura Rice", "RICE-1KG");
        (await staff.PostAsync($"/api/catalog/products/{product.Id}/publish", null, Cancel)).EnsureSuccessStatusCode();
        var variantId = await FirstVariantIdAsync(staff, product.Id);

        var response = await staff.PutAsJsonAsync($"/api/catalog/products/{product.Id}/variants/{variantId}", new { name = "1 kg", isActive = false }, Cancel);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task ChangePrice_AuditsBeforeAfterAndPublishesEvent()
    {
        var tenant = await api.CreateTenantAsync("price");
        using var staff = await OwnerClientAsync(tenant);
        staff.DefaultRequestHeaders.Add("X-Device-Id", "backoffice-01");
        using var shopper = api.CreateTenantClient(tenant.Identifier);
        var product = await CreateProductAsync(staff, "Chinigura Rice", "RICE-1KG", price: 165m);
        (await staff.PostAsync($"/api/catalog/products/{product.Id}/publish", null, Cancel)).EnsureSuccessStatusCode();
        var variantId = await FirstVariantIdAsync(staff, product.Id);

        var changed = await PutAsync<VariantDetails>(staff, $"/api/catalog/products/{product.Id}/variants/{variantId}/price", new { price = 159.5m, compareAtPrice = 175m });

        Assert.Equal((159.5m, 175m), (changed.Price, changed.CompareAtPrice));
        var page = await shopper.GetFromJsonAsync<ProductPage>($"/api/catalog/products/{product.Slug}", Cancel);
        Assert.Equal(159.5m, Assert.Single(page!.Variants).Price);

        var log = await staff.GetFromJsonAsync<AuditPage>($"/api/catalog/audit-log?entityId={variantId}", Cancel);
        var entry = Assert.Single(log!.Items);
        Assert.Equal(("catalog.price.changed", "Variant", "backoffice-01"), (entry.Action, entry.EntityType, entry.Device));
        Assert.NotNull(entry.UserId);
        Assert.Equal(165m, entry.Before!.Value.GetProperty("price").GetDecimal());
        Assert.Equal(JsonValueKind.Null, entry.Before.Value.GetProperty("compareAtPrice").ValueKind);
        Assert.Equal((159.5m, 175m), (entry.After!.Value.GetProperty("price").GetDecimal(), entry.After.Value.GetProperty("compareAtPrice").GetDecimal()));

        var received = await api.Events.WaitForAsync<ProductPriceChanged>(e => e.VariantId == variantId, TimeSpan.FromSeconds(15));
        Assert.Equal((product.Id, "RICE-1KG", 165m, 159.5m), (received.ProductId, received.Sku, received.OldPrice, received.NewPrice));
    }

    [Fact]
    public async Task ChangePrice_SamePrice_WritesNoAuditRow()
    {
        var tenant = await api.CreateTenantAsync("price-same");
        using var staff = await OwnerClientAsync(tenant);
        var product = await CreateProductAsync(staff, "Chinigura Rice", "RICE-1KG", price: 165m);
        var variantId = await FirstVariantIdAsync(staff, product.Id);

        await PutAsync<VariantDetails>(staff, $"/api/catalog/products/{product.Id}/variants/{variantId}/price", new { price = 165.00m });

        var log = await staff.GetFromJsonAsync<AuditPage>($"/api/catalog/audit-log?entityId={variantId}", Cancel);
        Assert.Empty(log!.Items);
    }

    [Fact]
    public async Task ChangePrice_ConcurrentChanges_EachAuditRowStartsFromThePreviousPrice()
    {
        var tenant = await api.CreateTenantAsync("price-race");
        using var staff = await OwnerClientAsync(tenant);
        var product = await CreateProductAsync(staff, "Chinigura Rice", "RICE-1KG", price: 100m);
        var variantId = await FirstVariantIdAsync(staff, product.Id);

        var responses = await Task.WhenAll(Enumerable.Range(1, 8).Select(i =>
            staff.PutAsJsonAsync($"/api/catalog/products/{product.Id}/variants/{variantId}/price", new { price = 100m + i }, Cancel)));

        Assert.All(responses, r => Assert.Contains(r.StatusCode, new[] { HttpStatusCode.OK, HttpStatusCode.Conflict }));
        var log = await staff.GetFromJsonAsync<AuditPage>($"/api/catalog/audit-log?entityId={variantId}&pageSize=200", Cancel);
        var chain = log!.Items.Reverse().Select(e => (Before: e.Before!.Value.GetProperty("price").GetDecimal(), After: e.After!.Value.GetProperty("price").GetDecimal())).ToList();
        Assert.Equal(responses.Count(r => r.StatusCode == HttpStatusCode.OK), chain.Count);
        Assert.Equal(100m, chain[0].Before);
        for (var i = 1; i < chain.Count; i++)
        {
            Assert.Equal(chain[i - 1].After, chain[i].Before);
        }

        var final = await staff.GetFromJsonAsync<ProductDetails>($"/api/catalog/products/{product.Id}", Cancel);
        Assert.Equal(chain[^1].After, Assert.Single(final!.Variants).Price);
    }

    [Fact]
    public async Task AuditLog_OwnerCanRead_CatalogManagerCannot()
    {
        var tenant = await api.CreateTenantAsync("cat-audit");
        using var owner = await OwnerClientAsync(tenant);
        (await owner.PostAsJsonAsync("/api/identity/users", new
        {
            email = "catalog@shop.test",
            fullName = "Catalog Person",
            password = "Manager-pass-1",
            roles = new[] { "Catalog Manager" },
        }, Cancel)).EnsureSuccessStatusCode();
        using var manager = api.CreateTenantClient(tenant.Identifier, await api.SignInAsync(tenant.Identifier, "catalog@shop.test", "Manager-pass-1"));

        var byOwner = await owner.GetAsync("/api/catalog/audit-log", Cancel);
        var byManager = await manager.GetAsync("/api/catalog/audit-log", Cancel);

        Assert.Equal((HttpStatusCode.OK, HttpStatusCode.Forbidden), (byOwner.StatusCode, byManager.StatusCode));
    }

    private static async Task<ProductResponse> CreateProductAsync(HttpClient staff, string name, string sku, decimal price = 50m) =>
        await PostAsync<ProductResponse>(staff, "/api/catalog/products", new { name, variants = new[] { new { sku, name = string.Empty, price } } });

    private static async Task<Guid> FirstVariantIdAsync(HttpClient staff, Guid productId) =>
        (await staff.GetFromJsonAsync<ProductDetails>($"/api/catalog/products/{productId}", Cancel))!.Variants[0].Id;

    private static async Task<T> PutAsync<T>(HttpClient client, string url, object body)
    {
        var response = await client.PutAsJsonAsync(url, body, Cancel);
        await response.EnsureSuccessWithBodyAsync();
        return (await response.Content.ReadFromJsonAsync<T>(Cancel))!;
    }

    private static async Task<StockRow> WaitForStockAsync(HttpClient staff, Guid variantId)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(Cancel);
        timeout.CancelAfter(TimeSpan.FromSeconds(15));
        while (true)
        {
            var page = await staff.GetFromJsonAsync<StockPage>($"/api/inventory/stock?variantId={variantId}", timeout.Token);
            if (page!.Items.Count > 0)
            {
                return Assert.Single(page.Items);
            }

            await Task.Delay(TimeSpan.FromMilliseconds(100), timeout.Token);
        }
    }

    private sealed record ProductDetails(
        Guid Id,
        string Name,
        string Slug,
        string Description,
        Guid? BrandId,
        string Status,
        DateTimeOffset? ArchivedAt,
        IReadOnlyList<VariantDetails> Variants);

    private sealed record VariantDetails(Guid Id, string Sku, string Name, decimal Price, decimal? CompareAtPrice, IReadOnlyList<string> Barcodes, bool IsActive);

    private sealed record StockRow(Guid VariantId, string Sku, string ProductName, decimal OnHand);

    private sealed record StockPage(IReadOnlyList<StockRow> Items);

    private sealed record AuditItem(string Action, string EntityType, Guid EntityId, JsonElement? Before, JsonElement? After, Guid? UserId, string? Device);

    private sealed record AuditPage(IReadOnlyList<AuditItem> Items, int TotalCount);
}
