namespace ClarioShop.Notifications.Domain;

/// <summary>The values a template can use.</summary>
internal static class MessagePlaceholders
{
    /// <summary>The store's name from its settings.</summary>
    public const string StoreName = "StoreName";

    /// <summary>The shopper's name, or a polite stand-in when it is not known.</summary>
    public const string CustomerName = "CustomerName";

    /// <summary>E.g. <c>CS-000123</c>.</summary>
    public const string OrderNumber = "OrderNumber";

    /// <summary>What the shopper pays, in the store's currency format, e.g. <c>৳1,250</c>. Only when the order is placed.</summary>
    public const string Total = "Total";

    /// <summary>The product's name, in a stock alert.</summary>
    public const string ProductName = "ProductName";

    /// <summary>The variant's SKU, in a stock alert.</summary>
    public const string Sku = "Sku";

    /// <summary>The location's code, e.g. <c>MAIN</c>, in a stock alert.</summary>
    public const string LocationCode = "LocationCode";

    /// <summary>On-hand stock after the change, below zero.</summary>
    public const string OnHand = "OnHand";

    /// <summary>The on-hand stock a stock level showed before the drift check corrected it.</summary>
    public const string RecordedOnHand = "RecordedOnHand";

    /// <summary>The on-hand stock according to the ledger, which the stock level now shows.</summary>
    public const string LedgerOnHand = "LedgerOnHand";

    /// <summary>The reserved stock a stock level showed before the drift check corrected it.</summary>
    public const string RecordedReserved = "RecordedReserved";

    /// <summary>The stock active reservations hold, which the stock level now shows.</summary>
    public const string HeldReserved = "HeldReserved";

    /// <summary>The placeholders that have a value for this kind of message.</summary>
    public static IReadOnlyList<string> For(NotificationKind kind) => kind switch
    {
        NotificationKind.OrderPlaced => [StoreName, CustomerName, OrderNumber, Total],
        NotificationKind.StockWentNegative => [StoreName, ProductName, Sku, LocationCode, OnHand],
        NotificationKind.StockDriftDetected => [StoreName, Sku, LocationCode, RecordedOnHand, LedgerOnHand, RecordedReserved, HeldReserved],
        _ => [StoreName, CustomerName, OrderNumber],
    };
}
