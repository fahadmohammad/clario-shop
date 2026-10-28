namespace ClarioShop.Ordering.Contracts;

/// <summary>Synchronous order lookups for modules that need an immediate answer, e.g. Payments checking what is owed.</summary>
public interface IOrderQueries
{
    /// <summary>What Payments needs to know about an order; null when it does not exist.</summary>
    Task<OrderPaymentInfo?> GetForPaymentAsync(Guid orderId, CancellationToken cancellationToken);

    /// <summary>What a payment gateway's hosted page needs about an order and its shopper; null when it does not exist.</summary>
    Task<OrderGatewayInfo?> GetForGatewayAsync(Guid orderId, CancellationToken cancellationToken);
}

/// <param name="Number">The readable per-tenant number, e.g. <c>CS-000123</c>.</param>
/// <param name="Status">The order's status, e.g. <c>Shipped</c>; see <see cref="OrderStatuses"/>.</param>
/// <param name="PaymentMethod">E.g. <c>CashOnDelivery</c>; see <see cref="OrderPaymentMethods"/>.</param>
/// <param name="Total">What the shopper pays in all.</param>
public sealed record OrderPaymentInfo(Guid OrderId, string Number, Guid CustomerId, string Status, string PaymentMethod, decimal Total);

/// <param name="Order">The order as <see cref="IOrderQueries.GetForPaymentAsync"/> describes it.</param>
/// <param name="CustomerPhone">E.164, e.g. +8801712345678.</param>
/// <param name="AddressLine">The delivery address's street lines and area on one line.</param>
/// <param name="ItemCount">Number of order lines.</param>
public sealed record OrderGatewayInfo(
    OrderPaymentInfo Order,
    string CustomerName,
    string CustomerPhone,
    string AddressLine,
    string City,
    string? PostalCode,
    int ItemCount);

/// <summary>The order status names other modules may compare <see cref="OrderPaymentInfo.Status"/> with.</summary>
public static class OrderStatuses
{
    public const string PendingPayment = "PendingPayment";

    public const string Shipped = "Shipped";

    public const string Delivered = "Delivered";
}

/// <summary>The payment method names other modules may compare <see cref="OrderPaymentInfo.PaymentMethod"/> with.</summary>
public static class OrderPaymentMethods
{
    public const string CashOnDelivery = "CashOnDelivery";

    /// <summary>Prepaid on SSLCommerz's hosted payment page.</summary>
    public const string SslCommerz = "SslCommerz";
}
