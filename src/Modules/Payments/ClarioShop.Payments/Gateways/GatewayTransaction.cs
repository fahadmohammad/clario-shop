namespace ClarioShop.Payments.Gateways;

/// <summary>A transaction as the gateway's validation API reports it.</summary>
/// <param name="TransactionId">Our payment id, as sent when the session was opened.</param>
/// <param name="ValidationId">The gateway's id for this transaction (SSLCommerz <c>val_id</c>).</param>
/// <param name="Amount">What the shopper paid, in <paramref name="Currency"/>; compare it with what was asked for.</param>
/// <param name="BankTransactionId">The bank's or wallet's reference, for staff tracing a payment.</param>
/// <param name="CardType">E.g. <c>BKASH-BKash</c> or <c>VISA-Dutch Bangla</c>.</param>
/// <param name="IsRisky">True when the gateway flags the payment for review before the order ships.</param>
internal sealed record GatewayTransaction(
    string TransactionId,
    string ValidationId,
    GatewayTransactionStatus Status,
    decimal Amount,
    string Currency,
    string? BankTransactionId,
    string? CardType,
    bool IsRisky);

internal enum GatewayTransactionStatus
{
    /// <summary>Paid, and this is the first time it was validated.</summary>
    Valid,

    /// <summary>Paid, and already validated before (e.g. the IPN after the redirect, or a retried webhook).</summary>
    AlreadyValidated,

    /// <summary>Not a successful payment: failed, cancelled, unknown to the gateway or not this store's.</summary>
    Invalid,
}
