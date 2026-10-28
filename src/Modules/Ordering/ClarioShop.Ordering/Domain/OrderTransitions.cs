namespace ClarioShop.Ordering.Domain;

/// <summary>
/// The order lifecycle: Placed (or PendingPayment) → Confirmed → Packed → Shipped → Delivered. An order can be
/// cancelled until it ships; a shipped parcel the shopper refused, or a delivered one sent back, is Returned.
/// </summary>
internal static class OrderTransitions
{
    private static readonly Dictionary<OrderStatus, OrderStatus[]> Allowed = new()
    {
        // Prepaid orders are confirmed by their payment (Payments module), never by staff.
        [OrderStatus.PendingPayment] = [OrderStatus.Cancelled],
        [OrderStatus.Placed] = [OrderStatus.Confirmed, OrderStatus.Cancelled],
        [OrderStatus.Confirmed] = [OrderStatus.Packed, OrderStatus.Cancelled],
        [OrderStatus.Packed] = [OrderStatus.Shipped, OrderStatus.Cancelled],
        [OrderStatus.Shipped] = [OrderStatus.Delivered, OrderStatus.Returned],
        [OrderStatus.Delivered] = [OrderStatus.Returned],
        [OrderStatus.Cancelled] = [],
        [OrderStatus.Returned] = [],
    };

    public static bool IsAllowed(OrderStatus from, OrderStatus to) => Allowed[from].Contains(to);

}
