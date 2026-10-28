using System.Net;
using System.Net.Http.Json;
using ClarioShop.Catalog.Persistence;
using ClarioShop.IntegrationTests.Infrastructure;
using ClarioShop.Ordering.Contracts.Events;
using ClarioShop.Ordering.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace ClarioShop.IntegrationTests.Features;

[Collection(ApiCollection.Name)]
public sealed class OrderingTests(ClarioApiFactory api)
{
    private const string Phone = "01712-345678";

    private static CancellationToken Cancel => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Checkout_HappyPath_PlacesOrderReservesStockAndPublishesOrderPlaced()
    {
        var shop = await ShopAsync("chk-ok");
        var rice = await shop.StockedVariantAsync("Miniket Rice 5kg", "RICE-5KG", price: 450m, stock: 10);
        var oil = await shop.StockedVariantAsync("Rupchanda Oil 5L", "OIL-5L", price: 899.5m, stock: 10);
        using var shopper = api.CreateTenantClient(shop.Tenant.Identifier);
        var cartId = await NewCartAsync(shopper);
        await AddItemAsync(shopper, cartId, rice, 2);
        await AddItemAsync(shopper, cartId, oil, 1);

        var response = await CheckoutAsync(shopper, cartId, "+8801712345678");

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var order = (await response.Content.ReadFromJsonAsync<OrderResponse>(Cancel))!;
        Assert.Equal(("Placed", "CashOnDelivery", "+8801712345678", "Rahim Uddin"), (order.Status, order.PaymentMethod, order.CustomerPhone, order.CustomerName));
        Assert.Matches("^CS-[0-9]{6}$", order.Number);
        Assert.Equal(["RICE-5KG", "OIL-5L"], order.Lines.Select(line => line.Sku));
        Assert.Equal((450m, 2m, 900m), (order.Lines[0].UnitPrice, order.Lines[0].Quantity, order.Lines[0].LineTotal));
        Assert.Equal((1799.5m, 1799.5m), (order.Subtotal, order.Total));
        Assert.Equal("Placed", Assert.Single(order.History).ToStatus);
        Assert.Equal(response.Headers.Location?.ToString(), $"/api/ordering/orders/{order.Id}");
        Assert.Equal(2m, (await shop.StockAsync(rice)).Reserved);
        var placed = await api.Events.WaitForAsync<OrderPlaced>(e => e.OrderId == order.Id, TimeSpan.FromSeconds(15));
        Assert.Equal((order.Number, order.CustomerId, 1799.5m, 2), (placed.OrderNumber, placed.CustomerId, placed.Total, placed.Lines.Count));
    }

    [Fact]
    public async Task Checkout_StaffCanReadOrderButAnonymousCannot()
    {
        var shop = await ShopAsync("chk-read");
        var variant = await shop.StockedVariantAsync("Teer Sugar 1kg", "SUGAR-1KG", price: 125m, stock: 5);
        using var shopper = api.CreateTenantClient(shop.Tenant.Identifier);
        var order = await PlaceOrderAsync(shopper, variant, quantity: 1);

        var asStaff = await shop.Staff.GetFromJsonAsync<OrderResponse>($"/api/ordering/orders/{order.Id}", Cancel);
        var asShopper = await shopper.GetAsync($"/api/ordering/orders/{order.Id}", Cancel);

        Assert.Equal(order.Number, asStaff!.Number);
        Assert.Equal(HttpStatusCode.Unauthorized, asShopper.StatusCode);
    }

    [Fact]
    public async Task Checkout_OrderNumbersAreSequentialPerTenant()
    {
        var shop = await ShopAsync("chk-seq");
        var variant = await shop.StockedVariantAsync("Lux Soap", "SOAP-LUX", price: 45m, stock: 10);
        using var shopper = api.CreateTenantClient(shop.Tenant.Identifier);

        var first = await PlaceOrderAsync(shopper, variant, quantity: 1);
        var second = await PlaceOrderAsync(shopper, variant, quantity: 1);

        Assert.Equal(("CS-000001", "CS-000002"), (first.Number, second.Number));
    }

    [Fact]
    public async Task Checkout_OutOfStock_ReturnsConflictAndPersistsNothing()
    {
        var shop = await ShopAsync("chk-oos");
        var variant = await shop.StockedVariantAsync("Ispahani Tea", "TEA-400G", price: 250m, stock: 1);
        using var shopper = api.CreateTenantClient(shop.Tenant.Identifier);
        var cartId = await NewCartAsync(shopper);
        await AddItemAsync(shopper, cartId, variant, 2);

        var response = await CheckoutAsync(shopper, cartId);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal("inventory.insufficient_stock", await ProblemCodeAsync(response));
        Assert.Equal(0, await CountOrdersAsync(shop.Tenant));
        Assert.Equal(0m, (await shop.StockAsync(variant)).Reserved);
        Assert.Null((await GetCartAsync(shopper, cartId)).OrderId);
    }

    [Fact]
    public async Task Checkout_CustomersModuleRejectsPhone_ReleasesReservation()
    {
        var shop = await ShopAsync("chk-phone");
        var variant = await shop.StockedVariantAsync("Eggs (dozen)", "EGG-12", price: 140m, stock: 3);
        using var shopper = api.CreateTenantClient(shop.Tenant.Identifier);
        var cartId = await NewCartAsync(shopper);
        await AddItemAsync(shopper, cartId, variant, 3);

        var response = await CheckoutAsync(shopper, cartId, phone: "02-9123456"); // a Dhaka landline, not a mobile number

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(0, await CountOrdersAsync(shop.Tenant));
        Assert.Equal(0m, (await shop.StockAsync(variant)).Reserved);
    }

    [Fact]
    public async Task AddItem_UnpublishedVariant_ReturnsValidationProblem()
    {
        var shop = await ShopAsync("cart-draft");
        var draft = await shop.CreateVariantAsync("Draft Biscuit", "BISCUIT-DRAFT", price: 30m, publish: false);
        using var shopper = api.CreateTenantClient(shop.Tenant.Identifier);
        var cartId = await NewCartAsync(shopper);

        var response = await shopper.PostAsJsonAsync($"/api/ordering/carts/{cartId}/items", new { variantId = draft, quantity = 1 }, Cancel);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Empty((await GetCartAsync(shopper, cartId)).Items);
    }

    [Fact]
    public async Task Checkout_VariantWithdrawnAfterAddingToCart_ReturnsConflict()
    {
        var shop = await ShopAsync("chk-gone");
        var kept = await shop.StockedVariantAsync("Pran Juice", "JUICE-250", price: 25m, stock: 5);
        var withdrawn = await shop.StockedVariantAsync("Old Juice", "JUICE-OLD", price: 20m, stock: 5);
        using var shopper = api.CreateTenantClient(shop.Tenant.Identifier);
        var cartId = await NewCartAsync(shopper);
        await AddItemAsync(shopper, cartId, kept, 1);
        await AddItemAsync(shopper, cartId, withdrawn, 1);
        await shop.UpdateCatalogAsync($"""UPDATE catalog."Variants" SET "IsActive" = FALSE WHERE "Id" = {withdrawn}""");

        var cart = await GetCartAsync(shopper, cartId);
        var response = await CheckoutAsync(shopper, cartId);

        Assert.False(Assert.Single(cart.Items, item => item.VariantId == withdrawn).IsAvailable);
        Assert.Equal(25m, cart.Subtotal);
        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal("ordering.items_unavailable", await ProblemCodeAsync(response));
        Assert.Equal(0m, (await shop.StockAsync(kept)).Reserved);
    }

    [Fact]
    public async Task Checkout_PriceChangesLater_OrderKeepsPriceAtPurchase()
    {
        var shop = await ShopAsync("chk-price");
        var variant = await shop.StockedVariantAsync("Fresh Milk 1L", "MILK-1L", price: 90m, stock: 5);
        using var shopper = api.CreateTenantClient(shop.Tenant.Identifier);
        var order = await PlaceOrderAsync(shopper, variant, quantity: 2);

        await shop.UpdateCatalogAsync($"""UPDATE catalog."Variants" SET "Price" = 110 WHERE "Id" = {variant}""");
        var reread = await shop.Staff.GetFromJsonAsync<OrderResponse>($"/api/ordering/orders/{order.Id}", Cancel);
        var newCart = await NewCartAsync(shopper);
        var repriced = await AddItemAsync(shopper, newCart, variant, 1);

        Assert.Equal((90m, 180m, 180m), (reread!.Lines[0].UnitPrice, reread.Lines[0].LineTotal, reread.Total));
        Assert.Equal(110m, Assert.Single(repriced.Items).UnitPrice);
    }

    [Fact]
    public async Task Checkout_SameCartTwice_ReturnsTheSameOrder()
    {
        var shop = await ShopAsync("chk-again");
        var variant = await shop.StockedVariantAsync("Radhuni Chilli", "CHILLI-200G", price: 85m, stock: 10);
        using var shopper = api.CreateTenantClient(shop.Tenant.Identifier);
        var cartId = await NewCartAsync(shopper);
        await AddItemAsync(shopper, cartId, variant, 3);

        var first = await CheckoutAsync(shopper, cartId);
        var second = await CheckoutAsync(shopper, cartId);

        Assert.Equal((HttpStatusCode.Created, HttpStatusCode.OK), (first.StatusCode, second.StatusCode));
        var firstOrder = await first.Content.ReadFromJsonAsync<OrderResponse>(Cancel);
        var secondOrder = await second.Content.ReadFromJsonAsync<OrderResponse>(Cancel);
        Assert.Equal(firstOrder!.Id, secondOrder!.Id);
        Assert.Equal(1, await CountOrdersAsync(shop.Tenant));
        Assert.Equal(3m, (await shop.StockAsync(variant)).Reserved);
    }

    [Fact]
    public async Task Checkout_SameCartConcurrently_CreatesOneOrderAndHoldsStockOnce()
    {
        const int Attempts = 6;
        var shop = await ShopAsync("chk-race");
        var variant = await shop.StockedVariantAsync("Fresh Atta 2kg", "ATTA-2KG", price: 130m, stock: 50);
        using var shopper = api.CreateTenantClient(shop.Tenant.Identifier);
        var cartId = await NewCartAsync(shopper);
        await AddItemAsync(shopper, cartId, variant, 4);

        var responses = await Task.WhenAll(Enumerable.Range(0, Attempts).Select(async _ =>
        {
            using var client = api.CreateTenantClient(shop.Tenant.Identifier);
            return await CheckoutAsync(client, cartId);
        }));

        var orders = await Task.WhenAll(responses.Select(async r =>
        {
            await r.EnsureSuccessWithBodyAsync();
            return (await r.Content.ReadFromJsonAsync<OrderResponse>(Cancel))!.Id;
        }));
        Assert.Single(orders.Distinct());
        Assert.Single(responses, r => r.StatusCode == HttpStatusCode.Created);
        Assert.Equal(1, await CountOrdersAsync(shop.Tenant));
        Assert.Equal(4m, (await shop.StockAsync(variant)).Reserved);
        Assert.Equal(1, await OrderPlacedInOutboxAsync(shop.Tenant, orders[0]));
    }

    [Fact]
    public async Task AddItem_FractionalQuantityOfUnitItem_ReturnsValidationProblem()
    {
        var shop = await ShopAsync("cart-frac");
        var soap = await shop.CreateVariantAsync("Lifebuoy Soap", "SOAP-LB", price: 40m);
        var fish = await shop.CreateVariantAsync("Rui Fish", "FISH-RUI", price: 380m, soldByWeight: true);
        using var shopper = api.CreateTenantClient(shop.Tenant.Identifier);
        var cartId = await NewCartAsync(shopper);

        var soapResponse = await shopper.PostAsJsonAsync($"/api/ordering/carts/{cartId}/items", new { variantId = soap, quantity = 1.5m }, Cancel);
        var weighed = await AddItemAsync(shopper, cartId, fish, 1.5m);

        Assert.Equal(HttpStatusCode.BadRequest, soapResponse.StatusCode);
        var line = Assert.Single(weighed.Items);
        Assert.Equal((1.5m, 570m), (line.Quantity, line.LineTotal));
    }

    [Fact]
    public async Task Cart_UpdateAndRemoveItems_RepricesSubtotal()
    {
        var shop = await ShopAsync("cart-edit");
        var tea = await shop.CreateVariantAsync("Tea", "TEA-A", price: 100m);
        var sugar = await shop.CreateVariantAsync("Sugar", "SUGAR-A", price: 120m);
        using var shopper = api.CreateTenantClient(shop.Tenant.Identifier);
        var cartId = await NewCartAsync(shopper);
        await AddItemAsync(shopper, cartId, tea, 1);
        await AddItemAsync(shopper, cartId, sugar, 1);
        await AddItemAsync(shopper, cartId, tea, 1);

        var updated = await SendAsync<CartResponse>(shopper, HttpMethod.Put, $"/api/ordering/carts/{cartId}/items/{sugar}", new { quantity = 3 });
        var removed = await SendAsync<CartResponse>(shopper, HttpMethod.Delete, $"/api/ordering/carts/{cartId}/items/{tea}", body: null);

        Assert.Equal(560m, updated.Subtotal);
        Assert.Equal((sugar, 3m, 360m), (Assert.Single(removed.Items).VariantId, removed.Items[0].Quantity, removed.Subtotal));
    }

    [Fact]
    public async Task Cart_AfterCheckout_RefusesChanges()
    {
        var shop = await ShopAsync("cart-closed");
        var variant = await shop.StockedVariantAsync("Detergent", "DET-1KG", price: 160m, stock: 5);
        using var shopper = api.CreateTenantClient(shop.Tenant.Identifier);
        var cartId = await NewCartAsync(shopper);
        await AddItemAsync(shopper, cartId, variant, 1);
        await (await CheckoutAsync(shopper, cartId)).EnsureSuccessWithBodyAsync();

        var response = await shopper.PostAsJsonAsync($"/api/ordering/carts/{cartId}/items", new { variantId = variant, quantity = 1 }, Cancel);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal("ordering.cart_checked_out", await ProblemCodeAsync(response));
    }

    [Theory]
    [InlineData("""{ "cartId": "CART", "phone": "", "name": "Rahim", "address": { "line1": "House 1", "area": "Mirpur", "city": "Dhaka" } }""")]
    [InlineData("""{ "cartId": "CART", "phone": "01712345678", "name": "", "address": { "line1": "House 1", "area": "Mirpur", "city": "Dhaka" } }""")]
    [InlineData("""{ "cartId": "CART", "phone": "01712345678", "name": "Rahim", "address": { "line1": "", "area": "Mirpur", "city": "Dhaka" } }""")]
    [InlineData("""{ "cartId": "CART", "phone": "01712345678", "name": "Rahim" }""")]
    public async Task Checkout_InvalidInput_ReturnsValidationProblem(string body)
    {
        var shop = await ShopAsync("chk-bad");
        var variant = await shop.StockedVariantAsync("Salt", "SALT-1KG", price: 42m, stock: 5);
        using var shopper = api.CreateTenantClient(shop.Tenant.Identifier);
        var cartId = await NewCartAsync(shopper);
        await AddItemAsync(shopper, cartId, variant, 1);

        var response = await shopper.PostAsync(
            "/api/ordering/checkout",
            new StringContent(body.Replace("CART", cartId.ToString(), StringComparison.Ordinal), System.Text.Encoding.UTF8, "application/json"),
            Cancel);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(0m, (await shop.StockAsync(variant)).Reserved);
    }

    [Fact]
    public async Task Checkout_EmptyOrUnknownCart_IsRefused()
    {
        var shop = await ShopAsync("chk-empty");
        using var shopper = api.CreateTenantClient(shop.Tenant.Identifier);
        var cartId = await NewCartAsync(shopper);

        var empty = await CheckoutAsync(shopper, cartId);
        var unknown = await CheckoutAsync(shopper, Guid.CreateVersion7());

        Assert.Equal((HttpStatusCode.BadRequest, HttpStatusCode.NotFound), (empty.StatusCode, unknown.StatusCode));
    }

    private async Task<Shop> ShopAsync(string prefix)
    {
        var tenant = await api.CreateTenantAsync(prefix);
        var staff = api.CreateTenantClient(tenant.Identifier, await api.SignInAsync(tenant.Identifier, tenant.OwnerEmail, tenant.OwnerPassword));
        return new Shop(api, tenant, staff);
    }

    private async Task<OrderResponse> PlaceOrderAsync(HttpClient shopper, Guid variantId, decimal quantity)
    {
        var cartId = await NewCartAsync(shopper);
        await AddItemAsync(shopper, cartId, variantId, quantity);
        var response = await CheckoutAsync(shopper, cartId);
        await response.EnsureSuccessWithBodyAsync();
        return (await response.Content.ReadFromJsonAsync<OrderResponse>(Cancel))!;
    }

    private static async Task<Guid> NewCartAsync(HttpClient shopper) =>
        (await SendAsync<CartResponse>(shopper, HttpMethod.Post, "/api/ordering/carts", body: null)).Id;

    private static Task<CartResponse> AddItemAsync(HttpClient shopper, Guid cartId, Guid variantId, decimal quantity) =>
        SendAsync<CartResponse>(shopper, HttpMethod.Post, $"/api/ordering/carts/{cartId}/items", new { variantId, quantity });

    private static async Task<CartResponse> GetCartAsync(HttpClient shopper, Guid cartId) =>
        (await shopper.GetFromJsonAsync<CartResponse>($"/api/ordering/carts/{cartId}", Cancel))!;

    private static Task<HttpResponseMessage> CheckoutAsync(HttpClient shopper, Guid cartId, string phone = Phone) =>
        shopper.PostAsJsonAsync("/api/ordering/checkout", new
        {
            cartId,
            phone,
            name = "Rahim Uddin",
            address = new { line1 = "House 12, Road 5", area = "Dhanmondi", city = "Dhaka", postalCode = "1205" },
            note = "Call before coming",
        }, Cancel);

    private async Task<int> CountOrdersAsync(TestTenant tenant)
    {
        await using var scope = await api.CreateTenantScopeAsync(tenant.Identifier);
        return await scope.ServiceProvider.GetRequiredService<OrderingDbContext>().Orders.CountAsync(Cancel);
    }

    /// <summary>OrderPlaced events written for the order, delivered or not.</summary>
    private async Task<int> OrderPlacedInOutboxAsync(TestTenant tenant, Guid orderId)
    {
        await using var scope = await api.CreateTenantScopeAsync(tenant.Identifier);
        var db = scope.ServiceProvider.GetRequiredService<OrderingDbContext>();
        var events = await db.OutboxMessages.AsNoTracking().Select(m => m.Content).ToListAsync(Cancel);
        return events.Count(content => content.Contains(orderId.ToString(), StringComparison.OrdinalIgnoreCase));
    }

    private static async Task<string?> ProblemCodeAsync(HttpResponseMessage response) =>
        (await response.Content.ReadFromJsonAsync<Problem>(Cancel))?.Code;

    private static async Task<T> SendAsync<T>(HttpClient client, HttpMethod method, string url, object? body)
    {
        using var request = new HttpRequestMessage(method, url) { Content = body is null ? null : JsonContent.Create(body) };
        var response = await client.SendAsync(request, Cancel);
        await response.EnsureSuccessWithBodyAsync();
        return (await response.Content.ReadFromJsonAsync<T>(Cancel))!;
    }

    /// <summary>A tenant with a signed-in owner, for setting up catalog and stock.</summary>
    private sealed record Shop(ClarioApiFactory Api, TestTenant Tenant, HttpClient Staff)
    {
        public async Task<Guid> CreateVariantAsync(string name, string sku, decimal price, bool soldByWeight = false, bool publish = true)
        {
            var product = await PostAsync<ProductResponse>("/api/catalog/products", new
            {
                name,
                variants = new[] { new { sku, name = sku, price, soldByWeight } },
            });
            await using var scope = await Api.CreateTenantScopeAsync(Tenant.Identifier);
            var db = scope.ServiceProvider.GetRequiredService<CatalogDbContext>();
            var variantId = await db.Variants.Where(v => v.ProductId == product.Id).Select(v => v.Id).SingleAsync(Cancel);
            if (publish)
            {
                (await Staff.PostAsync($"/api/catalog/products/{product.Id}/publish", null, Cancel)).EnsureSuccessStatusCode();
            }

            return variantId;
        }

        public async Task<Guid> StockedVariantAsync(string name, string sku, decimal price, decimal stock)
        {
            var variantId = await CreateVariantAsync(name, sku, price);
            await PostAsync<object>("/api/inventory/receipts", new { lines = new[] { new { variantId, quantity = stock, unitCost = price / 2 } } });
            return variantId;
        }

        public async Task<StockRow> StockAsync(Guid variantId)
        {
            var page = await Staff.GetFromJsonAsync<StockPage>($"/api/inventory/stock?variantId={variantId}", Cancel);
            return Assert.Single(page!.Items);
        }

        /// <summary>Changes the catalog behind the API's back; Catalog has no edit endpoints yet.</summary>
        public async Task UpdateCatalogAsync(FormattableString sql)
        {
            await using var scope = await Api.CreateTenantScopeAsync(Tenant.Identifier);
            var updated = await scope.ServiceProvider.GetRequiredService<CatalogDbContext>().Database.ExecuteSqlAsync(sql, Cancel);
            Assert.Equal(1, updated);
        }

        private async Task<T> PostAsync<T>(string url, object body)
        {
            var response = await Staff.PostAsJsonAsync(url, body, Cancel);
            await response.EnsureSuccessWithBodyAsync();
            return (await response.Content.ReadFromJsonAsync<T>(Cancel))!;
        }
    }

    private sealed record Problem(string? Code);

    private sealed record ProductResponse(Guid Id, string Slug);

    private sealed record StockPage(IReadOnlyList<StockRow> Items);

    private sealed record StockRow(Guid VariantId, decimal OnHand, decimal Reserved, decimal Available);

    private sealed record CartResponse(Guid Id, IReadOnlyList<CartLineResponse> Items, decimal Subtotal, Guid? OrderId);

    private sealed record CartLineResponse(Guid VariantId, string? Sku, decimal? UnitPrice, decimal Quantity, decimal? LineTotal, bool IsAvailable);

    private sealed record OrderResponse(
        Guid Id,
        string Number,
        string Status,
        string PaymentMethod,
        Guid CustomerId,
        string CustomerName,
        string CustomerPhone,
        IReadOnlyList<OrderLineResponse> Lines,
        decimal Subtotal,
        decimal Total,
        IReadOnlyList<StatusChangeResponse> History);

    private sealed record OrderLineResponse(Guid VariantId, string Sku, decimal UnitPrice, decimal Quantity, decimal LineTotal);

    private sealed record StatusChangeResponse(string? FromStatus, string ToStatus);
}
