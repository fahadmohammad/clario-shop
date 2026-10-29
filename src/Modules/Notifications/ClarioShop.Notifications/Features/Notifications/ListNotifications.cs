using ClarioShop.Notifications.Domain;
using ClarioShop.Notifications.Persistence;

namespace ClarioShop.Notifications.Features.Notifications;

/// <summary>
/// The notification log, newest first, e.g. every message of one order, every stock alert or every message that failed.
/// </summary>
public static class ListNotifications
{
    /// <param name="OrderId">Only messages about this order.</param>
    /// <param name="Status">Only messages in this status, e.g. <c>Failed</c>.</param>
    /// <param name="Kind">Only messages of this kind, e.g. <c>StockWentNegative</c>.</param>
    public sealed record Query(Guid? OrderId, string? Status, string? Kind, int Page = 1, int PageSize = 25);

    /// <param name="OrderId">Null for a stock alert.</param>
    /// <param name="OrderNumber">Null for a stock alert.</param>
    public sealed record NotificationItem(
        Guid Id,
        string Kind,
        Guid? OrderId,
        string? OrderNumber,
        string Channel,
        string Recipient,
        string Language,
        string? Subject,
        string Body,
        string Status,
        int Attempts,
        DateTimeOffset? NextAttemptAt,
        string? LastError,
        DateTimeOffset CreatedAt,
        DateTimeOffset? SentAt);

    public sealed record Response(IReadOnlyList<NotificationItem> Items, int Page, int PageSize, int TotalCount);

    internal sealed class Validator : AbstractValidator<Query>
    {
        public Validator()
        {
            RuleFor(query => query.Page).GreaterThanOrEqualTo(1);
            RuleFor(query => query.PageSize).InclusiveBetween(1, 100);
            RuleFor(query => query.Status)
                .IsEnumName(typeof(NotificationStatus), caseSensitive: false)
                .When(query => !string.IsNullOrEmpty(query.Status))
                .WithMessage($"Status must be one of: {string.Join(", ", Enum.GetNames<NotificationStatus>())}.");
            RuleFor(query => query.Kind)
                .IsEnumName(typeof(NotificationKind), caseSensitive: false)
                .When(query => !string.IsNullOrEmpty(query.Kind))
                .WithMessage($"Kind must be one of: {string.Join(", ", Enum.GetNames<NotificationKind>())}.");
        }
    }

    internal sealed class Handler(NotificationsDbContext db) : ICommandHandler<Query, Response>
    {
        public async Task<Result<Response>> HandleAsync(Query query, CancellationToken cancellationToken)
        {
            var logs = db.NotificationLogs.AsNoTracking();
            if (query.OrderId is { } orderId)
            {
                logs = logs.Where(log => log.OrderId == orderId);
            }

            if (!string.IsNullOrEmpty(query.Status))
            {
                var status = Enum.Parse<NotificationStatus>(query.Status, ignoreCase: true);
                logs = logs.Where(log => log.Status == status);
            }

            if (!string.IsNullOrEmpty(query.Kind))
            {
                var kind = Enum.Parse<NotificationKind>(query.Kind, ignoreCase: true);
                logs = logs.Where(log => log.Kind == kind);
            }

            var total = await logs.CountAsync(cancellationToken);
            var rows = await logs
                .OrderByDescending(log => log.CreatedAt)
                .ThenByDescending(log => log.Id)
                .Skip((query.Page - 1) * query.PageSize)
                .Take(query.PageSize)
                .ToListAsync(cancellationToken);

            var items = rows
                .Select(log => new NotificationItem(
                    log.Id,
                    log.Kind.ToString(),
                    log.OrderId,
                    log.OrderNumber,
                    log.Channel.ToString(),
                    log.Recipient,
                    log.Language,
                    log.Subject,
                    log.Body,
                    log.Status.ToString(),
                    log.Attempts,
                    log.NextAttemptAt,
                    log.LastError,
                    log.CreatedAt,
                    log.SentAt))
                .ToList();
            return new Response(items, query.Page, query.PageSize, total);
        }
    }
}
