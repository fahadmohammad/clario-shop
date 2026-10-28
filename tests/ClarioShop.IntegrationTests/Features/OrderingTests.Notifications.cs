using System.Net;
using System.Net.Http.Json;
using ClarioShop.IntegrationTests.Infrastructure;
using ClarioShop.Notifications.Features.Retries;
using ClarioShop.Ordering.Contracts.Events;
using Microsoft.Extensions.DependencyInjection;

namespace ClarioShop.IntegrationTests.Features;

/// <summary>
/// Notifications tells the shopper about their order by SMS (and email when they gave one): once per event, in the
/// store's default language, with failed sends retried by the retry job (run directly, at a chosen time).
/// </summary>
public sealed partial class OrderingTests
{
    /// <summary>Past every retry delay (at most an hour) and the claim timeout.</summary>
    private static readonly TimeSpan AfterRetryDelay = TimeSpan.FromHours(2);

    [Fact]
    public async Task Notify_CashOrderLifecycle_SendsSmsForEachStepInEnglish()
    {
        var (shop, storeName) = await NotifyingShopAsync("ntf-life", "en");
        var variant = await shop.StockedVariantAsync("Miniket Rice 5kg", "RICE-5KG", price: 450m, stock: 10);
        var phone = NewPhone();
        using var shopper = api.CreateTenantClient(shop.Tenant.Identifier);
        var order = await PlaceOrderForAsync(shopper, variant, quantity: 3, phone);

        await WaitForNotificationsAsync(shop, order.Id, rows => rows.Count == 1 && rows[0].Status == "Sent");
        await ChangeStatusAsync(shop, order.Id, "confirm");
        await WaitForNotificationsAsync(shop, order.Id, rows => rows.Count == 2 && rows.All(row => row.Status == "Sent"));
        await ChangeStatusAsync(shop, order.Id, "pack");
        await ChangeStatusAsync(shop, order.Id, "ship");
        await WaitForNotificationsAsync(shop, order.Id, rows => rows.Count == 3 && rows.All(row => row.Status == "Sent"));
        await ChangeStatusAsync(shop, order.Id, "deliver");
        var rows = await WaitForNotificationsAsync(shop, order.Id, rows => rows.Count == 4 && rows.All(row => row.Status == "Sent"));

        Assert.Equal(1350m, order.Total);
        Assert.Equal(
            [
                $"{storeName}: Thank you! We received your order {order.Number}, total ৳1,350. We will call you to confirm it.",
                $"{storeName}: Your order {order.Number} is confirmed. We are getting it ready.",
                $"{storeName}: Your order {order.Number} is on its way.",
                $"{storeName}: Your order {order.Number} has been delivered. Thank you for shopping with us!",
            ],
            api.Sms.MessagesTo(E164(phone), storeName));
        Assert.Equal(["OrderDelivered", "OrderShipped", "OrderConfirmed", "OrderPlaced"], rows.Select(row => row.Kind));
        Assert.All(rows, row => Assert.Equal(("Sms", E164(phone), "en", 1, order.Number), (row.Channel, row.Recipient, row.Language, row.Attempts, row.OrderNumber)));
    }

    [Fact]
    public async Task Notify_StoreInBangla_SendsSmsInBangla()
    {
        var (shop, storeName) = await NotifyingShopAsync("ntf-bn", "bn");
        var variant = await shop.StockedVariantAsync("Ispahani Tea", "TEA-400G", price: 250m, stock: 5);
        var phone = NewPhone();
        using var shopper = api.CreateTenantClient(shop.Tenant.Identifier);

        var order = await PlaceOrderForAsync(shopper, variant, quantity: 1, phone);

        var row = Assert.Single(await WaitForNotificationsAsync(shop, order.Id, rows => rows.Count == 1 && rows[0].Status == "Sent"));
        Assert.Equal("bn", row.Language);
        Assert.Equal(
            $"{storeName}: ধন্যবাদ! আপনার অর্ডার {order.Number} পেয়েছি, মোট ৳250। নিশ্চিত করতে আমরা আপনাকে কল করব।",
            Assert.Single(api.Sms.MessagesTo(E164(phone), storeName)));
    }

    [Fact]
    public async Task Notify_CustomerWithEmail_AlsoSendsEmail()
    {
        var (shop, storeName) = await NotifyingShopAsync("ntf-email", "en");
        var variant = await shop.StockedVariantAsync("Lux Soap", "SOAP-LUX", price: 45m, stock: 10);
        var phone = NewPhone();
        var email = $"rahim.{Guid.NewGuid():N}@example.com";
        (await shop.Staff.PostAsJsonAsync("/api/customers", new { phone, name = "Rahim Uddin", email }, Cancel)).EnsureSuccessStatusCode();
        using var shopper = api.CreateTenantClient(shop.Tenant.Identifier);

        var order = await PlaceOrderForAsync(shopper, variant, quantity: 2, phone);

        var rows = await WaitForNotificationsAsync(shop, order.Id, rows => rows.Count == 2 && rows.All(row => row.Status == "Sent"));
        Assert.Equal(["Email", "Sms"], rows.Select(row => row.Channel).Order());
        var sent = Assert.Single(api.Email.To(email));
        Assert.Equal($"{storeName}: we received your order {order.Number}", sent.Subject);
        Assert.StartsWith("Dear Rahim Uddin,", sent.Body, StringComparison.Ordinal);
        Assert.Contains($"The total is ৳90.", sent.Body, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Notify_PrepaidOrder_SendsNothingUntilPaidThenConfirmation()
    {
        var shop = await PrepaidShopAsync("ntf-prepaid");
        var variant = await shop.StockedVariantAsync("Rupchanda Oil 5L", "OIL-5L", price: 899.5m, stock: 5);
        using var shopper = api.CreateTenantClient(shop.Tenant.Identifier);
        var order = await PlacePrepaidOrderAsync(shopper, variant, quantity: 1);
        await api.Events.WaitForAsync<OrderPlaced>(e => e.OrderId == order.Id, EventTimeout);
        var started = await StartPaymentAsync(shopper, order.Id);

        (await PostIpnAsync(shop, started.TransactionId, api.Gateway.Pay(started.TransactionId))).EnsureSuccessStatusCode();

        var row = Assert.Single(await WaitForNotificationsAsync(shop, order.Id, rows => rows.Count == 1 && rows[0].Status == "Sent"));
        Assert.Equal("OrderConfirmed", row.Kind);
    }

    [Fact]
    public async Task Notify_SameEventDeliveredAgain_SendsOnce()
    {
        var (shop, storeName) = await NotifyingShopAsync("ntf-once", "en");
        var variant = await shop.StockedVariantAsync("Miniket Rice 5kg", "RICE-5KG", price: 450m, stock: 10);
        var phone = NewPhone();
        using var shopper = api.CreateTenantClient(shop.Tenant.Identifier);
        var order = await PlaceOrderForAsync(shopper, variant, quantity: 1, phone);
        await ChangeStatusAsync(shop, order.Id, "confirm");
        await WaitForNotificationsAsync(shop, order.Id, rows => rows.Count == 2 && rows.All(row => row.Status == "Sent"));
        var confirmed = await api.Events.WaitForAsync<OrderConfirmed>(e => e.OrderId == order.Id, EventTimeout);

        // Delivered again under a new message id, so the inbox does not stop it: the notification log must.
        await RedeliverAsync(confirmed);

        Assert.Equal(2, (await NotificationsAsync(shop, order.Id)).Count);
        Assert.Single(api.Sms.MessagesTo(E164(phone), $"{storeName}: Your order {order.Number} is confirmed."));
    }

    [Fact]
    public async Task Notify_SmsGatewayFails_RetryJobSendsItLater()
    {
        var (shop, storeName) = await NotifyingShopAsync("ntf-retry", "en");
        var variant = await shop.StockedVariantAsync("Ispahani Tea", "TEA-400G", price: 250m, stock: 5);
        var phone = NewPhone();
        api.Sms.FailNext(E164(phone), 1);
        using var shopper = api.CreateTenantClient(shop.Tenant.Identifier);
        var order = await PlaceOrderForAsync(shopper, variant, quantity: 1, phone);
        var failed = Assert.Single(await WaitForNotificationsAsync(shop, order.Id, rows => rows.Count == 1 && rows[0].LastError is not null));

        var early = await RetryNotificationsAsync(shop, DateTimeOffset.UtcNow);
        var tried = await RetryNotificationsAsync(shop, DateTimeOffset.UtcNow.Add(AfterRetryDelay));

        Assert.Equal(("Pending", 1), (failed.Status, failed.Attempts));
        Assert.Contains("SMS gateway unavailable", failed.LastError, StringComparison.Ordinal);
        Assert.Equal((0, 1), (early, tried));
        var sent = Assert.Single(await NotificationsAsync(shop, order.Id));
        Assert.Equal(("Sent", 2, (string?)null), (sent.Status, sent.Attempts, sent.LastError));
        Assert.Single(api.Sms.MessagesTo(E164(phone), storeName));
    }

    [Fact]
    public async Task Notify_SmsGatewayKeepsFailing_GivesUpAfterFiveTries()
    {
        var (shop, storeName) = await NotifyingShopAsync("ntf-giveup", "en");
        var variant = await shop.StockedVariantAsync("Lux Soap", "SOAP-LUX", price: 45m, stock: 5);
        var phone = NewPhone();
        api.Sms.FailNext(E164(phone), 100);
        using var shopper = api.CreateTenantClient(shop.Tenant.Identifier);
        var order = await PlaceOrderForAsync(shopper, variant, quantity: 1, phone);
        await WaitForNotificationsAsync(shop, order.Id, rows => rows.Count == 1 && rows[0].LastError is not null);

        for (var run = 1; run <= 5; run++)
        {
            await RetryNotificationsAsync(shop, DateTimeOffset.UtcNow.Add(AfterRetryDelay * run));
        }

        var row = Assert.Single(await NotificationsAsync(shop, order.Id));
        Assert.Equal(("Failed", 5, (DateTimeOffset?)null), (row.Status, row.Attempts, row.NextAttemptAt));
        Assert.Empty(api.Sms.MessagesTo(E164(phone), storeName));
        var failedOnly = await shop.Staff.GetFromJsonAsync<NotificationPage>("/api/notifications?status=failed", Cancel);
        Assert.Equal(row.Id, Assert.Single(failedOnly!.Items).Id);
    }

    [Fact]
    public async Task Retry_ConcurrentRuns_SendTheMessageOnce()
    {
        var (shop, storeName) = await NotifyingShopAsync("ntf-race", "en");
        var variant = await shop.StockedVariantAsync("Miniket Rice 5kg", "RICE-5KG", price: 450m, stock: 5);
        var phone = NewPhone();
        api.Sms.FailNext(E164(phone), 1);
        using var shopper = api.CreateTenantClient(shop.Tenant.Identifier);
        var order = await PlaceOrderForAsync(shopper, variant, quantity: 1, phone);
        await WaitForNotificationsAsync(shop, order.Id, rows => rows.Count == 1 && rows[0].LastError is not null);
        var now = DateTimeOffset.UtcNow.Add(AfterRetryDelay);

        var runs = await Task.WhenAll(Enumerable.Range(0, 10).Select(_ => Task.Run(() => RetryNotificationsAsync(shop, now), Cancel)));

        Assert.Equal(1, runs.Sum());
        Assert.Single(api.Sms.MessagesTo(E164(phone), storeName));
        Assert.Equal(("Sent", 2), (await NotificationsAsync(shop, order.Id)).Select(row => (row.Status, row.Attempts)).Single());
    }

    [Fact]
    public async Task ListNotifications_NeedsNotificationsViewAndAValidStatus()
    {
        var shop = await ShopAsync("ntf-auth");
        var clerk = await StaffWithRoleAsync(shop, $"clerk@{shop.Tenant.Identifier}.test", "Inventory Clerk");
        using var anonymous = api.CreateTenantClient(shop.Tenant.Identifier);

        var asAnonymous = await anonymous.GetAsync("/api/notifications", Cancel);
        var asClerk = await clerk.GetAsync("/api/notifications", Cancel);
        var badStatus = await shop.Staff.GetAsync("/api/notifications?status=lost", Cancel);
        var asOwner = await shop.Staff.GetAsync("/api/notifications", Cancel);

        Assert.Equal(
            (HttpStatusCode.Unauthorized, HttpStatusCode.Forbidden, HttpStatusCode.BadRequest, HttpStatusCode.OK),
            (asAnonymous.StatusCode, asClerk.StatusCode, badStatus.StatusCode, asOwner.StatusCode));
    }

    /// <summary>A shop with its own store name (to pick its SMS out of every test's) in the given language, prices in whole taka.</summary>
    private async Task<(Shop Shop, string StoreName)> NotifyingShopAsync(string prefix, string language)
    {
        var shop = await ShopAsync(prefix);
        var storeName = $"Mart {shop.Tenant.Identifier}";
        var response = await shop.Staff.PutAsJsonAsync("/api/settings/store", new
        {
            profile = new { storeName },
            invoicePrefix = "CM",
            vatRatePercent = 0m,
            currency = new { symbol = "৳", symbolPosition = "Before", decimalDigits = 0, grouping = "SouthAsian" },
            defaultLanguage = language,
            languages = new[] { "en", "bn" },
        }, Cancel);
        await response.EnsureSuccessWithBodyAsync();
        return (shop, storeName);
    }

    private static async Task<OrderResponse> PlaceOrderForAsync(HttpClient shopper, Guid variantId, decimal quantity, string phone)
    {
        var cartId = await NewCartAsync(shopper);
        await AddItemAsync(shopper, cartId, variantId, quantity);
        var response = await CheckoutAsync(shopper, cartId, phone);
        await response.EnsureSuccessWithBodyAsync();
        return (await response.Content.ReadFromJsonAsync<OrderResponse>(Cancel))!;
    }

    /// <summary>A Bangladeshi mobile number no other test uses, so the SMS sent to it are this test's alone.</summary>
    private static string NewPhone() => $"019{Random.Shared.Next(10_000_000, 100_000_000)}";

    private static string E164(string phone) => $"+880{phone[1..]}";

    private static async Task<IReadOnlyList<NotificationRow>> NotificationsAsync(Shop shop, Guid orderId) =>
        (await shop.Staff.GetFromJsonAsync<NotificationPage>($"/api/notifications?orderId={orderId}", Cancel))!.Items;

    /// <summary>Waits for Notifications to catch up with the order's events.</summary>
    private static async Task<IReadOnlyList<NotificationRow>> WaitForNotificationsAsync(Shop shop, Guid orderId, Func<IReadOnlyList<NotificationRow>, bool> condition)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(Cancel);
        timeout.CancelAfter(EventTimeout);
        while (true)
        {
            var rows = await NotificationsAsync(shop, orderId);
            if (condition(rows))
            {
                return rows;
            }

            await Task.Delay(TimeSpan.FromMilliseconds(100), timeout.Token);
        }
    }

    private async Task<int> RetryNotificationsAsync(Shop shop, DateTimeOffset now)
    {
        await using var scope = await api.CreateTenantScopeAsync(shop.Tenant.Identifier);
        return await scope.ServiceProvider.GetRequiredService<NotificationRetry>().RetryDueAsync(now, Cancel);
    }

    private sealed record NotificationPage(IReadOnlyList<NotificationRow> Items, int TotalCount);

    private sealed record NotificationRow(
        Guid Id,
        string Kind,
        string OrderNumber,
        string Channel,
        string Recipient,
        string Language,
        string Status,
        int Attempts,
        DateTimeOffset? NextAttemptAt,
        string? LastError);
}
