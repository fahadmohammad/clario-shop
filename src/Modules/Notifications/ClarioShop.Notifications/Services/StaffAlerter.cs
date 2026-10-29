using ClarioShop.Customers.Contracts;
using ClarioShop.Notifications.Domain;
using ClarioShop.Notifications.Persistence;
using ClarioShop.Settings.Contracts;
using Microsoft.Extensions.Logging;
using Npgsql;

namespace ClarioShop.Notifications.Services;

/// <summary>
/// Tells the store's contact about a stock alert: an SMS to the store's contact phone when it is a Bangladeshi mobile
/// number, and an email to its contact email, in the store's default language. Like <see cref="OrderNotifier"/>, the
/// messages are logged before they are sent, once per alert, so a redelivered event only sends what is still pending.
/// </summary>
internal sealed partial class StaffAlerter(
    NotificationsDbContext db,
    ICustomerDirectory customers,
    ISettingsQueries settings,
    NotificationSender sender,
    TimeProvider clock,
    ILogger<StaffAlerter> logger)
{
    public async Task AlertAsync(StockAlert alert, CancellationToken cancellationToken)
    {
        var logged = await db.NotificationLogs.AnyAsync(log => log.EventId == alert.AlertId, cancellationToken);
        if (!logged)
        {
            await LogMessagesAsync(alert, cancellationToken);
        }

        var pending = await db.NotificationLogs
            .Where(log => log.EventId == alert.AlertId && log.Status == NotificationStatus.Pending)
            .Select(log => log.Id)
            .ToListAsync(cancellationToken);
        foreach (var id in pending)
        {
            await sender.TrySendAsync(id, clock.GetUtcNow(), cancellationToken);
        }
    }

    private async Task LogMessagesAsync(StockAlert alert, CancellationToken cancellationToken)
    {
        var store = await settings.GetStoreSettingsAsync(cancellationToken);
        var phone = string.IsNullOrWhiteSpace(store.Profile.ContactPhone) ? null : customers.NormalizePhone(store.Profile.ContactPhone);
        var email = string.IsNullOrWhiteSpace(store.Profile.ContactEmail) ? null : store.Profile.ContactEmail.Trim();
        if (phone is null && email is null)
        {
            LogNoContact(logger, alert.Kind);
            return;
        }

        var language = DefaultTemplates.LanguageFor(store.DefaultLanguage);
        var values = new Dictionary<string, string>(alert.Values) { [MessagePlaceholders.StoreName] = store.Profile.StoreName };
        var now = clock.GetUtcNow();
        List<NotificationLog> logs = [];
        if (phone is not null)
        {
            logs.Add(Log(alert, NotificationChannel.Sms, phone, language, values, now));
        }

        if (email is not null)
        {
            logs.Add(Log(alert, NotificationChannel.Email, email, language, values, now));
        }

        db.NotificationLogs.AddRange(logs);
        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception) when (exception.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation })
        {
            // Another drift of the same check logged the alert at the same moment; it is sent once, from those rows.
            foreach (var log in logs)
            {
                db.Entry(log).State = EntityState.Detached;
            }
        }
    }

    private static NotificationLog Log(
        StockAlert alert,
        NotificationChannel channel,
        string recipient,
        string language,
        IReadOnlyDictionary<string, string> values,
        DateTimeOffset now)
    {
        var message = StaffAlertTemplates.For(alert.Kind, channel, language).Render(values);
        return NotificationLog.CreateStaffAlert(alert.AlertId, alert.Kind, channel, recipient, language, message.Subject, message.Body, now);
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "{Kind} alert not sent: the store has no contact email or mobile number in its settings")]
    private static partial void LogNoContact(ILogger logger, NotificationKind kind);
}
