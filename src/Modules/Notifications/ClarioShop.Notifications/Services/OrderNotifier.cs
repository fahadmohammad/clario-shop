using ClarioShop.Customers.Contracts;
using ClarioShop.Notifications.Domain;
using ClarioShop.Notifications.Persistence;
using ClarioShop.Settings.Contracts;

namespace ClarioShop.Notifications.Services;

/// <summary>
/// Tells the shopper about an order event: an SMS to their phone, and an email when they gave an address, in the
/// store's default language. The messages are logged before they are sent, once per event, so a redelivered event
/// only sends what is still pending.
/// </summary>
internal sealed class OrderNotifier(
    NotificationsDbContext db,
    ICustomerDirectory customers,
    ISettingsQueries settings,
    NotificationSender sender,
    TimeProvider clock)
{
    public async Task NotifyAsync(OrderNotification notification, CancellationToken cancellationToken)
    {
        var logged = await db.NotificationLogs.AnyAsync(log => log.EventId == notification.EventId, cancellationToken);
        if (!logged)
        {
            await LogMessagesAsync(notification, cancellationToken);
        }

        var pending = await db.NotificationLogs
            .Where(log => log.EventId == notification.EventId && log.Status == NotificationStatus.Pending)
            .Select(log => log.Id)
            .ToListAsync(cancellationToken);
        foreach (var id in pending)
        {
            await sender.TrySendAsync(id, clock.GetUtcNow(), cancellationToken);
        }
    }

    private async Task LogMessagesAsync(OrderNotification notification, CancellationToken cancellationToken)
    {
        var customer = await customers.GetAsync(notification.CustomerId, cancellationToken)
            ?? throw new InvalidOperationException($"Order {notification.OrderNumber} belongs to customer {notification.CustomerId}, who does not exist.");
        var store = await settings.GetStoreSettingsAsync(cancellationToken);
        var language = DefaultTemplates.LanguageFor(store.DefaultLanguage);

        var values = new Dictionary<string, string>
        {
            [MessagePlaceholders.StoreName] = store.Profile.StoreName,
            [MessagePlaceholders.CustomerName] = string.IsNullOrWhiteSpace(customer.Name) ? DefaultTemplates.UnnamedCustomer(language) : customer.Name,
            [MessagePlaceholders.OrderNumber] = notification.OrderNumber,
        };
        if (notification.Total is { } total)
        {
            values[MessagePlaceholders.Total] = AmountFormatter.Format(total, store.Currency);
        }

        var now = clock.GetUtcNow();
        db.NotificationLogs.Add(Log(notification, NotificationChannel.Sms, customer.Phone, language, values, now));
        if (!string.IsNullOrWhiteSpace(customer.Email))
        {
            db.NotificationLogs.Add(Log(notification, NotificationChannel.Email, customer.Email, language, values, now));
        }

        // A concurrent delivery of the same event fails here on the unique (event, channel) index and is redelivered.
        await db.SaveChangesAsync(cancellationToken);
    }

    private static NotificationLog Log(
        OrderNotification notification,
        NotificationChannel channel,
        string recipient,
        string language,
        IReadOnlyDictionary<string, string> values,
        DateTimeOffset now)
    {
        var message = DefaultTemplates.For(notification.Kind, channel, language).Render(values);
        return NotificationLog.Create(
            notification.EventId,
            notification.Kind,
            notification.OrderId,
            notification.OrderNumber,
            notification.CustomerId,
            channel,
            recipient,
            language,
            message.Subject,
            message.Body,
            now);
    }
}
