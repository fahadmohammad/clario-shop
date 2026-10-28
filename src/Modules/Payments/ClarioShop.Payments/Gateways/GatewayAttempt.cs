namespace ClarioShop.Payments.Gateways;

/// <summary>
/// One try the shopper made on the gateway's page for one of our transaction ids, as the gateway's transaction lookup
/// reports it. A session can hold several, e.g. a declined card and then a paid bKash.
/// </summary>
/// <param name="State">What the try means for the payment.</param>
/// <param name="ReportedStatus">The gateway's own word for it, e.g. <c>CANCELLED</c>; for notes and logs.</param>
/// <param name="ValidationId">The gateway's id for the try (SSLCommerz <c>val_id</c>); validate it before trusting a payment.</param>
internal sealed record GatewayAttempt(GatewayAttemptState State, string ReportedStatus, string? ValidationId);

internal enum GatewayAttemptState
{
    /// <summary>The gateway says it was paid; it is only believed once the validation API confirms it.</summary>
    Paid,

    /// <summary>Still being processed, e.g. the bank has not answered; it may yet be paid.</summary>
    InProgress,

    /// <summary>Failed, cancelled, expired or never attempted.</summary>
    NotPaid,
}
