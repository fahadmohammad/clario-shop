using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using ClarioShop.Infrastructure.Messaging;
using ClarioShop.IntegrationTests.Infrastructure;
using ClarioShop.Inventory.Contracts.Events;
using ClarioShop.Notifications.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace ClarioShop.IntegrationTests.Features;

/// <summary>
/// Notifications tells the store's contact (the phone and email in its settings) when stock goes below zero and when
/// the drift check corrects stock levels, in the store's default language, once per event (once per check for drift).
/// </summary>
public sealed partial class InventoryTests
{
    private static readonly TimeSpan AlertTimeout = TimeSpan.FromSeconds(15);

    [Fact]
    public async Task StockWentNegative_StoreHasContact_SendsSmsAndEmailToIt()
    {
        var (tenant, variantId) = await StockedVariantAsync("alert-neg", quantity: 1);
        using var staff = await OwnerClientAsync(tenant);
        var contact = await SetStoreContactAsync(staff, tenant, "en");

        await RecordPosSaleAsync(tenant, variantId, quantity: 3);

        var rows = await WaitForAlertsAsync(staff, "StockWentNegative", rows => rows.Count == 2 && rows.All(row => row.Status == "Sent"));
        Assert.Equal(
            [("Email", contact.Email, "en"), ("Sms", contact.E164, "en")],
            rows.Select(row => (row.Channel, row.Recipient, row.Language)).Order());
        Assert.All(rows, row => Assert.Equal((null, null), (row.OrderId, row.OrderNumber)));
        Assert.Equal(
            $"{contact.StoreName}: Stock of Item alert-neg (SKU-ALERT-NEG) at MAIN is below zero (-2). A sale was recorded without stock; please count it.",
            Assert.Single(api.Sms.MessagesTo(contact.E164, contact.StoreName)));
        var email = Assert.Single(api.Email.To(contact.Email));
        Assert.Equal($"{contact.StoreName}: SKU-ALERT-NEG stock is below zero at MAIN", email.Subject);
    }

    [Fact]
    public async Task StockWentNegative_StoreInBangla_SendsSmsInBangla()
    {
        var (tenant, variantId) = await StockedVariantAsync("alert-bn", quantity: 1);
        using var staff = await OwnerClientAsync(tenant);
        var contact = await SetStoreContactAsync(staff, tenant, "bn");

        await RecordPosSaleAsync(tenant, variantId, quantity: 2);

        await WaitForAlertsAsync(staff, "StockWentNegative", rows => rows.Count == 2 && rows.All(row => row.Status == "Sent"));
        Assert.Equal(
            $"{contact.StoreName}: MAIN-এ Item alert-bn (SKU-ALERT-BN)-এর স্টক শূন্যের নিচে (-1)। স্টক ছাড়া বিক্রি রেকর্ড হয়েছে; অনুগ্রহ করে গুনে দেখুন।",
            Assert.Single(api.Sms.MessagesTo(contact.E164, contact.StoreName)));
    }

    [Fact]
    public async Task StockWentNegative_ContactPhoneIsNotAMobileNumber_SendsEmailOnly()
    {
        var (tenant, variantId) = await StockedVariantAsync("alert-landline", quantity: 1);
        using var staff = await OwnerClientAsync(tenant);
        var contact = await SetStoreContactAsync(staff, tenant, "en", phone: "02-9876543");

        await RecordPosSaleAsync(tenant, variantId, quantity: 2);

        var row = Assert.Single(await WaitForAlertsAsync(staff, "StockWentNegative", rows => rows.Count == 1 && rows[0].Status == "Sent"));
        Assert.Equal(("Email", contact.Email), (row.Channel, row.Recipient));
    }

    [Fact]
    public async Task StockWentNegative_StoreHasNoContact_SendsNothing()
    {
        var (tenant, variantId) = await StockedVariantAsync("alert-none", quantity: 1);
        using var staff = await OwnerClientAsync(tenant);

        await RecordPosSaleAsync(tenant, variantId, quantity: 2);

        await WaitUntilNotificationsHandledAsync(tenant, nameof(StockWentNegative));
        Assert.Empty(await AlertsAsync(staff, "StockWentNegative"));
    }

    [Fact]
    public async Task StockWentNegative_SameEventDeliveredAgain_SendsOnce()
    {
        var (tenant, variantId) = await StockedVariantAsync("alert-once", quantity: 1);
        using var staff = await OwnerClientAsync(tenant);
        var contact = await SetStoreContactAsync(staff, tenant, "en");
        await RecordPosSaleAsync(tenant, variantId, quantity: 2);
        await WaitForAlertsAsync(staff, "StockWentNegative", rows => rows.Count == 2 && rows.All(row => row.Status == "Sent"));
        var negative = await api.Events.WaitForAsync<StockWentNegative>(e => e.VariantId == variantId, AlertTimeout);

        // Delivered again under a new message id, so the inbox does not stop it: the notification log must.
        await RedeliverAsync(negative);

        Assert.Equal(2, (await AlertsAsync(staff, "StockWentNegative")).Count);
        Assert.Single(api.Sms.MessagesTo(contact.E164, contact.StoreName));
    }

    [Fact]
    public async Task DriftCheck_CorrectsSeveralLevels_AlertsTheStoreOncePerCheck()
    {
        var tenant = await api.CreateTenantAsync("alert-drift");
        using var staff = await OwnerClientAsync(tenant);
        var product = await CreateProductAsync(staff, "Pran Juice", "JUICE-250", "JUICE-1L");
        foreach (var variantId in product.VariantIds)
        {
            await ReceiveAsync(staff, variantId, quantity: 10, unitCost: 10m);
            await TamperLevelAsync(tenant, variantId, onHand: 13, reserved: 0);
        }

        var contact = await SetStoreContactAsync(staff, tenant, "en");

        var check = await PostAsync<DriftCheckResponse>(staff, "/api/inventory/drift-checks", new { });

        var rows = await WaitForAlertsAsync(staff, "StockDriftDetected", rows => rows.Count == 2 && rows.All(row => row.Status == "Sent"));
        await WaitUntilNotificationsHandledAsync(tenant, nameof(StockDriftDetected), handled: 2);
        Assert.Equal(2, check.Drifts.Count);
        Assert.Equal(["Email", "Sms"], rows.Select(row => row.Channel).Order());
        Assert.Equal(2, (await AlertsAsync(staff, "StockDriftDetected")).Count);
        Assert.StartsWith(
            $"{contact.StoreName}: The stock check corrected stock counts that did not match the stock records, e.g. JUICE-",
            Assert.Single(api.Sms.MessagesTo(contact.E164, contact.StoreName)),
            StringComparison.Ordinal);
        Assert.Contains("showed 13 on hand and 0 reserved; the records hold 10 on hand and 0 reserved", Assert.Single(api.Email.To(contact.Email)).Body, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ListNotifications_UnknownKind_ReturnsValidationProblem()
    {
        var tenant = await api.CreateTenantAsync("alert-kind");
        using var staff = await OwnerClientAsync(tenant);

        var response = await staff.GetAsync("/api/notifications?kind=StockLow", Cancel);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    /// <summary>Gives the store a name, contact phone and email no other test uses, so the alerts sent to them are this test's alone.</summary>
    private static async Task<StoreContact> SetStoreContactAsync(HttpClient staff, TestTenant tenant, string language, string? phone = null)
    {
        var mobile = $"018{Random.Shared.Next(10_000_000, 100_000_000)}";
        var contact = new StoreContact($"Store {tenant.Identifier}", $"+880{mobile[1..]}", $"owner.{Guid.NewGuid():N}@shop.test");
        var response = await staff.PutAsJsonAsync("/api/settings/store", new
        {
            profile = new { storeName = contact.StoreName, contactPhone = phone ?? $"{mobile[..5]}-{mobile[5..]}", contactEmail = contact.Email },
            invoicePrefix = "CS",
            vatRatePercent = 0m,
            currency = new { symbol = "৳", symbolPosition = "Before", decimalDigits = 0, grouping = "SouthAsian" },
            defaultLanguage = language,
            languages = new[] { "en", "bn" },
        }, Cancel);
        await response.EnsureSuccessWithBodyAsync();
        return contact;
    }

    private static async Task<IReadOnlyList<AlertRow>> AlertsAsync(HttpClient staff, string kind) =>
        (await staff.GetFromJsonAsync<AlertPage>($"/api/notifications?kind={kind}", Cancel))!.Items;

    /// <summary>Waits for Notifications to catch up with Inventory's events.</summary>
    private static async Task<IReadOnlyList<AlertRow>> WaitForAlertsAsync(HttpClient staff, string kind, Func<IReadOnlyList<AlertRow>, bool> condition)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(Cancel);
        timeout.CancelAfter(AlertTimeout);
        while (true)
        {
            var rows = await AlertsAsync(staff, kind);
            if (condition(rows))
            {
                return rows;
            }

            await Task.Delay(TimeSpan.FromMilliseconds(100), timeout.Token);
        }
    }

    /// <summary>Waits until Notifications' inbox shows it handled <paramref name="handled"/> events of the type.</summary>
    private async Task WaitUntilNotificationsHandledAsync(TestTenant tenant, string eventType, int handled = 1)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(Cancel);
        timeout.CancelAfter(AlertTimeout);
        while (true)
        {
            await using (var scope = await api.CreateTenantScopeAsync(tenant.Identifier))
            {
                var db = scope.ServiceProvider.GetRequiredService<NotificationsDbContext>();
                var handlers = await db.InboxMessages.AsNoTracking().Select(inbox => inbox.Handler).ToListAsync(timeout.Token);
                if (handlers.Count(handler => handler.Contains(eventType, StringComparison.Ordinal)) >= handled)
                {
                    return;
                }
            }

            await Task.Delay(TimeSpan.FromMilliseconds(100), timeout.Token);
        }
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

    private sealed record StoreContact(string StoreName, string E164, string Email);

    private sealed record AlertRow(Guid Id, string Kind, Guid? OrderId, string? OrderNumber, string Channel, string Recipient, string Language, string Status);

    private sealed record AlertPage(IReadOnlyList<AlertRow> Items, int TotalCount);
}
