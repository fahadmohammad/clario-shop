namespace ClarioShop.Ordering.Contracts;

/// <summary>Synchronous order lookups for modules that need an immediate answer, e.g. Payments checking what is owed.</summary>
public interface IOrderQueries
{
    /// <summary>What Payments needs to know about an order; null when it does not exist.</summary>
    Task<OrderPaymentInfo?> GetForPaymentAsync(Guid orderId, CancellationToken cancellationToken);
}

/// <param name="Number">The readable per-tenant number, e.g. <c>CS-000123</c>.</param>
/// <param name="Status">The order's status, e.g. <c>Shipped</c>; see <see cref="OrderStatuses"/>.</param>
/// <param name="PaymentMethod">E.g. <c>CashOnDelivery</c>.</param>
/// <param name="Total">What the shopper pays in all.</param>
public sealed record OrderPaymentInfo(Guid OrderId, string Number, Guid CustomerId, string Status, string PaymentMethod, decimal Total);

/// <summary>The order status names other modules may compare <see cref="OrderPaymentInfo.Status"/> with.</summary>
public static class OrderStatuses
{
    public const string Shipped = "Shipped";

    public const string Delivered = "Delivered";
}
