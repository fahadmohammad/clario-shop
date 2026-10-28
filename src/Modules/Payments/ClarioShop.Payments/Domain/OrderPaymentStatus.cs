namespace ClarioShop.Payments.Domain;

/// <summary>How much of an order is paid, from its succeeded payments.</summary>
internal enum OrderPaymentStatus
{
    Unpaid,
    PartiallyPaid,
    Paid,
}
