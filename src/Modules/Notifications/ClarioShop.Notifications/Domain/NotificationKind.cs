namespace ClarioShop.Notifications.Domain;

/// <summary>What happened to the order, which picks the message the shopper gets.</summary>
internal enum OrderNotificationKind
{
    OrderPlaced,

    OrderConfirmed,

    OrderShipped,

    OrderDelivered,
}
