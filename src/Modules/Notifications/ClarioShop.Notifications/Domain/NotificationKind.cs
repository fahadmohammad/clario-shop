namespace ClarioShop.Notifications.Domain;

/// <summary>What happened, which picks the message and who gets it: the shopper for an order, the store for a stock alert.</summary>
internal enum NotificationKind
{
    OrderPlaced,

    OrderConfirmed,

    OrderShipped,

    OrderDelivered,

    /// <summary>A variant's stock at a location went below zero; the store's contact is told.</summary>
    StockWentNegative,

    /// <summary>The drift check corrected stock levels that disagreed with the ledger; the store's contact is told once per check.</summary>
    StockDriftDetected,
}
