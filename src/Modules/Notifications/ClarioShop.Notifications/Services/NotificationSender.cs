using ClarioShop.Infrastructure.Email;
using ClarioShop.Infrastructure.Sms;
using ClarioShop.Notifications.Domain;
using ClarioShop.Notifications.Persistence;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace ClarioShop.Notifications.Services;

/// <summary>
/// Tries to send one logged message. The try is claimed in the database first, so two senders (the event handler and
/// the retry job, or two API instances) never send the same try twice.
/// </summary>
internal sealed partial class NotificationSender(
    NotificationsDbContext db,
    ISmsSender sms,
    IEmailSender email,
    IOptions<NotificationOptions> options,
    ILogger<NotificationSender> logger)
{
    /// <summary>Whether this call tried to send it; false when it was not due at <paramref name="now"/>, or another sender took the try.</summary>
    public async Task<bool> TrySendAsync(Guid notificationId, DateTimeOffset now, CancellationToken cancellationToken)
    {
        var policy = options.Value.RetryPolicy;
        var notification = await db.NotificationLogs.SingleAsync(log => log.Id == notificationId, cancellationToken);
        if (!notification.TryClaim(policy, now))
        {
            return false;
        }

        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            // Another sender claimed it first.
            db.Entry(notification).State = EntityState.Detached;
            return false;
        }

        try
        {
            await DeliverAsync(notification, cancellationToken);
            notification.MarkSent(now);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            // The SMS gateway or email provider is outside our control; any failure there waits for a retry.
            LogSendFailed(logger, exception, notification.Id, notification.Channel, notification.Attempts);
            notification.RecordFailure($"{exception.GetType().Name}: {exception.Message}", policy, now);
        }

        // Recorded even when the request was cancelled meanwhile: a message that went out must not be sent again.
        await db.SaveChangesAsync(CancellationToken.None);
        return true;
    }

    private Task DeliverAsync(NotificationLog notification, CancellationToken cancellationToken) => notification.Channel switch
    {
        NotificationChannel.Sms => sms.SendAsync(notification.Recipient, notification.Body, cancellationToken),
        NotificationChannel.Email => email.SendAsync(notification.Recipient, notification.Subject ?? string.Empty, notification.Body, cancellationToken),
        _ => throw new InvalidOperationException($"Unknown channel {notification.Channel}."),
    };

    [LoggerMessage(Level = LogLevel.Warning, Message = "Notification {NotificationId} ({Channel}) could not be sent on try {Attempt}")]
    private static partial void LogSendFailed(ILogger logger, Exception exception, Guid notificationId, NotificationChannel channel, int attempt);
}
