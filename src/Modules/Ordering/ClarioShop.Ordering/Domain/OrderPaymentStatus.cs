namespace ClarioShop.Ordering.Domain;

/// <summary>How much of the order Payments has reported as paid.</summary>
internal enum OrderPaymentStatus
{
    Unpaid,
    PartiallyPaid,
    Paid,
}
