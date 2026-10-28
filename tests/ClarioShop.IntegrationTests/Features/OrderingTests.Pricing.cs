using System.Net;
using System.Net.Http.Json;
using ClarioShop.IntegrationTests.Infrastructure;
using ClarioShop.Ordering.Contracts.Events;

namespace ClarioShop.IntegrationTests.Features;

public sealed partial class OrderingTests
{
    [Fact]
    public async Task Checkout_WithDeliveryZoneAndVat_ChargesFeeAndVatOnTopOfSubtotal()
    {
        var shop = await ShopAsync("chk-charges");
        var rice = await shop.StockedVariantAsync("Miniket Rice 5kg", "RICE-5KG", price: 450m, stock: 10);
        var oil = await shop.StockedVariantAsync("Rupchanda Oil 5L", "OIL-5L", price: 899.5m, stock: 10);
        var zone = await shop.CreateDeliveryZoneAsync("Inside Dhaka", fee: 60m, freeDeliveryThreshold: 5000m);
        await shop.SetVatRateAsync(7.5m);
        using var shopper = api.CreateTenantClient(shop.Tenant.Identifier);
        var cartId = await NewCartAsync(shopper);
        await AddItemAsync(shopper, cartId, rice, 2);
        await AddItemAsync(shopper, cartId, oil, 1);

        var response = await CheckoutAsync(shopper, cartId, deliveryZoneId: zone);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var order = (await response.Content.ReadFromJsonAsync<OrderResponse>(Cancel))!;
        Assert.Equal((1799.5m, 60m, 7.5m, 134.96m, 1994.46m), (order.Subtotal, order.DeliveryFee, order.VatRatePercent, order.Vat, order.Total));
        Assert.Equal(new DeliveryZoneResponse(zone, "Inside Dhaka"), order.DeliveryZone);
        var placed = await api.Events.WaitForAsync<OrderPlaced>(e => e.OrderId == order.Id, TimeSpan.FromSeconds(15));
        Assert.Equal((1799.5m, 60m, 134.96m, 1994.46m), (placed.Subtotal, placed.DeliveryFee, placed.Vat, placed.Total));
    }

    [Fact]
    public async Task Checkout_SubtotalReachesFreeDeliveryThreshold_DeliveryIsFree()
    {
        var shop = await ShopAsync("chk-free");
        var variant = await shop.StockedVariantAsync("Basmati Rice 5kg", "BASMATI-5KG", price: 500m, stock: 10);
        var zone = await shop.CreateDeliveryZoneAsync("Inside Dhaka", fee: 60m, freeDeliveryThreshold: 1000m);
        using var shopper = api.CreateTenantClient(shop.Tenant.Identifier);
        var cartId = await NewCartAsync(shopper);
        await AddItemAsync(shopper, cartId, variant, 2);

        var response = await CheckoutAsync(shopper, cartId, deliveryZoneId: zone);

        await response.EnsureSuccessWithBodyAsync();
        var order = (await response.Content.ReadFromJsonAsync<OrderResponse>(Cancel))!;
        Assert.Equal((1000m, 0m, 0m, 1000m), (order.Subtotal, order.DeliveryFee, order.Vat, order.Total));
    }

    [Fact]
    public async Task Checkout_StoreHasZonesButNoneChosen_ReturnsValidationProblemAndReservesNothing()
    {
        var shop = await ShopAsync("chk-nozone");
        var variant = await shop.StockedVariantAsync("Mustard Oil 1L", "MOIL-1L", price: 280m, stock: 5);
        await shop.CreateDeliveryZoneAsync("Inside Dhaka", fee: 60m, freeDeliveryThreshold: null);
        using var shopper = api.CreateTenantClient(shop.Tenant.Identifier);
        var cartId = await NewCartAsync(shopper);
        await AddItemAsync(shopper, cartId, variant, 1);

        var response = await CheckoutAsync(shopper, cartId);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(0, await CountOrdersAsync(shop.Tenant));
        Assert.Equal(0m, (await shop.StockAsync(variant)).Reserved);
    }

    [Fact]
    public async Task Checkout_InactiveOrUnknownZone_ReturnsValidationProblemAndReservesNothing()
    {
        var shop = await ShopAsync("chk-badzone");
        var variant = await shop.StockedVariantAsync("Lentils 1kg", "DAL-1KG", price: 150m, stock: 5);
        await shop.CreateDeliveryZoneAsync("Inside Dhaka", fee: 60m, freeDeliveryThreshold: null);
        var paused = await shop.CreateDeliveryZoneAsync("Chattogram", fee: 150m, freeDeliveryThreshold: null, isActive: false);
        using var shopper = api.CreateTenantClient(shop.Tenant.Identifier);
        var cartId = await NewCartAsync(shopper);
        await AddItemAsync(shopper, cartId, variant, 1);

        var inactive = await CheckoutAsync(shopper, cartId, deliveryZoneId: paused);
        var unknown = await CheckoutAsync(shopper, cartId, deliveryZoneId: Guid.CreateVersion7());

        Assert.Equal((HttpStatusCode.BadRequest, HttpStatusCode.BadRequest), (inactive.StatusCode, unknown.StatusCode));
        Assert.Equal(0, await CountOrdersAsync(shop.Tenant));
        Assert.Equal(0m, (await shop.StockAsync(variant)).Reserved);
    }

    [Fact]
    public async Task Checkout_VatAndFeeChangeLater_OrderKeepsChargesAtPurchase()
    {
        var shop = await ShopAsync("chk-keep");
        var variant = await shop.StockedVariantAsync("Ghee 500g", "GHEE-500G", price: 800m, stock: 5);
        var zone = await shop.CreateDeliveryZoneAsync("Inside Dhaka", fee: 60m, freeDeliveryThreshold: null);
        await shop.SetVatRateAsync(5m);
        using var shopper = api.CreateTenantClient(shop.Tenant.Identifier);
        var cartId = await NewCartAsync(shopper);
        await AddItemAsync(shopper, cartId, variant, 1);
        var placed = (await (await CheckoutAsync(shopper, cartId, deliveryZoneId: zone)).Content.ReadFromJsonAsync<OrderResponse>(Cancel))!;

        await shop.SetVatRateAsync(15m);
        var updateZone = await shop.Staff.PutAsJsonAsync($"/api/settings/delivery-zones/{zone}", new
        {
            name = "Inside Dhaka",
            areas = new[] { "Dhanmondi" },
            fee = 100m,
            freeDeliveryThreshold = (decimal?)null,
            isActive = true,
            sortOrder = 1,
        }, Cancel);
        await updateZone.EnsureSuccessWithBodyAsync();
        var reread = await shop.Staff.GetFromJsonAsync<OrderResponse>($"/api/ordering/orders/{placed.Id}", Cancel);

        Assert.Equal((800m, 60m, 5m, 40m, 900m), (placed.Subtotal, placed.DeliveryFee, placed.VatRatePercent, placed.Vat, placed.Total));
        Assert.Equal((60m, 5m, 40m, 900m), (reread!.DeliveryFee, reread.VatRatePercent, reread.Vat, reread.Total));
    }
}
