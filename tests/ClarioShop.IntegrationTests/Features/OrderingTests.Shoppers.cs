using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using ClarioShop.IntegrationTests.Infrastructure;

namespace ClarioShop.IntegrationTests.Features;

/// <summary>Signed-in shoppers: their own order history, and checkout with their account and saved addresses.</summary>
public sealed partial class OrderingTests
{
    private const string OtherPhone = "+8801812345678";

    private static readonly object DeliveryAddress = new { line1 = "House 12, Road 5", area = "Dhanmondi", city = "Dhaka", postalCode = "1205" };

    [Fact]
    public async Task ListMyOrders_TwoShoppers_EachSeesOnlyTheirOwnOrders()
    {
        var shop = await ShopAsync("my-list");
        var variant = await shop.StockedVariantAsync("Miniket Rice 5kg", "RICE-5KG", price: 450m, stock: 10);
        using var guest = api.CreateTenantClient(shop.Tenant.Identifier);
        var placedAsGuest = await PlaceOrderAsync(guest, variant, quantity: 1); // with Phone, before signing in
        using var rahim = await ShopperClientAsync(shop, Phone);
        using var karim = await ShopperClientAsync(shop, OtherPhone);
        var placedSignedIn = await PlaceSignedInOrderAsync(rahim, variant, new { savedAddressId = (Guid?)null, address = DeliveryAddress });

        var rahimsOrders = await rahim.GetFromJsonAsync<MyOrderPage>("/api/ordering/me/orders", Cancel);
        var karimsOrders = await karim.GetFromJsonAsync<MyOrderPage>("/api/ordering/me/orders", Cancel);

        Assert.Equal([placedSignedIn.Id, placedAsGuest.Id], rahimsOrders!.Items.Select(order => order.Id));
        Assert.Equal((2, 1, 450m), (rahimsOrders.TotalCount, rahimsOrders.Items[0].LineCount, rahimsOrders.Items[0].Total));
        Assert.Equal(0, karimsOrders!.TotalCount);
    }

    [Fact]
    public async Task GetMyOrder_AnotherShoppersOrder_ReturnsNotFound()
    {
        var shop = await ShopAsync("my-get");
        var variant = await shop.StockedVariantAsync("Teer Sugar 1kg", "SUGAR-1KG", price: 125m, stock: 5);
        using var rahim = await ShopperClientAsync(shop, Phone);
        using var karim = await ShopperClientAsync(shop, OtherPhone);
        var order = await PlaceSignedInOrderAsync(rahim, variant, new { address = DeliveryAddress });

        var own = await rahim.GetAsync($"/api/ordering/me/orders/{order.Id}", Cancel);
        var foreign = await karim.GetAsync($"/api/ordering/me/orders/{order.Id}", Cancel);

        Assert.Equal((HttpStatusCode.OK, HttpStatusCode.NotFound), (own.StatusCode, foreign.StatusCode));
    }

    [Fact]
    public async Task GetMyOrder_ConfirmedWithStaffNote_HidesNoteAndStaffUser()
    {
        var shop = await ShopAsync("my-hist");
        var variant = await shop.StockedVariantAsync("Lux Soap", "SOAP-LUX", price: 45m, stock: 5);
        using var rahim = await ShopperClientAsync(shop, Phone);
        var order = await PlaceSignedInOrderAsync(rahim, variant, new { address = DeliveryAddress });
        (await shop.Staff.PostAsJsonAsync($"/api/ordering/orders/{order.Id}/confirm", new { note = "Regular, rude on phone" }, Cancel)).EnsureSuccessStatusCode();

        var mine = await rahim.GetFromJsonAsync<MyOrderResponse>($"/api/ordering/me/orders/{order.Id}", Cancel);

        Assert.Equal(["Placed", "Confirmed"], mine!.History.Select(change => change.ToStatus));
        Assert.All(mine.History, change => Assert.Equal((null, null), (change.Note, change.ChangedByUserId)));
    }

    [Fact]
    public async Task MyOrders_StaffToken_ReturnsForbidden()
    {
        var shop = await ShopAsync("my-staff");

        var response = await shop.Staff.GetAsync("/api/ordering/me/orders", Cancel);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Checkout_SignedInWithSavedAddress_DeliversThereAsTheAccount()
    {
        var shop = await ShopAsync("my-saved");
        var variant = await shop.StockedVariantAsync("Rupchanda Oil 5L", "OIL-5L", price: 899.5m, stock: 5);
        using var rahim = await ShopperClientAsync(shop, Phone);
        var profile = await SaveAddressAsync(rahim, line1: "Level 7, Gulshan Avenue", area: "Gulshan");

        var order = await PlaceSignedInOrderAsync(rahim, variant, new { savedAddressId = profile.Addresses[0].Id });

        Assert.Equal((profile.Id, "+8801712345678", "Rahim Uddin"), (order.CustomerId, order.CustomerPhone, order.CustomerName));
        Assert.Equal(("Level 7, Gulshan Avenue", "Gulshan", "Dhaka"), (order.DeliveryAddress.Line1, order.DeliveryAddress.Area, order.DeliveryAddress.City));
    }

    [Fact]
    public async Task Checkout_AnotherShoppersSavedAddress_ReturnsValidationProblemAndReservesNothing()
    {
        var shop = await ShopAsync("my-foreign");
        var variant = await shop.StockedVariantAsync("Eggs (dozen)", "EGG-12", price: 140m, stock: 3);
        using var rahim = await ShopperClientAsync(shop, Phone);
        using var karim = await ShopperClientAsync(shop, OtherPhone);
        var rahimsAddress = (await SaveAddressAsync(rahim, line1: "House 12, Road 5", area: "Dhanmondi")).Addresses[0].Id;
        var cartId = await NewCartAsync(karim);
        await AddItemAsync(karim, cartId, variant, 1);

        var response = await karim.PostAsJsonAsync("/api/ordering/checkout", new { cartId, savedAddressId = rahimsAddress }, Cancel);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal((0, 0m), (await CountOrdersAsync(shop.Tenant), (await shop.StockAsync(variant)).Reserved));
    }

    [Fact]
    public async Task Checkout_GuestWithSavedAddress_ReturnsValidationProblem()
    {
        var shop = await ShopAsync("my-guest");
        var variant = await shop.StockedVariantAsync("Ispahani Tea", "TEA-400G", price: 250m, stock: 3);
        using var rahim = await ShopperClientAsync(shop, Phone);
        var address = (await SaveAddressAsync(rahim, line1: "House 12, Road 5", area: "Dhanmondi")).Addresses[0].Id;
        using var guest = api.CreateTenantClient(shop.Tenant.Identifier);
        var cartId = await NewCartAsync(guest);
        await AddItemAsync(guest, cartId, variant, 1);

        var response = await guest.PostAsJsonAsync("/api/ordering/checkout", new { cartId, phone = Phone, name = "Rahim Uddin", savedAddressId = address }, Cancel);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(0, await CountOrdersAsync(shop.Tenant));
    }

    [Fact]
    public async Task Checkout_SignedInWithSomeoneElsesPhone_ReturnsValidationProblem()
    {
        var shop = await ShopAsync("my-phone");
        var variant = await shop.StockedVariantAsync("Pran Juice", "JUICE-250", price: 25m, stock: 3);
        using var rahim = await ShopperClientAsync(shop, Phone);
        var cartId = await NewCartAsync(rahim);
        await AddItemAsync(rahim, cartId, variant, 1);

        var response = await rahim.PostAsJsonAsync("/api/ordering/checkout", new { cartId, phone = OtherPhone, name = "Rahim Uddin", address = DeliveryAddress }, Cancel);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(0, await CountOrdersAsync(shop.Tenant));
    }

    [Fact]
    public async Task Checkout_GuestWithoutPhone_ReturnsValidationProblem()
    {
        var shop = await ShopAsync("my-nophone");
        var variant = await shop.StockedVariantAsync("Lux Soap", "SOAP-LUX", price: 45m, stock: 3);
        using var guest = api.CreateTenantClient(shop.Tenant.Identifier);
        var cartId = await NewCartAsync(guest);
        await AddItemAsync(guest, cartId, variant, 1);

        var response = await guest.PostAsJsonAsync("/api/ordering/checkout", new { cartId, name = "Rahim Uddin", address = DeliveryAddress }, Cancel);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(0m, (await shop.StockAsync(variant)).Reserved);
    }

    private async Task<HttpClient> ShopperClientAsync(Shop shop, string phone)
    {
        var client = api.CreateTenantClient(shop.Tenant.Identifier, await api.SignInShopperAsync(shop.Tenant.Identifier, phone));
        var named = await client.PutAsJsonAsync("/api/customers/me", new { name = "Rahim Uddin" }, Cancel);
        await named.EnsureSuccessWithBodyAsync();
        return client;
    }

    private static Task<ProfileResponse> SaveAddressAsync(HttpClient shopper, string line1, string area) =>
        SendAsync<ProfileResponse>(shopper, HttpMethod.Post, "/api/customers/me/addresses", new
        {
            label = "Home",
            recipientName = "Rahim Uddin",
            recipientPhone = "01712-345678",
            line1,
            area,
            city = "Dhaka",
        });

    /// <summary>Checks out a one-item cart as the signed-in shopper; <paramref name="delivery"/> holds address or savedAddressId.</summary>
    private static async Task<MyOrderResponse> PlaceSignedInOrderAsync(HttpClient shopper, Guid variantId, object delivery)
    {
        var cartId = await NewCartAsync(shopper);
        await AddItemAsync(shopper, cartId, variantId, 1);
        var body = JsonSerializer.SerializeToNode(delivery)!.AsObject();
        body["cartId"] = cartId;
        var response = await shopper.PostAsJsonAsync("/api/ordering/checkout", body, Cancel);
        await response.EnsureSuccessWithBodyAsync();
        return (await response.Content.ReadFromJsonAsync<MyOrderResponse>(Cancel))!;
    }

    private sealed record ProfileResponse(Guid Id, IReadOnlyList<IdResponse> Addresses);

    private sealed record MyOrderPage(IReadOnlyList<MyOrderRow> Items, int TotalCount);

    private sealed record MyOrderRow(Guid Id, string Number, int LineCount, decimal Total);

    private sealed record MyOrderResponse(
        Guid Id,
        Guid CustomerId,
        string CustomerName,
        string CustomerPhone,
        DeliveryAddressResponse DeliveryAddress,
        IReadOnlyList<MyStatusChangeResponse> History);

    private sealed record DeliveryAddressResponse(string Line1, string Area, string City);

    private sealed record MyStatusChangeResponse(string ToStatus, string? Note, Guid? ChangedByUserId);
}
