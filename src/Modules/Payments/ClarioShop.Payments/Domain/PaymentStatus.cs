namespace ClarioShop.Payments.Domain;

/// <summary>A gateway payment waits in Pending until the gateway answers; cash is Succeeded the moment it is recorded.</summary>
internal enum PaymentStatus
{
    Pending,
    Succeeded,
    Failed,
}
