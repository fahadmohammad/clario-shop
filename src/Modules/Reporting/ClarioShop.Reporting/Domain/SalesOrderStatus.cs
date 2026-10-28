namespace ClarioShop.Reporting.Domain;

/// <summary>
/// An order's status as Ordering names it. The values run in lifecycle order, which breaks ties between two changes
/// made at the same instant: the later stage wins.
/// </summary>
internal enum SalesOrderStatus
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
