namespace ClarioShop.Ordering.Domain;

/// <summary>Prepaid orders start at PendingPayment, cash-on-delivery orders at Placed.</summary>
internal enum OrderStatus
{
    PendingPayment,
    Placed,
    Confirmed,
    Packed,
    Shipped,
    Delivered,
    Cancelled,
    Returned,
}
