using System.Net;
using System.Net.Http.Json;
using ClarioShop.IntegrationTests.Infrastructure;
using ClarioShop.Ordering.Contracts.Events;
using ClarioShop.Ordering.Persistence;
using ClarioShop.Payments.Contracts.Events;
using ClarioShop.Payments.Domain;
using ClarioShop.Payments.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace ClarioShop.IntegrationTests.Features;

/// <summary>
/// Prepaid checkout with SSLCommerz (a fake gateway): the order waits in PendingPayment, Payments opens a session, and
/// the IPN settles the payment once the gateway's validation API confirms it; Ordering then confirms the order and
/// Inventory sells the stock.
/// </summary>
public sealed partial class OrderingTests
{
    [Fact]
    public async Task Checkout_SslCommerz_PlacesPendingPaymentOrderAndReservesStock()
    {
        var shop = await PrepaidShopAsync("pre-place");
        var variant = await shop.StockedVariantAsync("Miniket Rice 5kg", "RICE-5KG", price: 450m, stock: 10);
        using var shopper = api.CreateTenantClient(shop.Tenant.Identifier);

        var order = await PlacePrepaidOrderAsync(shopper, variant, quantity: 2);

        Assert.Equal(("PendingPayment", "SslCommerz", 900m), (order.Status, order.PaymentMethod, order.Total));
        Assert.Equal("PendingPayment", Assert.Single(order.History).ToStatus);
        Assert.Equal(2m, (await shop.StockAsync(variant)).Reserved);
        var placed = await api.Events.WaitForAsync<OrderPlaced>(e => e.OrderId == order.Id, EventTimeout);
        Assert.Equal("SslCommerz", placed.PaymentMethod);
    }

    [Fact]
    public async Task Checkout_SslCommerzWithoutMerchantAccount_ReturnsConflictAndReservesNothing()
    {
        var shop = await ShopAsync("pre-noacct");
        var variant = await shop.StockedVariantAsync("Teer Sugar 1kg", "SUGAR-1KG", price: 125m, stock: 5);
        using var shopper = api.CreateTenantClient(shop.Tenant.Identifier);
        var cartId = await NewCartAsync(shopper);
        await AddItemAsync(shopper, cartId, variant, 1);

        var response = await PrepaidCheckoutAsync(shopper, cartId);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal("ordering.payment_method_unavailable", await ProblemCodeAsync(response));
        Assert.Equal(0m, (await shop.StockAsync(variant)).Reserved);
        Assert.Equal(0, await CountOrdersAsync(shop.Tenant));
    }

    [Fact]
    public async Task Checkout_UnknownPaymentMethod_ReturnsValidationProblem()
    {
        var shop = await PrepaidShopAsync("pre-badmethod");
        var variant = await shop.StockedVariantAsync("Lux Soap", "SOAP-LUX", price: 45m, stock: 5);
        using var shopper = api.CreateTenantClient(shop.Tenant.Identifier);
        var cartId = await NewCartAsync(shopper);
        await AddItemAsync(shopper, cartId, variant, 1);

        var response = await PrepaidCheckoutAsync(shopper, cartId, paymentMethod: "Bitcoin");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(0, await CountOrdersAsync(shop.Tenant));
    }

    [Fact]
    public async Task StartGatewayPayment_PendingOrder_OpensSessionForOrderTotalWithCallbacksOnStoreHost()
    {
        var shop = await PrepaidShopAsync("pre-session");
        var rice = await shop.StockedVariantAsync("Miniket Rice 5kg", "RICE-5KG", price: 450m, stock: 10);
        var oil = await shop.StockedVariantAsync("Rupchanda Oil 5L", "OIL-5L", price: 899.5m, stock: 10);
        using var shopper = api.CreateTenantClient(shop.Tenant.Identifier);
        var cartId = await NewCartAsync(shopper);
        await AddItemAsync(shopper, cartId, rice, 1);
        await AddItemAsync(shopper, cartId, oil, 1);
        var order = (await (await PrepaidCheckoutAsync(shopper, cartId)).Content.ReadFromJsonAsync<OrderResponse>(Cancel))!;

        var response = await shopper.PostAsync($"/api/payments/orders/{order.Id}/gateway-sessions", null, Cancel);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var started = (await response.Content.ReadFromJsonAsync<SessionResponse>(Cancel))!;
        Assert.Equal((1349.5m, new Uri($"https://sandbox.sslcommerz.test/pay/{started.TransactionId}")), (started.Amount, started.RedirectUrl));
        var session = api.Gateway.SessionFor(started.TransactionId);
        Assert.Equal((1349.5m, "BDT", order.Number, 2), (session.Amount, session.Currency, session.OrderNumber, session.ItemCount));
        Assert.Equal(("Rahim Uddin", "+8801712345678", "House 12, Road 5, Dhanmondi", "Dhaka", "1205"), (session.Customer.Name, session.Customer.Phone, session.Customer.AddressLine, session.Customer.City, session.Customer.PostalCode));
        Assert.Equal("http://localhost/api/payments/sslcommerz/ipn", session.Callbacks.Ipn.AbsoluteUri);
        Assert.Equal("http://localhost/api/payments/sslcommerz/success", session.Callbacks.Success.AbsoluteUri);
        var payments = await shop.Staff.GetFromJsonAsync<OrderPaymentsResponse>($"/api/payments/orders/{order.Id}", Cancel);
        var payment = Assert.Single(payments!.Payments);
        Assert.Equal((started.PaymentId, "SslCommerz", "Pending", 1349.5m), (payment.Id, payment.Method, payment.Status, payment.Amount));
        Assert.Equal("Pending", Assert.Single(payment.Attempts).Outcome);
        Assert.Equal(("Unpaid", 0m), (payments.PaymentStatus, payments.AmountPaid));
    }

    [Fact]
    public async Task StartGatewayPayment_Again_OpensNewAttemptForSamePayment()
    {
        var shop = await PrepaidShopAsync("pre-retry");
        var variant = await shop.StockedVariantAsync("Ispahani Tea", "TEA-400G", price: 250m, stock: 5);
        using var shopper = api.CreateTenantClient(shop.Tenant.Identifier);
        var order = await PlacePrepaidOrderAsync(shopper, variant, quantity: 1);

        var first = await StartPaymentAsync(shopper, order.Id);
        var second = await StartPaymentAsync(shopper, order.Id);

        Assert.Equal(first.PaymentId, second.PaymentId);
        Assert.NotEqual(first.TransactionId, second.TransactionId);
        var payment = Assert.Single((await shop.Staff.GetFromJsonAsync<OrderPaymentsResponse>($"/api/payments/orders/{order.Id}", Cancel))!.Payments);
        Assert.Equal(2, payment.Attempts.Count);
    }

    [Fact]
    public async Task StartGatewayPayment_NotAPendingPrepaidOrder_ReturnsConflictOrNotFound()
    {
        var shop = await PrepaidShopAsync("pre-wrong");
        var variant = await shop.StockedVariantAsync("Pran Juice", "JUICE-250", price: 25m, stock: 5);
        using var shopper = api.CreateTenantClient(shop.Tenant.Identifier);
        var cashOrder = await PlaceOrderAsync(shopper, variant, quantity: 1);
        var cancelled = await PlacePrepaidOrderAsync(shopper, variant, quantity: 1);
        await ChangeStatusAsync(shop, cancelled.Id, "cancel");

        var forCash = await shopper.PostAsync($"/api/payments/orders/{cashOrder.Id}/gateway-sessions", null, Cancel);
        var forCancelled = await shopper.PostAsync($"/api/payments/orders/{cancelled.Id}/gateway-sessions", null, Cancel);
        var forUnknown = await shopper.PostAsync($"/api/payments/orders/{Guid.CreateVersion7()}/gateway-sessions", null, Cancel);

        Assert.Equal((HttpStatusCode.Conflict, "payments.not_prepaid"), (forCash.StatusCode, await ProblemCodeAsync(forCash)));
        Assert.Equal((HttpStatusCode.Conflict, "payments.not_awaiting_payment"), (forCancelled.StatusCode, await ProblemCodeAsync(forCancelled)));
        Assert.Equal(HttpStatusCode.NotFound, forUnknown.StatusCode);
        Assert.Equal(0, await PaymentCountAsync(shop));
    }

    [Fact]
    public async Task StartGatewayPayment_GatewayRefuses_ReturnsUnavailableAndRecordsFailedAttempt()
    {
        var shop = await PrepaidShopAsync("pre-refused");
        var variant = await shop.StockedVariantAsync("Salt", "SALT-1KG", price: 42m, stock: 5);
        using var shopper = api.CreateTenantClient(shop.Tenant.Identifier);
        var customer = $"Refused {shop.Tenant.Identifier}";
        api.Gateway.RefuseSessionsFor(customer);
        var cartId = await NewCartAsync(shopper);
        await AddItemAsync(shopper, cartId, variant, 1);
        var order = (await (await PrepaidCheckoutAsync(shopper, cartId, name: customer)).Content.ReadFromJsonAsync<OrderResponse>(Cancel))!;

        var response = await shopper.PostAsync($"/api/payments/orders/{order.Id}/gateway-sessions", null, Cancel);

        Assert.Equal((HttpStatusCode.ServiceUnavailable, "payments.gateway_refused"), (response.StatusCode, await ProblemCodeAsync(response)));
        var payment = Assert.Single((await shop.Staff.GetFromJsonAsync<OrderPaymentsResponse>($"/api/payments/orders/{order.Id}", Cancel))!.Payments);
        Assert.Equal("Pending", payment.Status);
        Assert.Equal(("Failed", "payments.gateway_refused"), (Assert.Single(payment.Attempts).Outcome, payment.Attempts[0].Note));
    }

    [Fact]
    public async Task Ipn_PaymentConfirmedByGateway_SettlesPaymentConfirmsOrderAndSellsStock()
    {
        var shop = await PrepaidShopAsync("pre-ipn");
        var variant = await shop.StockedVariantAsync("Miniket Rice 5kg", "RICE-5KG", price: 450m, stock: 10);
        using var shopper = api.CreateTenantClient(shop.Tenant.Identifier);
        var order = await PlacePrepaidOrderAsync(shopper, variant, quantity: 2);
        var started = await StartPaymentAsync(shopper, order.Id);
        var validationId = api.Gateway.Pay(started.TransactionId);

        var response = await PostIpnAsync(shop, started.TransactionId, validationId);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var published = await api.Events.WaitForAsync<PaymentSucceeded>(e => e.OrderId == order.Id, EventTimeout);
        Assert.Equal((started.PaymentId, "SslCommerz", 900m, order.Number), (published.PaymentId, published.Method, published.Amount, published.OrderNumber));
        var confirmed = await WaitForOrderAsync(shop, order.Id, o => o.Status == "Confirmed");
        Assert.Equal(("Paid", 900m), (confirmed.PaymentStatus, confirmed.AmountPaid));
        await api.Events.WaitForAsync<OrderConfirmed>(e => e.OrderId == order.Id, EventTimeout);
        Assert.Equal(1, await OrderConfirmedInOutboxAsync(shop, order.Id));
        var stock = await WaitForStockAsync(shop, variant, s => s.OnHand == 8m);
        Assert.Equal(0m, stock.Reserved);
        var payment = Assert.Single((await shop.Staff.GetFromJsonAsync<OrderPaymentsResponse>($"/api/payments/orders/{order.Id}", Cancel))!.Payments);
        Assert.Equal(("Succeeded", "Succeeded"), (payment.Status, Assert.Single(payment.Attempts).Outcome));
        var log = Assert.Single(await WebhookLogsAsync(shop));
        Assert.Equal((GatewayCallbackSource.Ipn, GatewayCallbackOutcome.Settled, started.PaymentId), (log.Source, log.Outcome, log.PaymentId));
        Assert.Contains(validationId, log.Payload);
    }

    [Fact]
    public async Task Ipn_SentAgainAndShopperReturns_SettlesThePaymentOnce()
    {
        var shop = await PrepaidShopAsync("pre-again");
        var variant = await shop.StockedVariantAsync("Rupchanda Oil 5L", "OIL-5L", price: 899.5m, stock: 5);
        using var shopper = api.CreateTenantClient(shop.Tenant.Identifier);
        var order = await PlacePrepaidOrderAsync(shopper, variant, quantity: 1);
        var started = await StartPaymentAsync(shopper, order.Id);
        var validationId = api.Gateway.Pay(started.TransactionId);

        var returned = await PostCallbackAsync(shop, "success", started.TransactionId, validationId);
        await PostIpnAsync(shop, started.TransactionId, validationId);
        await PostIpnAsync(shop, started.TransactionId, validationId);

        Assert.Equal(HttpStatusCode.SeeOther, returned.StatusCode);
        Assert.Equal($"/checkout/payment?orderId={order.Id}&payment=paid", returned.Headers.Location?.OriginalString);
        await WaitForOrderAsync(shop, order.Id, o => o.Status == "Confirmed");
        Assert.Equal(1, await PaymentSucceededInOutboxAsync(shop, order.Id));
        var outcomes = (await WebhookLogsAsync(shop)).Select(log => log.Outcome).ToList();
        Assert.Equal([GatewayCallbackOutcome.Settled, GatewayCallbackOutcome.AlreadySettled, GatewayCallbackOutcome.AlreadySettled], outcomes);
    }

    [Fact]
    public async Task Ipn_DeliveredConcurrently_SettlesThePaymentOnce()
    {
        const int Deliveries = 6;
        var shop = await PrepaidShopAsync("pre-race");
        var variant = await shop.StockedVariantAsync("Fresh Atta 2kg", "ATTA-2KG", price: 130m, stock: 5);
        using var shopper = api.CreateTenantClient(shop.Tenant.Identifier);
        var order = await PlacePrepaidOrderAsync(shopper, variant, quantity: 2);
        var started = await StartPaymentAsync(shopper, order.Id);
        var validationId = api.Gateway.Pay(started.TransactionId);

        var responses = await Task.WhenAll(Enumerable.Range(0, Deliveries).Select(_ => PostIpnAsync(shop, started.TransactionId, validationId)));

        Assert.All(responses, response => Assert.Equal(HttpStatusCode.OK, response.StatusCode));
        var outcomes = (await WebhookLogsAsync(shop)).Select(log => log.Outcome).ToList();
        Assert.Single(outcomes, outcome => outcome == GatewayCallbackOutcome.Settled);
        Assert.Equal(Deliveries - 1, outcomes.Count(outcome => outcome == GatewayCallbackOutcome.AlreadySettled));
        Assert.Equal(1, await PaymentSucceededInOutboxAsync(shop, order.Id));
    }

    [Fact]
    public async Task Ipn_NotConfirmedByGateway_ChangesNothing()
    {
        var shop = await PrepaidShopAsync("pre-forged");
        var variant = await shop.StockedVariantAsync("Eggs (dozen)", "EGG-12", price: 140m, stock: 5);
        using var shopper = api.CreateTenantClient(shop.Tenant.Identifier);
        var order = await PlacePrepaidOrderAsync(shopper, variant, quantity: 1);
        var started = await StartPaymentAsync(shopper, order.Id);

        // A forged notification: the shopper never paid, so the gateway does not know this validation id.
        var response = await PostIpnAsync(shop, started.TransactionId, "VAL-FORGED");
        var returned = await PostCallbackAsync(shop, "success", started.TransactionId, "VAL-FORGED");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal($"/checkout/payment?orderId={order.Id}&payment=pending", returned.Headers.Location?.OriginalString);
        Assert.All(await WebhookLogsAsync(shop), log => Assert.Equal((GatewayCallbackOutcome.Rejected, "payments.not_confirmed_by_gateway"), (log.Outcome, log.Detail)));
        await AssertStillUnpaidAsync(shop, order.Id);
    }

    [Fact]
    public async Task Ipn_PaidAmountDiffers_ChangesNothing()
    {
        var shop = await PrepaidShopAsync("pre-amount");
        var variant = await shop.StockedVariantAsync("Ispahani Tea", "TEA-400G", price: 250m, stock: 5);
        using var shopper = api.CreateTenantClient(shop.Tenant.Identifier);
        var order = await PlacePrepaidOrderAsync(shopper, variant, quantity: 2);
        var started = await StartPaymentAsync(shopper, order.Id);
        var validationId = api.Gateway.Pay(started.TransactionId, amount: 10m);

        await PostIpnAsync(shop, started.TransactionId, validationId);

        var log = Assert.Single(await WebhookLogsAsync(shop));
        Assert.Equal((GatewayCallbackOutcome.Rejected, "payments.amount_mismatch"), (log.Outcome, log.Detail));
        await AssertStillUnpaidAsync(shop, order.Id);
    }

    [Fact]
    public async Task Ipn_FlaggedRiskyByGateway_HoldsPaymentForReview()
    {
        var shop = await PrepaidShopAsync("pre-risky");
        var variant = await shop.StockedVariantAsync("Lux Soap", "SOAP-LUX", price: 45m, stock: 5);
        using var shopper = api.CreateTenantClient(shop.Tenant.Identifier);
        var order = await PlacePrepaidOrderAsync(shopper, variant, quantity: 1);
        var started = await StartPaymentAsync(shopper, order.Id);
        var validationId = api.Gateway.Pay(started.TransactionId, risky: true);

        await PostIpnAsync(shop, started.TransactionId, validationId);

        Assert.Equal("payments.held_for_review", Assert.Single(await WebhookLogsAsync(shop)).Detail);
        await AssertStillUnpaidAsync(shop, order.Id);
    }

    [Fact]
    public async Task Ipn_ValidatedTransactionIsAnotherOne_ChangesNothing()
    {
        var shop = await PrepaidShopAsync("pre-swap");
        var variant = await shop.StockedVariantAsync("Teer Sugar 1kg", "SUGAR-1KG", price: 125m, stock: 5);
        using var shopper = api.CreateTenantClient(shop.Tenant.Identifier);
        var cheap = await PlacePrepaidOrderAsync(shopper, variant, quantity: 1);
        var dear = await PlacePrepaidOrderAsync(shopper, variant, quantity: 3);
        var cheapPayment = await StartPaymentAsync(shopper, cheap.Id);
        var dearPayment = await StartPaymentAsync(shopper, dear.Id);

        // A real payment for the cheap order, replayed as if it were for the dear one.
        var validationId = api.Gateway.Pay(cheapPayment.TransactionId);
        await PostIpnAsync(shop, dearPayment.TransactionId, validationId);

        var log = Assert.Single(await WebhookLogsAsync(shop));
        Assert.Equal((GatewayCallbackOutcome.Rejected, "payments.transaction_mismatch"), (log.Outcome, log.Detail));
        await AssertStillUnpaidAsync(shop, dear.Id);
    }

    [Fact]
    public async Task Ipn_GatewayUnavailable_AsksForRetryAndSettlesOnRetry()
    {
        var shop = await PrepaidShopAsync("pre-down");
        var variant = await shop.StockedVariantAsync("Pran Juice", "JUICE-250", price: 25m, stock: 5);
        using var shopper = api.CreateTenantClient(shop.Tenant.Identifier);
        var order = await PlacePrepaidOrderAsync(shopper, variant, quantity: 4);
        var started = await StartPaymentAsync(shopper, order.Id);
        var validationId = api.Gateway.Pay(started.TransactionId);
        api.Gateway.FailValidations(validationId, times: 1);

        var first = await PostIpnAsync(shop, started.TransactionId, validationId);
        var retried = await PostIpnAsync(shop, started.TransactionId, validationId);

        Assert.Equal((HttpStatusCode.ServiceUnavailable, HttpStatusCode.OK), (first.StatusCode, retried.StatusCode));
        Assert.Equal([GatewayCallbackOutcome.GatewayUnavailable, GatewayCallbackOutcome.Settled], (await WebhookLogsAsync(shop)).Select(log => log.Outcome));
        await WaitForOrderAsync(shop, order.Id, o => o.Status == "Confirmed");
    }

    [Theory]
    [InlineData("FAILED")]
    [InlineData("CANCELLED")]
    public async Task Ipn_PaymentNotMade_IsLoggedAndChangesNothing(string status)
    {
        var shop = await PrepaidShopAsync("pre-failed");
        var variant = await shop.StockedVariantAsync("Salt", "SALT-1KG", price: 42m, stock: 5);
        using var shopper = api.CreateTenantClient(shop.Tenant.Identifier);
        var order = await PlacePrepaidOrderAsync(shopper, variant, quantity: 1);
        var started = await StartPaymentAsync(shopper, order.Id);

        var response = await PostIpnAsync(shop, started.TransactionId, validationId: null, status);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var log = Assert.Single(await WebhookLogsAsync(shop));
        Assert.Equal((GatewayCallbackOutcome.NotPaid, status, started.PaymentId), (log.Outcome, log.ReportedStatus, log.PaymentId));
        await AssertStillUnpaidAsync(shop, order.Id);
    }

    [Fact]
    public async Task Ipn_UnknownTransaction_IsLoggedAndIgnored()
    {
        var shop = await PrepaidShopAsync("pre-unknown");

        var response = await PostIpnAsync(shop, Guid.CreateVersion7(), "VAL-NOBODY");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(GatewayCallbackOutcome.UnknownPayment, Assert.Single(await WebhookLogsAsync(shop)).Outcome);
    }

    [Fact]
    public async Task Ipn_OversizedForm_IsRefusedAndNotStored()
    {
        var shop = await PrepaidShopAsync("pre-big");
        using var client = api.CreateTenantClient(shop.Tenant.Identifier);
        using var form = new FormUrlEncodedContent(new Dictionary<string, string> { ["tran_id"] = Guid.CreateVersion7().ToString(), ["junk"] = new string('x', 100_000) });

        var response = await client.PostAsync("/api/payments/sslcommerz/ipn", form, Cancel);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Empty(await WebhookLogsAsync(shop));
    }

    [Theory]
    [InlineData("fail", "failed")]
    [InlineData("cancel", "cancelled")]
    public async Task ShopperReturns_WithoutPaying_RedirectsToStorefrontAndChangesNothing(string route, string result)
    {
        var shop = await PrepaidShopAsync("pre-back");
        var variant = await shop.StockedVariantAsync("Lux Soap", "SOAP-LUX", price: 45m, stock: 5);
        using var shopper = api.CreateTenantClient(shop.Tenant.Identifier);
        var order = await PlacePrepaidOrderAsync(shopper, variant, quantity: 1);
        var started = await StartPaymentAsync(shopper, order.Id);

        var returned = await PostCallbackAsync(shop, route, started.TransactionId, validationId: null, status: route == "fail" ? "FAILED" : "CANCELLED");

        Assert.Equal(HttpStatusCode.SeeOther, returned.StatusCode);
        Assert.Equal($"/checkout/payment?orderId={order.Id}&payment={result}", returned.Headers.Location?.OriginalString);
        await AssertStillUnpaidAsync(shop, order.Id);
    }

    [Fact]
    public async Task Ipn_AfterStockHoldExpired_ReservesAgainAndConfirms()
    {
        var shop = await PrepaidShopAsync("pre-late");
        var variant = await shop.StockedVariantAsync("Miniket Rice 5kg", "RICE-5KG", price: 450m, stock: 5);
        using var shopper = api.CreateTenantClient(shop.Tenant.Identifier);
        var order = await PlacePrepaidOrderAsync(shopper, variant, quantity: 2);
        var started = await StartPaymentAsync(shopper, order.Id);
        await ExpireReservationsAsync(shop, order.Id);

        await PostIpnAsync(shop, started.TransactionId, api.Gateway.Pay(started.TransactionId));

        await WaitForOrderAsync(shop, order.Id, o => o.Status == "Confirmed");
        var stock = await WaitForStockAsync(shop, variant, s => s.OnHand == 3m);
        Assert.Equal(0m, stock.Reserved);
    }

    [Fact]
    public async Task Ipn_AfterStockHoldExpiredAndStockSoldElsewhere_RecordsPaymentButLeavesOrderPending()
    {
        var shop = await PrepaidShopAsync("pre-gone");
        var variant = await shop.StockedVariantAsync("Ilish (1kg)", "ILISH-1KG", price: 1500m, stock: 1);
        using var shopper = api.CreateTenantClient(shop.Tenant.Identifier);
        var order = await PlacePrepaidOrderAsync(shopper, variant, quantity: 1);
        var started = await StartPaymentAsync(shopper, order.Id);
        await ExpireReservationsAsync(shop, order.Id);
        await PlaceOrderAsync(shopper, variant, quantity: 1);

        await PostIpnAsync(shop, started.TransactionId, api.Gateway.Pay(started.TransactionId));

        var paid = await WaitForOrderAsync(shop, order.Id, o => o.PaymentStatus == "Paid");
        Assert.Equal(("PendingPayment", 1500m), (paid.Status, paid.AmountPaid));
        Assert.Equal(0, await OrderConfirmedInOutboxAsync(shop, order.Id));
    }

    [Fact]
    public async Task Confirm_PendingPaymentOrder_ReturnsConflictButCancelReleasesStock()
    {
        var shop = await PrepaidShopAsync("pre-staff");
        var variant = await shop.StockedVariantAsync("Teer Sugar 1kg", "SUGAR-1KG", price: 125m, stock: 5);
        using var shopper = api.CreateTenantClient(shop.Tenant.Identifier);
        var order = await PlacePrepaidOrderAsync(shopper, variant, quantity: 2);

        var confirm = await shop.Staff.PostAsJsonAsync($"/api/ordering/orders/{order.Id}/confirm", new { note = (string?)null }, Cancel);
        var cancelled = await ChangeStatusAsync(shop, order.Id, "cancel");

        Assert.Equal((HttpStatusCode.Conflict, "ordering.awaiting_payment"), (confirm.StatusCode, await ProblemCodeAsync(confirm)));
        Assert.Equal("Cancelled", cancelled.Status);
        await WaitForStockAsync(shop, variant, s => s.Reserved == 0m);
    }

    /// <summary>A shop with an SSLCommerz merchant account switched on.</summary>
    private async Task<Shop> PrepaidShopAsync(string prefix)
    {
        var shop = await ShopAsync(prefix);
        var response = await shop.Staff.PutAsJsonAsync(
            "/api/settings/payment-gateways/sslcommerz",
            new { storeId = "clario0test", storePassword = "sandbox-pass", useSandbox = true, isEnabled = true },
            Cancel);
        await response.EnsureSuccessWithBodyAsync();
        return shop;
    }

    private static Task<HttpResponseMessage> PrepaidCheckoutAsync(HttpClient shopper, Guid cartId, string paymentMethod = "SslCommerz", string name = "Rahim Uddin") =>
        shopper.PostAsJsonAsync("/api/ordering/checkout", new
        {
            cartId,
            phone = Phone,
            name,
            address = new { line1 = "House 12, Road 5", area = "Dhanmondi", city = "Dhaka", postalCode = "1205" },
            paymentMethod,
        }, Cancel);

    private async Task<OrderResponse> PlacePrepaidOrderAsync(HttpClient shopper, Guid variantId, decimal quantity)
    {
        var cartId = await NewCartAsync(shopper);
        await AddItemAsync(shopper, cartId, variantId, quantity);
        var response = await PrepaidCheckoutAsync(shopper, cartId);
        await response.EnsureSuccessWithBodyAsync();
        return (await response.Content.ReadFromJsonAsync<OrderResponse>(Cancel))!;
    }

    private static async Task<SessionResponse> StartPaymentAsync(HttpClient shopper, Guid orderId)
    {
        var response = await shopper.PostAsync($"/api/payments/orders/{orderId}/gateway-sessions", null, Cancel);
        await response.EnsureSuccessWithBodyAsync();
        return (await response.Content.ReadFromJsonAsync<SessionResponse>(Cancel))!;
    }

    private Task<HttpResponseMessage> PostIpnAsync(Shop shop, Guid transactionId, string? validationId, string status = "VALID") =>
        PostCallbackAsync(shop, "ipn", transactionId, validationId, status);

    /// <summary>Posts form fields the way SSLCommerz does, anonymously, on the shop's host.</summary>
    private async Task<HttpResponseMessage> PostCallbackAsync(Shop shop, string route, Guid transactionId, string? validationId, string status = "VALID")
    {
        var fields = new Dictionary<string, string>
        {
            ["tran_id"] = transactionId.ToString(),
            ["status"] = status,
            ["amount"] = "0.00",
            ["currency"] = "BDT",
            ["value_a"] = "CS-000001",
        };
        if (validationId is not null)
        {
            fields["val_id"] = validationId;
        }

        using var client = api.CreateClient(new Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        client.DefaultRequestHeaders.Add("X-Tenant", shop.Tenant.Identifier);
        using var form = new FormUrlEncodedContent(fields);
        return await client.PostAsync($"/api/payments/sslcommerz/{route}", form, Cancel);
    }

    private async Task AssertStillUnpaidAsync(Shop shop, Guid orderId)
    {
        var order = await shop.Staff.GetFromJsonAsync<PaidOrderResponse>($"/api/ordering/orders/{orderId}", Cancel);
        Assert.Equal(("PendingPayment", "Unpaid"), (order!.Status, order.PaymentStatus));
        var payment = Assert.Single((await shop.Staff.GetFromJsonAsync<OrderPaymentsResponse>($"/api/payments/orders/{orderId}", Cancel))!.Payments);
        Assert.Equal("Pending", payment.Status);
        Assert.Equal(0, await PaymentSucceededInOutboxAsync(shop, orderId));
    }

    private async Task<List<GatewayWebhookLog>> WebhookLogsAsync(Shop shop)
    {
        await using var scope = await api.CreateTenantScopeAsync(shop.Tenant.Identifier);
        return await scope.ServiceProvider.GetRequiredService<PaymentsDbContext>().GatewayWebhookLogs.AsNoTracking()
            .OrderBy(log => log.ReceivedAt).ThenBy(log => log.Id)
            .ToListAsync(Cancel);
    }

    /// <summary>PaymentSucceeded events Payments wrote for the order, delivered or not.</summary>
    private async Task<int> PaymentSucceededInOutboxAsync(Shop shop, Guid orderId)
    {
        await using var scope = await api.CreateTenantScopeAsync(shop.Tenant.Identifier);
        var contents = await scope.ServiceProvider.GetRequiredService<PaymentsDbContext>().OutboxMessages.AsNoTracking()
            .Select(message => message.Content)
            .ToListAsync(Cancel);
        return contents.Count(content => content.Contains(orderId.ToString(), StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>OrderConfirmed events Ordering wrote for the order, delivered or not.</summary>
    private async Task<int> OrderConfirmedInOutboxAsync(Shop shop, Guid orderId)
    {
        await using var scope = await api.CreateTenantScopeAsync(shop.Tenant.Identifier);
        var messages = await scope.ServiceProvider.GetRequiredService<OrderingDbContext>().OutboxMessages.AsNoTracking()
            .Select(message => new { message.Type, message.Content })
            .ToListAsync(Cancel);
        return messages.Count(message => message.Type.Contains(nameof(OrderConfirmed), StringComparison.Ordinal)
            && message.Content.Contains(orderId.ToString(), StringComparison.OrdinalIgnoreCase));
    }

    private sealed record SessionResponse(Guid PaymentId, Guid TransactionId, decimal Amount, Uri RedirectUrl);
}
