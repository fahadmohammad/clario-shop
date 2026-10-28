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
}
