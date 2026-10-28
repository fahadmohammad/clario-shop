using System.Net;
using System.Net.Http.Json;
using ClarioShop.IntegrationTests.Infrastructure;
using ClarioShop.Payments.Contracts.Events;
using ClarioShop.Payments.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace ClarioShop.IntegrationTests.Features;

/// <summary>Cash collected on delivery: Payments records it, Ordering shows the order as paid.</summary>
public sealed partial class OrderingTests
{
    [Fact]
    public async Task CollectCash_DeliveredOrder_RecordsPaymentAndOrderShowsPaid()
    {
        var shop = await ShopAsync("pay-cod");
        var variant = await shop.StockedVariantAsync("Miniket Rice 5kg", "RICE-5KG", price: 450m, stock: 10);
        using var shopper = api.CreateTenantClient(shop.Tenant.Identifier);
        var order = await PlaceOrderAsync(shopper, variant, quantity: 2);
        await DeliverAsync(shop, order.Id);

        var response = await shop.Staff.PostAsJsonAsync($"/api/payments/orders/{order.Id}/cash-collections", new { amount = 900m, note = "Paid in notes" }, Cancel);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var payments = (await response.Content.ReadFromJsonAsync<OrderPaymentsResponse>(Cancel))!;
        Assert.Equal((order.Number, 900m, 900m, 0m, "Paid"), (payments.OrderNumber, payments.Total, payments.AmountPaid, payments.Outstanding, payments.PaymentStatus));
        var payment = Assert.Single(payments.Payments);
        Assert.Equal(("CashOnDelivery", "Succeeded", 900m), (payment.Method, payment.Status, payment.Amount));
        var attempt = Assert.Single(payment.Attempts);
        Assert.Equal(("Succeeded", "Paid in notes"), (attempt.Outcome, attempt.Note));
        Assert.NotNull(attempt.RecordedByUserId);
        var published = await api.Events.WaitForAsync<PaymentSucceeded>(e => e.OrderId == order.Id, EventTimeout);
        Assert.Equal((payment.Id, 900m, "CashOnDelivery"), (published.PaymentId, published.Amount, published.Method));
        var paidOrder = await WaitForOrderAsync(shop, order.Id, o => o.PaymentStatus == "Paid");
        Assert.Equal(900m, paidOrder.AmountPaid);
    }

    [Fact]
    public async Task CollectCash_InTwoParts_IsPartiallyPaidThenPaid()
    {
        var shop = await ShopAsync("pay-parts");
        var variant = await shop.StockedVariantAsync("Rupchanda Oil 5L", "OIL-5L", price: 899.5m, stock: 5);
        using var shopper = api.CreateTenantClient(shop.Tenant.Identifier);
        var order = await PlaceOrderAsync(shopper, variant, quantity: 1);
        await ShipAsync(shop, order.Id);

        var first = await CollectAsync(shop, order.Id, 500m);
        var partlyPaid = await WaitForOrderAsync(shop, order.Id, o => o.AmountPaid == 500m);
        var second = await CollectAsync(shop, order.Id, 399.5m);

        Assert.Equal((500m, 399.5m, "PartiallyPaid"), (first.AmountPaid, first.Outstanding, first.PaymentStatus));
        Assert.Equal("PartiallyPaid", partlyPaid.PaymentStatus);
        Assert.Equal((899.5m, 0m, "Paid", 2), (second.AmountPaid, second.Outstanding, second.PaymentStatus, second.Payments.Count));
        var paid = await WaitForOrderAsync(shop, order.Id, o => o.PaymentStatus == "Paid");
        Assert.Equal(899.5m, paid.AmountPaid);
        var listed = await shop.Staff.GetFromJsonAsync<PaidOrderPage>("/api/ordering/orders", Cancel);
        Assert.Equal("Paid", Assert.Single(listed!.Items).PaymentStatus);
    }

    [Fact]
    public async Task CollectCash_MoreThanOutstanding_ReturnsConflictAndRecordsNothing()
    {
        var shop = await ShopAsync("pay-over");
        var variant = await shop.StockedVariantAsync("Teer Sugar 1kg", "SUGAR-1KG", price: 125m, stock: 5);
        using var shopper = api.CreateTenantClient(shop.Tenant.Identifier);
        var order = await PlaceOrderAsync(shopper, variant, quantity: 2);
        await DeliverAsync(shop, order.Id);
        await CollectAsync(shop, order.Id, 200m);

        var response = await shop.Staff.PostAsJsonAsync($"/api/payments/orders/{order.Id}/cash-collections", new { amount = 50.01m }, Cancel);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal("payments.exceeds_outstanding", await ProblemCodeAsync(response));
        var payments = await shop.Staff.GetFromJsonAsync<OrderPaymentsResponse>($"/api/payments/orders/{order.Id}", Cancel);
        Assert.Equal((200m, 50m, 1), (payments!.AmountPaid, payments.Outstanding, payments.Payments.Count));
    }

    [Fact]
    public async Task CollectCash_OrderNotShipped_ReturnsConflict()
    {
        var shop = await ShopAsync("pay-early");
        var variant = await shop.StockedVariantAsync("Lux Soap", "SOAP-LUX", price: 45m, stock: 5);
        using var shopper = api.CreateTenantClient(shop.Tenant.Identifier);
        var order = await PlaceOrderAsync(shopper, variant, quantity: 1);
        await ChangeStatusAsync(shop, order.Id, "confirm");

        var response = await shop.Staff.PostAsJsonAsync($"/api/payments/orders/{order.Id}/cash-collections", new { amount = 45m }, Cancel);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal("payments.not_out_for_delivery", await ProblemCodeAsync(response));
        Assert.Equal(0, await PaymentCountAsync(shop));
    }

    [Fact]
    public async Task CollectCash_FullAmountConcurrently_OnlyOneSucceeds()
    {
        const int Attempts = 6;
        var shop = await ShopAsync("pay-race");
        var variant = await shop.StockedVariantAsync("Fresh Atta 2kg", "ATTA-2KG", price: 130m, stock: 5);
        using var shopper = api.CreateTenantClient(shop.Tenant.Identifier);
        var order = await PlaceOrderAsync(shopper, variant, quantity: 2);
        await DeliverAsync(shop, order.Id);
        var token = await api.SignInAsync(shop.Tenant.Identifier, shop.Tenant.OwnerEmail, shop.Tenant.OwnerPassword);

        var responses = await Task.WhenAll(Enumerable.Range(0, Attempts).Select(async _ =>
        {
            using var client = api.CreateTenantClient(shop.Tenant.Identifier, token);
            return await client.PostAsJsonAsync($"/api/payments/orders/{order.Id}/cash-collections", new { amount = 260m }, Cancel);
        }));

        Assert.Single(responses, r => r.StatusCode == HttpStatusCode.Created);
        Assert.All(responses.Where(r => r.StatusCode != HttpStatusCode.Created), r => Assert.Equal(HttpStatusCode.Conflict, r.StatusCode));
        Assert.Equal(1, await PaymentCountAsync(shop));
    }

    [Fact]
    public async Task PaymentSucceeded_DeliveredAgain_CountsThePaymentOnce()
    {
        var shop = await ShopAsync("pay-again");
        var variant = await shop.StockedVariantAsync("Ispahani Tea", "TEA-400G", price: 250m, stock: 5);
        using var shopper = api.CreateTenantClient(shop.Tenant.Identifier);
        var order = await PlaceOrderAsync(shopper, variant, quantity: 2);
        await DeliverAsync(shop, order.Id);
        await CollectAsync(shop, order.Id, 300m);
        var published = await api.Events.WaitForAsync<PaymentSucceeded>(e => e.OrderId == order.Id, EventTimeout);
        await WaitForOrderAsync(shop, order.Id, o => o.AmountPaid == 300m);

        // The same event under a new message id, as a publisher retry after a lost acknowledgement would send it.
        await RedeliverAsync(published);

        var reread = await shop.Staff.GetFromJsonAsync<PaidOrderResponse>($"/api/ordering/orders/{order.Id}", Cancel);
        Assert.Equal((300m, "PartiallyPaid"), (reread!.AmountPaid, reread.PaymentStatus));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-5)]
    [InlineData(10.005)]
    public async Task CollectCash_InvalidAmount_ReturnsValidationProblem(decimal amount)
    {
        var shop = await ShopAsync("pay-bad");
        var variant = await shop.StockedVariantAsync("Salt", "SALT-1KG", price: 42m, stock: 5);
        using var shopper = api.CreateTenantClient(shop.Tenant.Identifier);
        var order = await PlaceOrderAsync(shopper, variant, quantity: 1);
        await DeliverAsync(shop, order.Id);

        var response = await shop.Staff.PostAsJsonAsync($"/api/payments/orders/{order.Id}/cash-collections", new { amount }, Cancel);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(0, await PaymentCountAsync(shop));
    }

    [Fact]
    public async Task OrderPayments_UnknownOrder_ReturnsNotFound()
    {
        var shop = await ShopAsync("pay-none");

        var read = await shop.Staff.GetAsync($"/api/payments/orders/{Guid.CreateVersion7()}", Cancel);
        var collect = await shop.Staff.PostAsJsonAsync($"/api/payments/orders/{Guid.CreateVersion7()}/cash-collections", new { amount = 10m }, Cancel);

        Assert.Equal((HttpStatusCode.NotFound, HttpStatusCode.NotFound), (read.StatusCode, collect.StatusCode));
    }

    [Fact]
    public async Task CollectCash_WithoutPaymentsManage_IsRefused()
    {
        var shop = await ShopAsync("pay-perm");
        var variant = await shop.StockedVariantAsync("Pran Juice", "JUICE-250", price: 25m, stock: 5);
        using var shopper = api.CreateTenantClient(shop.Tenant.Identifier);
        var order = await PlaceOrderAsync(shopper, variant, quantity: 1);
        await DeliverAsync(shop, order.Id);
        using var orderHandler = await StaffWithRoleAsync(shop, "orders@shop.test", "Order Handler");

        var byShopper = await shopper.PostAsJsonAsync($"/api/payments/orders/{order.Id}/cash-collections", new { amount = 25m }, Cancel);
        var byOrderHandler = await orderHandler.PostAsJsonAsync($"/api/payments/orders/{order.Id}/cash-collections", new { amount = 25m }, Cancel);

        Assert.Equal((HttpStatusCode.Unauthorized, HttpStatusCode.Forbidden), (byShopper.StatusCode, byOrderHandler.StatusCode));
        Assert.Equal(0, await PaymentCountAsync(shop));
    }

    private async Task ShipAsync(Shop shop, Guid orderId)
    {
        await ChangeStatusAsync(shop, orderId, "confirm");
        await ChangeStatusAsync(shop, orderId, "pack");
        await ChangeStatusAsync(shop, orderId, "ship");
    }

    private async Task DeliverAsync(Shop shop, Guid orderId)
    {
        await ShipAsync(shop, orderId);
        await ChangeStatusAsync(shop, orderId, "deliver");
    }

    private static async Task<OrderPaymentsResponse> CollectAsync(Shop shop, Guid orderId, decimal amount)
    {
        var response = await shop.Staff.PostAsJsonAsync($"/api/payments/orders/{orderId}/cash-collections", new { amount }, Cancel);
        await response.EnsureSuccessWithBodyAsync();
        return (await response.Content.ReadFromJsonAsync<OrderPaymentsResponse>(Cancel))!;
    }

    /// <summary>Waits for Ordering to catch up with a payment event.</summary>
    private static async Task<PaidOrderResponse> WaitForOrderAsync(Shop shop, Guid orderId, Func<PaidOrderResponse, bool> condition)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(Cancel);
        timeout.CancelAfter(EventTimeout);
        while (true)
        {
            var order = (await shop.Staff.GetFromJsonAsync<PaidOrderResponse>($"/api/ordering/orders/{orderId}", Cancel))!;
            if (condition(order))
            {
                return order;
            }

            await Task.Delay(TimeSpan.FromMilliseconds(100), timeout.Token);
        }
    }

    private async Task<int> PaymentCountAsync(Shop shop)
    {
        await using var scope = await api.CreateTenantScopeAsync(shop.Tenant.Identifier);
        return await scope.ServiceProvider.GetRequiredService<PaymentsDbContext>().Payments.CountAsync(Cancel);
    }

    private sealed record PaidOrderResponse(Guid Id, string Status, decimal Total, string PaymentStatus, decimal AmountPaid);

    private sealed record PaidOrderPage(IReadOnlyList<PaidOrderRow> Items);

    private sealed record PaidOrderRow(Guid Id, string PaymentStatus);

    private sealed record OrderPaymentsResponse(
        Guid OrderId,
        string OrderNumber,
        decimal Total,
        decimal AmountPaid,
        decimal Outstanding,
        string PaymentStatus,
        IReadOnlyList<PaymentResponse> Payments);

    private sealed record PaymentResponse(Guid Id, string Method, string Status, decimal Amount, IReadOnlyList<PaymentAttemptResponse> Attempts);

    private sealed record PaymentAttemptResponse(string Outcome, Guid? RecordedByUserId, string? Note);
}
