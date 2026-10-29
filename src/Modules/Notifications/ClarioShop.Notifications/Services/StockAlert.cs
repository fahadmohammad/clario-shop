using ClarioShop.Notifications.Domain;

namespace ClarioShop.Notifications.Services;

/// <summary>A stock problem the store's contact is told about.</summary>
/// <param name="AlertId">What the alert is sent once for; see <see cref="NotificationLog.EventId"/>.</param>
/// <param name="Values">The kind's placeholders (<see cref="MessagePlaceholders.For"/>) except the store name.</param>
internal sealed record StockAlert(Guid AlertId, NotificationKind Kind, IReadOnlyDictionary<string, string> Values);
