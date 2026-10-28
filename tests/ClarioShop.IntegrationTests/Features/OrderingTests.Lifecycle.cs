using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using ClarioShop.Infrastructure.Messaging;
using ClarioShop.IntegrationTests.Infrastructure;
using ClarioShop.Inventory.Persistence;
using ClarioShop.Ordering.Contracts.Events;
using ClarioShop.Ordering.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace ClarioShop.IntegrationTests.Features;

/// <summary>Staff moving orders through their lifecycle, and Inventory following through on the stock.</summary>
public sealed partial class OrderingTests
{
    private static readonly TimeSpan EventTimeout = TimeSpan.FromSeconds(15);

    [Fact]
    public async Task Confirm_PlacedOrder_SellsTheReservedStock()
    {
        var shop = await ShopAsync("life-confirm");
        var variant = await shop.StockedVariantAsync("Miniket Rice 5kg", "RICE-5KG", price: 450m, stock: 10);
        using var shopper = api.CreateTenantClient(shop.Tenant.Identifier);
        var order = await PlaceOrderAsync(shopper, variant, quantity: 2);

        var response = await shop.Staff.PostAsJsonAsync($"/api/ordering/orders/{order.Id}/confirm", new { note = "Called the customer" }, Cancel);

        await response.EnsureSuccessWithBodyAsync();
        var confirmed = (await response.Content.ReadFromJsonAsync<OrderResponse>(Cancel))!;
        Assert.Equal("Confirmed", confirmed.Status);
        Assert.Equal(["Placed", "Confirmed"], confirmed.History.Select(change => change.ToStatus));
        await api.Events.WaitForAsync<OrderConfirmed>(e => e.OrderId == order.Id, EventTimeout);
        var stock = await WaitForStockAsync(shop, variant, s => s.OnHand == 8m);
        Assert.Equal((8m, 0m), (stock.OnHand, stock.Reserved));
        var sale = Assert.Single(await MovementsAsync(shop, variant), m => m.Type == "OnlineSale");
        Assert.Equal((-2m, "Order", order.Id), (sale.Quantity, sale.ReferenceType, sale.ReferenceId));
    }

    [Fact]
    public async Task Cancel_PlacedOrder_ReleasesTheReservation()
    {
        var shop = await ShopAsync("life-cancel");
        var variant = await shop.StockedVariantAsync("Rupchanda Oil 5L", "OIL-5L", price: 899m, stock: 5);
        using var shopper = api.CreateTenantClient(shop.Tenant.Identifier);
        var order = await PlaceOrderAsync(shopper, variant, quantity: 3);

        var cancelled = await ChangeStatusAsync(shop, order.Id, "cancel", note: "Customer changed their mind");

        Assert.Equal("Cancelled", cancelled.Status);
        var published = await api.Events.WaitForAsync<OrderCancelled>(e => e.OrderId == order.Id, EventTimeout);
        Assert.Equal(("Placed", "Customer changed their mind"), (published.PreviousStatus, published.Reason));
        var stock = await WaitForStockAsync(shop, variant, s => s.Reserved == 0m);
        Assert.Equal((5m, 5m), (stock.OnHand, stock.Available));
        Assert.DoesNotContain(await MovementsAsync(shop, variant), m => m.Type == "OnlineSale");
    }

    [Fact]
    public async Task Cancel_ConfirmedOrder_PutsTheSoldStockBack()
    {
        var shop = await ShopAsync("life-unsell");
        var variant = await shop.StockedVariantAsync("Teer Sugar 1kg", "SUGAR-1KG", price: 125m, stock: 10);
        using var shopper = api.CreateTenantClient(shop.Tenant.Identifier);
        var order = await PlaceOrderAsync(shopper, variant, quantity: 4);
        await ChangeStatusAsync(shop, order.Id, "confirm");
        await WaitForStockAsync(shop, variant, s => s.OnHand == 6m);
        await ChangeStatusAsync(shop, order.Id, "pack");

        await ChangeStatusAsync(shop, order.Id, "cancel");

        var stock = await WaitForStockAsync(shop, variant, s => s.OnHand == 10m);
        Assert.Equal((10m, 0m), (stock.OnHand, stock.Reserved));
        var reversal = Assert.Single(await MovementsAsync(shop, variant), m => m.Type == "OnlineSaleReversal");
        Assert.Equal((4m, order.Id), (reversal.Quantity, reversal.ReferenceId));
    }

    [Fact]
    public async Task Confirm_AfterReservationExpired_ReservesAgainAndSells()
    {
        var shop = await ShopAsync("life-late");
        var variant = await shop.StockedVariantAsync("Ispahani Tea", "TEA-400G", price: 250m, stock: 5);
        using var shopper = api.CreateTenantClient(shop.Tenant.Identifier);
        var order = await PlaceOrderAsync(shopper, variant, quantity: 2);
        var checkoutReservation = await ReservationOfAsync(shop, order.Id);
        await ExpireReservationsAsync(shop, order.Id);

        var confirmed = await ChangeStatusAsync(shop, order.Id, "confirm");

        Assert.Equal("Confirmed", confirmed.Status);
        Assert.NotEqual(checkoutReservation, await ReservationOfAsync(shop, order.Id));
        var stock = await WaitForStockAsync(shop, variant, s => s.OnHand == 3m);
        Assert.Equal((3m, 0m), (stock.OnHand, stock.Reserved));
    }

    [Fact]
    public async Task Confirm_AfterReservationExpiredAndStockSoldElsewhere_ReturnsConflict()
    {
        var shop = await ShopAsync("life-gone");
        var variant = await shop.StockedVariantAsync("Eggs (dozen)", "EGG-12", price: 140m, stock: 2);
        using var shopper = api.CreateTenantClient(shop.Tenant.Identifier);
        var late = await PlaceOrderAsync(shopper, variant, quantity: 2);
        await ExpireReservationsAsync(shop, late.Id);
        await PlaceOrderAsync(shopper, variant, quantity: 2); // takes the stock the expired hold gave back

        var response = await shop.Staff.PostAsync($"/api/ordering/orders/{late.Id}/confirm", content: null, Cancel);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal("ordering.stock_unavailable", await ProblemCodeAsync(response));
        var reread = await shop.Staff.GetFromJsonAsync<OrderResponse>($"/api/ordering/orders/{late.Id}", Cancel);
        Assert.Equal("Placed", reread!.Status);
        Assert.Equal((2m, 2m), ((await shop.StockAsync(variant)).OnHand, (await shop.StockAsync(variant)).Reserved));
    }

    [Fact]
    public async Task ChangeStatus_FullLifecycle_RecordsHistoryAndPublishesEvents()
    {
        var shop = await ShopAsync("life-full");
        var variant = await shop.StockedVariantAsync("Lux Soap", "SOAP-LUX", price: 45m, stock: 10);
        using var shopper = api.CreateTenantClient(shop.Tenant.Identifier);
        var order = await PlaceOrderAsync(shopper, variant, quantity: 1);

        await ChangeStatusAsync(shop, order.Id, "confirm");
        await ChangeStatusAsync(shop, order.Id, "pack");
        await ChangeStatusAsync(shop, order.Id, "ship");
        var delivered = await ChangeStatusAsync(shop, order.Id, "deliver");

        Assert.Equal(["Placed", "Confirmed", "Packed", "Shipped", "Delivered"], delivered.History.Select(change => change.ToStatus));
        Assert.Equal(["Placed", "Confirmed", "Packed", "Shipped"], delivered.History.Skip(1).Select(change => change.FromStatus));
        await api.Events.WaitForAsync<OrderShipped>(e => e.OrderId == order.Id, EventTimeout);
        var deliveredEvent = await api.Events.WaitForAsync<OrderDelivered>(e => e.OrderId == order.Id, EventTimeout);
        Assert.Equal((order.Number, order.CustomerId), (deliveredEvent.OrderNumber, deliveredEvent.CustomerId));
    }

    [Theory]
    [InlineData("ship")]
    [InlineData("deliver")]
    [InlineData("return")]
    [InlineData("pack")]
    public async Task ChangeStatus_SkippingSteps_ReturnsConflict(string action)
    {
        var shop = await ShopAsync("life-skip");
        var variant = await shop.StockedVariantAsync("Salt", "SALT-1KG", price: 42m, stock: 5);
        using var shopper = api.CreateTenantClient(shop.Tenant.Identifier);
        var order = await PlaceOrderAsync(shopper, variant, quantity: 1);

        var response = await shop.Staff.PostAsync($"/api/ordering/orders/{order.Id}/{action}", content: null, Cancel);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal("ordering.invalid_transition", await ProblemCodeAsync(response));
    }

    [Fact]
    public async Task Cancel_ShippedOrder_ReturnsConflict()
    {
        var shop = await ShopAsync("life-shipped");
        var variant = await shop.StockedVariantAsync("Radhuni Chilli", "CHILLI-200G", price: 85m, stock: 5);
        using var shopper = api.CreateTenantClient(shop.Tenant.Identifier);
        var order = await PlaceOrderAsync(shopper, variant, quantity: 1);
        foreach (var action in new[] { "confirm", "pack", "ship" })
        {
            await ChangeStatusAsync(shop, order.Id, action);
        }

        var response = await shop.Staff.PostAsync($"/api/ordering/orders/{order.Id}/cancel", content: null, Cancel);
        var returned = await ChangeStatusAsync(shop, order.Id, "return", note: "Customer refused the parcel");

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal("Returned", returned.Status);
    }

    [Fact]
    public async Task Confirm_Concurrently_ConfirmsOnceAndSellsOnce()
    {
        const int Attempts = 5;
        var shop = await ShopAsync("life-race");
        var variant = await shop.StockedVariantAsync("Fresh Atta 2kg", "ATTA-2KG", price: 130m, stock: 20);
        using var shopper = api.CreateTenantClient(shop.Tenant.Identifier);
        var order = await PlaceOrderAsync(shopper, variant, quantity: 3);
        var token = shop.Staff.DefaultRequestHeaders.Authorization!.Parameter;

        var responses = await Task.WhenAll(Enumerable.Range(0, Attempts).Select(async _ =>
        {
            using var client = api.CreateTenantClient(shop.Tenant.Identifier, token);
            return await client.PostAsync($"/api/ordering/orders/{order.Id}/confirm", content: null, Cancel);
        }));

        Assert.Single(responses, r => r.StatusCode == HttpStatusCode.OK);
        Assert.All(responses.Where(r => r.StatusCode != HttpStatusCode.OK), r => Assert.Equal(HttpStatusCode.Conflict, r.StatusCode));
        await WaitForStockAsync(shop, variant, s => s.OnHand == 17m);
        var reread = await shop.Staff.GetFromJsonAsync<OrderResponse>($"/api/ordering/orders/{order.Id}", Cancel);
        Assert.Single(reread!.History, change => change.ToStatus == "Confirmed");
        Assert.Single(await MovementsAsync(shop, variant), m => m.Type == "OnlineSale");
        Assert.Equal(0m, (await shop.StockAsync(variant)).Reserved);
    }

    [Fact]
    public async Task OrderEvents_DeliveredTwice_ChangeStockOnce()
    {
        var shop = await ShopAsync("life-dup");
        var variant = await shop.StockedVariantAsync("Detergent", "DET-1KG", price: 160m, stock: 10);
        using var shopper = api.CreateTenantClient(shop.Tenant.Identifier);
        var order = await PlaceOrderAsync(shopper, variant, quantity: 2);
        await ChangeStatusAsync(shop, order.Id, "confirm");
        var confirmed = await api.Events.WaitForAsync<OrderConfirmed>(e => e.OrderId == order.Id, EventTimeout);
        await WaitForStockAsync(shop, variant, s => s.OnHand == 8m);
        await ChangeStatusAsync(shop, order.Id, "cancel");
        var cancelled = await api.Events.WaitForAsync<OrderCancelled>(e => e.OrderId == order.Id, EventTimeout);
        await WaitForStockAsync(shop, variant, s => s.OnHand == 10m);

        // The same events again under new message ids, as a publisher retry after a lost acknowledgement would send them.
        await RedeliverAsync(confirmed);
        await RedeliverAsync(cancelled);

        var stock = await shop.StockAsync(variant);
        Assert.Equal((10m, 0m), (stock.OnHand, stock.Reserved));
        var movements = await MovementsAsync(shop, variant);
        Assert.Single(movements, m => m.Type == "OnlineSale");
        Assert.Single(movements, m => m.Type == "OnlineSaleReversal");
    }

    [Fact]
    public async Task ChangeStatus_WithoutOrdersManage_IsRefused()
    {
        var shop = await ShopAsync("life-perm");
        var variant = await shop.StockedVariantAsync("Pran Juice", "JUICE-250", price: 25m, stock: 5);
        using var shopper = api.CreateTenantClient(shop.Tenant.Identifier);
        var order = await PlaceOrderAsync(shopper, variant, quantity: 1);
        using var catalogStaff = await StaffWithRoleAsync(shop, "catalog@shop.test", "Catalog Manager");

        var byShopper = await shopper.PostAsync($"/api/ordering/orders/{order.Id}/confirm", content: null, Cancel);
        var byCatalogStaff = await catalogStaff.PostAsync($"/api/ordering/orders/{order.Id}/cancel", content: null, Cancel);

        Assert.Equal((HttpStatusCode.Unauthorized, HttpStatusCode.Forbidden), (byShopper.StatusCode, byCatalogStaff.StatusCode));
    }

    [Fact]
    public async Task OrderHandlerRole_CanListAndConfirmOrders()
    {
        var shop = await ShopAsync("life-role");
        var variant = await shop.StockedVariantAsync("Fresh Milk 1L", "MILK-1L", price: 90m, stock: 5);
        using var shopper = api.CreateTenantClient(shop.Tenant.Identifier);
        var first = await PlaceOrderAsync(shopper, variant, quantity: 1);
        var second = await PlaceOrderAsync(shopper, variant, quantity: 1);
        using var handler = await StaffWithRoleAsync(shop, "orders@shop.test", "Order Handler");
        (await handler.PostAsync($"/api/ordering/orders/{first.Id}/confirm", content: null, Cancel)).EnsureSuccessStatusCode();

        var placed = await handler.GetFromJsonAsync<OrderPage>("/api/ordering/orders?status=placed", Cancel);
        var all = await handler.GetFromJsonAsync<OrderPage>("/api/ordering/orders", Cancel);

        Assert.Equal(second.Id, Assert.Single(placed!.Items).Id);
        Assert.Equal([second.Number, first.Number], all!.Items.Select(item => item.Number));
        Assert.Equal(HttpStatusCode.BadRequest, (await handler.GetAsync("/api/ordering/orders?status=lost", Cancel)).StatusCode);
    }

    private async Task<OrderResponse> ChangeStatusAsync(Shop shop, Guid orderId, string action, string? note = null)
    {
        var response = await shop.Staff.PostAsJsonAsync($"/api/ordering/orders/{orderId}/{action}", new { note }, Cancel);
        await response.EnsureSuccessWithBodyAsync();
        return (await response.Content.ReadFromJsonAsync<OrderResponse>(Cancel))!;
    }

    private async Task<HttpClient> StaffWithRoleAsync(Shop shop, string email, string role)
    {
        const string Password = "Staff-pass-123";
        (await shop.Staff.PostAsJsonAsync("/api/identity/users", new { email, fullName = role, password = Password, roles = new[] { role } }, Cancel))
            .EnsureSuccessStatusCode();
        return api.CreateTenantClient(shop.Tenant.Identifier, await api.SignInAsync(shop.Tenant.Identifier, email, Password));
    }

    /// <summary>Waits for Inventory to catch up with an order event.</summary>
    private static async Task<StockRow> WaitForStockAsync(Shop shop, Guid variantId, Func<StockRow, bool> condition)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(Cancel);
        timeout.CancelAfter(EventTimeout);
        while (true)
        {
            var stock = await shop.StockAsync(variantId);
            if (condition(stock))
            {
                return stock;
            }

            await Task.Delay(TimeSpan.FromMilliseconds(100), timeout.Token);
        }
    }

    private static async Task<IReadOnlyList<MovementRow>> MovementsAsync(Shop shop, Guid variantId) =>
        (await shop.Staff.GetFromJsonAsync<MovementPage>($"/api/inventory/movements?variantId={variantId}", Cancel))!.Items;

    private async Task<Guid> ReservationOfAsync(Shop shop, Guid orderId)
    {
        await using var scope = await api.CreateTenantScopeAsync(shop.Tenant.Identifier);
        return await scope.ServiceProvider.GetRequiredService<OrderingDbContext>().Orders
            .Where(order => order.Id == orderId)
            .Select(order => order.ReservationId)
            .SingleAsync(Cancel);
    }

    /// <summary>Moves the order's stock hold past its 15 minutes, as if staff confirmed it late.</summary>
    private async Task ExpireReservationsAsync(Shop shop, Guid orderId)
    {
        await using var scope = await api.CreateTenantScopeAsync(shop.Tenant.Identifier);
        var updated = await scope.ServiceProvider.GetRequiredService<InventoryDbContext>().Database.ExecuteSqlAsync(
            $"""UPDATE inventory."StockReservations" SET "ExpiresAt" = now() - interval '1 minute' WHERE "ReferenceId" = {orderId} AND "Status" = 'Active'""",
            Cancel);
        Assert.Equal(1, updated);
    }

    private async Task RedeliverAsync<TEvent>(TEvent integrationEvent)
        where TEvent : ClarioShop.SharedKernel.Messaging.IIntegrationEvent
    {
        var message = new OutgoingMessage(
            Guid.CreateVersion7(),
            IntegrationEventNames.For(typeof(TEvent)),
            JsonSerializer.Serialize(integrationEvent, JsonSerializerOptions.Web),
            api.Events.TenantOf(integrationEvent));
        await api.Services.GetRequiredService<IntegrationEventDispatcher>().DispatchAsync(message, queueName: null, Cancel);
    }

    private sealed record OrderPage(IReadOnlyList<OrderRow> Items, int TotalCount);

    private sealed record OrderRow(Guid Id, string Number, string Status);

    private sealed record MovementPage(IReadOnlyList<MovementRow> Items);

    private sealed record MovementRow(decimal Quantity, string Type, string ReferenceType, Guid ReferenceId);
}
