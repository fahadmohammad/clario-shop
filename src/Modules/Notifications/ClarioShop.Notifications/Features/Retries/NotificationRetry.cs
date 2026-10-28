using ClarioShop.Notifications.Domain;
using ClarioShop.Notifications.Persistence;
using ClarioShop.Notifications.Services;
using Microsoft.Extensions.Options;

namespace ClarioShop.Notifications.Features.Retries;

/// <summary>
/// Sends the current store's messages that are due: retries after a failed try, and messages whose try was cut
/// short (e.g. the process stopped mid-send) once their claim has timed out.
/// </summary>
internal sealed class NotificationRetry(NotificationsDbContext db, NotificationSender sender, IOptions<NotificationOptions> options)
{
    /// <returns>How many messages this run tried to send.</returns>
    public async Task<int> RetryDueAsync(DateTimeOffset now, CancellationToken cancellationToken)
    {
        var due = await db.NotificationLogs
            .AsNoTracking()
            .Where(log => log.Status == NotificationStatus.Pending && log.NextAttemptAt <= now)
            .OrderBy(log => log.NextAttemptAt)
            .Select(log => log.Id)
            .Take(options.Value.BatchSize)
            .ToListAsync(cancellationToken);

        var tried = 0;
        foreach (var id in due)
        {
            if (await sender.TrySendAsync(id, now, cancellationToken))
            {
                tried++;
            }
        }

        return tried;
    }
}
