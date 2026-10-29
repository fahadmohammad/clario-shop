using ClarioShop.Customers.Contracts;
using ClarioShop.Notifications.Domain;
using ClarioShop.Notifications.Persistence;
using ClarioShop.Settings.Contracts;

namespace ClarioShop.Notifications.Services;

/// <summary>
/// Tells the shopper about an order event: an SMS to their phone, and an email when they gave an address, in the
/// store's default language, from the store's own template when it has one and the built-in one otherwise. The messages are logged before they are sent, once per event, so a redelivered event
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
        var storeTemplates = await settings.GetNotificationTemplatesAsync(cancellationToken);

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
        db.NotificationLogs.Add(Log(notification, NotificationChannel.Sms, customer.Phone, language, storeTemplates, values, now));
        if (!string.IsNullOrWhiteSpace(customer.Email))
        {
            db.NotificationLogs.Add(Log(notification, NotificationChannel.Email, customer.Email, language, storeTemplates, values, now));
        }

        // A concurrent delivery of the same event fails here on the unique (event, channel) index and is redelivered.
        await db.SaveChangesAsync(cancellationToken);
    }

    private static NotificationLog Log(
        OrderNotification notification,
        NotificationChannel channel,
        string recipient,
        string language,
        IReadOnlyList<NotificationTemplateInfo> storeTemplates,
        IReadOnlyDictionary<string, string> values,
        DateTimeOffset now)
    {
        var message = TemplateFor(notification.Kind, channel, language, storeTemplates).Render(values);
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

    private static MessageTemplate TemplateFor(
        NotificationKind kind,
        NotificationChannel channel,
        string language,
        IReadOnlyList<NotificationTemplateInfo> storeTemplates)
    {
        var own = storeTemplates.FirstOrDefault(template =>
            template.Event == kind.ToString() && template.Channel == channel.ToString() && template.Language == language);
        return own is null ? DefaultTemplates.For(kind, channel, language) : new MessageTemplate(own.Subject, own.Body);
    }
}
