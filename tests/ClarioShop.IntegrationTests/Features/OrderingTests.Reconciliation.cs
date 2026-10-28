using System.Net;
using System.Net.Http.Json;
using ClarioShop.Ordering.Contracts.Events;
using ClarioShop.Payments.Contracts.Events;
using ClarioShop.Payments.Domain;
using ClarioShop.Payments.Features.Gateways;
using ClarioShop.Payments.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace ClarioShop.IntegrationTests.Features;

/// <summary>
/// The reconciliation job (run directly, at a chosen time): a payment whose IPN never came is settled exactly as the
/// IPN would; one the gateway has nothing paid for fails once the shopper stopped trying, and Ordering then cancels
/// the order and Inventory releases its stock.
/// </summary>
public sealed partial class OrderingTests
{
    /// <summary>Well past the payment window (30 minutes after the last try).</summary>
    private static readonly TimeSpan AfterWindow = TimeSpan.FromHours(1);

    [Fact]
    public async Task Reconcile_PaidButIpnNeverCame_SettlesPaymentAndConfirmsOrder()
    {
        var shop = await PrepaidShopAsync("rec-missed");
        var variant = await shop.StockedVariantAsync("Miniket Rice 5kg", "RICE-5KG", price: 450m, stock: 10);
        using var shopper = api.CreateTenantClient(shop.Tenant.Identifier);
        var order = await PlacePrepaidOrderAsync(shopper, variant, quantity: 2);
        var started = await StartPaymentAsync(shopper, order.Id);
        var validationId = api.Gateway.Pay(started.TransactionId);

        var run = await ReconcileAsync(shop, DateTimeOffset.UtcNow);

        Assert.Equal(1, run.Settled);
        var published = await api.Events.WaitForAsync<PaymentSucceeded>(e => e.OrderId == order.Id, EventTimeout);
        Assert.Equal((started.PaymentId, 900m), (published.PaymentId, published.Amount));
        var confirmed = await WaitForOrderAsync(shop, order.Id, o => o.Status == "Confirmed");
        Assert.Equal("Paid", confirmed.PaymentStatus);
        await WaitForStockAsync(shop, variant, s => s.OnHand == 8m && s.Reserved == 0m);
        var log = Assert.Single(await WebhookLogsAsync(shop));
        Assert.Equal((GatewayCallbackSource.Reconciliation, GatewayCallbackOutcome.Settled, validationId), (log.Source, log.Outcome, log.ValidationId));
    }

    [Fact]
    public async Task Reconcile_ThenIpnArrivesLate_SettlesThePaymentOnce()
    {
        var shop = await PrepaidShopAsync("rec-late-ipn");
        var variant = await shop.StockedVariantAsync("Ispahani Tea", "TEA-400G", price: 250m, stock: 5);
        using var shopper = api.CreateTenantClient(shop.Tenant.Identifier);
        var order = await PlacePrepaidOrderAsync(shopper, variant, quantity: 1);
        var started = await StartPaymentAsync(shopper, order.Id);
        var validationId = api.Gateway.Pay(started.TransactionId);

        await ReconcileAsync(shop, DateTimeOffset.UtcNow);
        var ipn = await PostIpnAsync(shop, started.TransactionId, validationId);
        var again = await ReconcileAsync(shop, DateTimeOffset.UtcNow.Add(AfterWindow));

        Assert.Equal(HttpStatusCode.OK, ipn.StatusCode);
        Assert.Equal(default, again);
        Assert.Equal([GatewayCallbackOutcome.Settled, GatewayCallbackOutcome.AlreadySettled], (await WebhookLogsAsync(shop)).Select(log => log.Outcome));
        Assert.Equal(1, await PaymentSucceededInOutboxAsync(shop, order.Id));
    }

    [Fact]
    public async Task Reconcile_ShopperCancelledAndWindowPassed_FailsPaymentCancelsOrderAndReleasesStock()
    {
        var shop = await PrepaidShopAsync("rec-cancel");
        var variant = await shop.StockedVariantAsync("Rupchanda Oil 5L", "OIL-5L", price: 899.5m, stock: 5);
        using var shopper = api.CreateTenantClient(shop.Tenant.Identifier);
        var order = await PlacePrepaidOrderAsync(shopper, variant, quantity: 2);
        var started = await StartPaymentAsync(shopper, order.Id);
        api.Gateway.Report(started.TransactionId, "FAILED", "CANCELLED");

        var run = await ReconcileAsync(shop, DateTimeOffset.UtcNow.Add(AfterWindow));

        Assert.Equal(1, run.Failed);
        var failed = await api.Events.WaitForAsync<PaymentFailed>(e => e.OrderId == order.Id, EventTimeout);
        Assert.Equal((started.PaymentId, "SslCommerz", 1799m), (failed.PaymentId, failed.Method, failed.Amount));
        var cancelled = await api.Events.WaitForAsync<OrderCancelled>(e => e.OrderId == order.Id, EventTimeout);
        Assert.Equal(("PendingPayment", "Payment failed"), (cancelled.PreviousStatus, cancelled.Reason));
        var reread = await WaitForOrderAsync(shop, order.Id, o => o.Status == "Cancelled");
        Assert.Equal("Unpaid", reread.PaymentStatus);
        await WaitForStockAsync(shop, variant, s => s.Reserved == 0m && s.OnHand == 5m);
        var payment = Assert.Single((await shop.Staff.GetFromJsonAsync<OrderPaymentsResponse>($"/api/payments/orders/{order.Id}", Cancel))!.Payments);
        Assert.Equal("Failed", payment.Status);
        var attempt = Assert.Single(payment.Attempts);
        Assert.Equal(("Failed", "SslCommerz reports FAILED, CANCELLED."), (attempt.Outcome, attempt.Note));
        var history = (await shop.Staff.GetFromJsonAsync<OrderResponse>($"/api/ordering/orders/{order.Id}", Cancel))!.History;
        Assert.StartsWith("Payment failed: ", history[^1].Note);
    }

    [Fact]
    public async Task Reconcile_ShopperNeverPaidAndWindowPassed_FailsPayment()
    {
        var shop = await PrepaidShopAsync("rec-abandon");
        var variant = await shop.StockedVariantAsync("Lux Soap", "SOAP-LUX", price: 45m, stock: 5);
        using var shopper = api.CreateTenantClient(shop.Tenant.Identifier);
        var order = await PlacePrepaidOrderAsync(shopper, variant, quantity: 1);
        await StartPaymentAsync(shopper, order.Id);

        await ReconcileAsync(shop, DateTimeOffset.UtcNow.Add(AfterWindow));

        await WaitForOrderAsync(shop, order.Id, o => o.Status == "Cancelled");
        var payment = Assert.Single((await shop.Staff.GetFromJsonAsync<OrderPaymentsResponse>($"/api/payments/orders/{order.Id}", Cancel))!.Payments);
        Assert.Equal(("Failed", "SslCommerz has no payment for this session."), (payment.Status, Assert.Single(payment.Attempts).Note));
    }

    [Fact]
    public async Task Reconcile_FailedWithinWindow_LeavesPaymentOpenSoTheShopperCanRetry()
    {
        var shop = await PrepaidShopAsync("rec-retry");
        var variant = await shop.StockedVariantAsync("Teer Sugar 1kg", "SUGAR-1KG", price: 125m, stock: 5);
        using var shopper = api.CreateTenantClient(shop.Tenant.Identifier);
        var order = await PlacePrepaidOrderAsync(shopper, variant, quantity: 2);
        var first = await StartPaymentAsync(shopper, order.Id);
        api.Gateway.Report(first.TransactionId, "CANCELLED");

        var early = await ReconcileAsync(shop, DateTimeOffset.UtcNow);
        var retry = await StartPaymentAsync(shopper, order.Id);
        api.Gateway.Pay(retry.TransactionId);
        var later = await ReconcileAsync(shop, DateTimeOffset.UtcNow.Add(AfterWindow));

        Assert.Equal((1, 0), (early.StillPending, early.Failed));
        Assert.Equal((1, 0), (later.Settled, later.Failed));
        await WaitForOrderAsync(shop, order.Id, o => o.Status == "Confirmed");
        Assert.Equal(0, api.Events.Count<PaymentFailed>(e => e.OrderId == order.Id));
    }

    [Fact]
    public async Task Reconcile_GatewayStillProcessingAfterWindow_LeavesPaymentOpen()
    {
        var shop = await PrepaidShopAsync("rec-processing");
        var variant = await shop.StockedVariantAsync("Pran Juice", "JUICE-250", price: 25m, stock: 5);
        using var shopper = api.CreateTenantClient(shop.Tenant.Identifier);
        var order = await PlacePrepaidOrderAsync(shopper, variant, quantity: 2);
        var started = await StartPaymentAsync(shopper, order.Id);
        api.Gateway.Report(started.TransactionId, "FAILED", "PENDING");

        var run = await ReconcileAsync(shop, DateTimeOffset.UtcNow.Add(AfterWindow));

        Assert.Equal(1, run.StillPending);
        await AssertStillUnpaidAsync(shop, order.Id);
    }

    [Fact]
    public async Task Reconcile_GatewayUnavailable_ChangesNothingAndTriesAgainNextRun()
    {
        var shop = await PrepaidShopAsync("rec-down");
        var variant = await shop.StockedVariantAsync("Salt", "SALT-1KG", price: 42m, stock: 5);
        using var shopper = api.CreateTenantClient(shop.Tenant.Identifier);
        var order = await PlacePrepaidOrderAsync(shopper, variant, quantity: 1);
        var started = await StartPaymentAsync(shopper, order.Id);
        api.Gateway.FailLookups(started.TransactionId, times: 1);

        var down = await ReconcileAsync(shop, DateTimeOffset.UtcNow.Add(AfterWindow));
        await AssertStillUnpaidAsync(shop, order.Id);
        var back = await ReconcileAsync(shop, DateTimeOffset.UtcNow.Add(AfterWindow));

        Assert.Equal((1, 0), (down.Unavailable, down.Failed));
        Assert.Equal(1, back.Failed);
    }

    [Fact]
    public async Task Reconcile_PaidAmountDiffers_NeverFailsThePayment()
    {
        var shop = await PrepaidShopAsync("rec-amount");
        var variant = await shop.StockedVariantAsync("Eggs (dozen)", "EGG-12", price: 140m, stock: 5);
        using var shopper = api.CreateTenantClient(shop.Tenant.Identifier);
        var order = await PlacePrepaidOrderAsync(shopper, variant, quantity: 1);
        var started = await StartPaymentAsync(shopper, order.Id);
        api.Gateway.Pay(started.TransactionId, amount: 10m);

        var run = await ReconcileAsync(shop, DateTimeOffset.UtcNow.Add(AfterWindow));

        Assert.Equal((1, 0), (run.StillPending, run.Failed));
        var log = Assert.Single(await WebhookLogsAsync(shop));
        Assert.Equal((GatewayCallbackOutcome.Rejected, "payments.amount_mismatch"), (log.Outcome, log.Detail));
        await AssertStillUnpaidAsync(shop, order.Id);
    }

    [Fact]
    public async Task Reconcile_RunConcurrently_FailsThePaymentOnce()
    {
        const int Runs = 6;
        var shop = await PrepaidShopAsync("rec-race");
        var variant = await shop.StockedVariantAsync("Fresh Atta 2kg", "ATTA-2KG", price: 130m, stock: 5);
        using var shopper = api.CreateTenantClient(shop.Tenant.Identifier);
        var order = await PlacePrepaidOrderAsync(shopper, variant, quantity: 1);
        var started = await StartPaymentAsync(shopper, order.Id);
        api.Gateway.Report(started.TransactionId, "CANCELLED");
        var now = DateTimeOffset.UtcNow.Add(AfterWindow);

        var runs = await Task.WhenAll(Enumerable.Range(0, Runs).Select(_ => ReconcileAsync(shop, now)));

        Assert.Equal(1, runs.Sum(run => run.Failed));
        Assert.Equal(1, await OutboxCountAsync(shop, nameof(PaymentFailed), order.Id));
    }

    [Fact]
    public async Task StartGatewayPayment_AfterPaymentFailed_ReturnsConflictAndOpensNoNewPayment()
    {
        var shop = await PrepaidShopAsync("rec-restart");
        var variant = await shop.StockedVariantAsync("Ilish (1kg)", "ILISH-1KG", price: 1500m, stock: 2);
        using var shopper = api.CreateTenantClient(shop.Tenant.Identifier);
        var order = await PlacePrepaidOrderAsync(shopper, variant, quantity: 1);
        await StartPaymentAsync(shopper, order.Id);
        await ReconcileAsync(shop, DateTimeOffset.UtcNow.Add(AfterWindow));

        var response = await shopper.PostAsync($"/api/payments/orders/{order.Id}/gateway-sessions", null, Cancel);

        // Before Ordering has cancelled the order it is payments.payment_failed; after, payments.not_awaiting_payment.
        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Contains(await ProblemCodeAsync(response), new[] { "payments.payment_failed", "payments.not_awaiting_payment" });
        Assert.Equal(1, await PaymentCountAsync(shop));
    }

    [Fact]
    public async Task Ipn_AfterPaymentFailed_SettlesItAndLeavesTheCancelledOrderForARefund()
    {
        var shop = await PrepaidShopAsync("rec-too-late");
        var variant = await shop.StockedVariantAsync("Miniket Rice 5kg", "RICE-5KG", price: 450m, stock: 5);
        using var shopper = api.CreateTenantClient(shop.Tenant.Identifier);
        var order = await PlacePrepaidOrderAsync(shopper, variant, quantity: 1);
        var started = await StartPaymentAsync(shopper, order.Id);
        await ReconcileAsync(shop, DateTimeOffset.UtcNow.Add(AfterWindow));
        await WaitForOrderAsync(shop, order.Id, o => o.Status == "Cancelled");

        await PostIpnAsync(shop, started.TransactionId, api.Gateway.Pay(started.TransactionId));

        var paid = await WaitForOrderAsync(shop, order.Id, o => o.PaymentStatus == "Paid");
        Assert.Equal("Cancelled", paid.Status);
        var payment = Assert.Single((await shop.Staff.GetFromJsonAsync<OrderPaymentsResponse>($"/api/payments/orders/{order.Id}", Cancel))!.Payments);
        Assert.Equal("Succeeded", payment.Status);
    }

    [Fact]
    public async Task PaymentFailed_OrderCancelledByStaffFirst_IsIgnored()
    {
        var shop = await PrepaidShopAsync("rec-staff");
        var variant = await shop.StockedVariantAsync("Lux Soap", "SOAP-LUX", price: 45m, stock: 5);
        using var shopper = api.CreateTenantClient(shop.Tenant.Identifier);
        var order = await PlacePrepaidOrderAsync(shopper, variant, quantity: 1);
        await StartPaymentAsync(shopper, order.Id);
        await ChangeStatusAsync(shop, order.Id, "cancel");

        await ReconcileAsync(shop, DateTimeOffset.UtcNow.Add(AfterWindow));
        var failed = await api.Events.WaitForAsync<PaymentFailed>(e => e.OrderId == order.Id, EventTimeout);

        // Delivered again under a new message id, so Ordering handles it now rather than skipping it as a duplicate.
        await RedeliverAsync(failed);

        Assert.Equal(1, await OutboxCountAsync(shop, nameof(OrderCancelled), order.Id, ordering: true));
        var history = (await shop.Staff.GetFromJsonAsync<OrderResponse>($"/api/ordering/orders/{order.Id}", Cancel))!.History;
        Assert.Equal(["PendingPayment", "Cancelled"], history.Select(change => change.ToStatus));
    }

    private async Task<ReconciliationRun> ReconcileAsync(Shop shop, DateTimeOffset now)
    {
        await using var scope = await api.CreateTenantScopeAsync(shop.Tenant.Identifier);
        return await scope.ServiceProvider.GetRequiredService<GatewayReconciliation>().ReconcileAsync(now, Cancel);
    }

    /// <summary>Events of a type a module wrote for the order, delivered or not.</summary>
    private async Task<int> OutboxCountAsync(Shop shop, string eventType, Guid orderId, bool ordering = false)
    {
        await using var scope = await api.CreateTenantScopeAsync(shop.Tenant.Identifier);
        var outbox = ordering
            ? scope.ServiceProvider.GetRequiredService<ClarioShop.Ordering.Persistence.OrderingDbContext>().OutboxMessages
            : scope.ServiceProvider.GetRequiredService<PaymentsDbContext>().OutboxMessages;
        var messages = await outbox.AsNoTracking().Select(message => new { message.Type, message.Content }).ToListAsync(Cancel);
        return messages.Count(message => message.Type.Contains(eventType, StringComparison.Ordinal)
            && message.Content.Contains(orderId.ToString(), StringComparison.OrdinalIgnoreCase));
    }
}
