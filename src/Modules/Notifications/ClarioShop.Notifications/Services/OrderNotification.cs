using ClarioShop.Notifications.Domain;

namespace ClarioShop.Notifications.Services;

/// <summary>An order event the shopper is told about.</summary>
/// <param name="Total">What the shopper pays; only known when the order is placed.</param>
internal sealed record OrderNotification(
    Guid EventId,
    OrderNotificationKind Kind,
    Guid OrderId,
    string OrderNumber,
    Guid CustomerId,
    decimal? Total = null);
