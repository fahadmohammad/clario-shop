namespace ClarioShop.Payments.Domain;

/// <summary>A paid transaction as the gateway's own validation API confirmed it, never as the shopper's browser reported it.</summary>
/// <param name="ValidationId">The gateway's id for the transaction (SSLCommerz <c>val_id</c>).</param>
/// <param name="Currency">ISO 4217, e.g. <c>BDT</c>.</param>
/// <param name="BankTransactionId">The bank's or wallet's reference, for staff tracing a payment.</param>
/// <param name="CardType">E.g. <c>BKASH-BKash</c>.</param>
/// <param name="IsRisky">The gateway flagged the payment for review.</param>
internal sealed record GatewayReceipt(
    string ValidationId,
    decimal Amount,
    string Currency,
    string? BankTransactionId,
    string? CardType,
    bool IsRisky);
