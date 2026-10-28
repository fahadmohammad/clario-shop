namespace ClarioShop.Reporting.Domain;

/// <summary>What an order was worth when it was placed.</summary>
/// <param name="Status">Where it started: <see cref="SalesOrderStatus.Placed"/>, or <see cref="SalesOrderStatus.PendingPayment"/> when prepaid.</param>
/// <param name="PlacedOn">The store's calendar day of <paramref name="PlacedAt"/>.</param>
internal sealed record SalesOrderPlacement(
    SalesOrderStatus Status,
    string PaymentMethod,
    decimal Subtotal,
    decimal DeliveryFee,
    decimal Vat,
    decimal Total,
    DateTimeOffset PlacedAt,
    DateOnly PlacedOn,
    IReadOnlyList<SalesOrderLineSpec> Lines);
