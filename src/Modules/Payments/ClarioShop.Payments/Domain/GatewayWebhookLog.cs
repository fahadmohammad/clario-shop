using ClarioShop.SharedKernel.Domain;

namespace ClarioShop.Payments.Domain;

/// <summary>
/// One callback from a payment gateway, stored raw for audit before anything acts on it: the server-to-server
/// notification (IPN) or the shopper's browser coming back from the payment page. What came of it is
/// <see cref="Outcome"/>.
/// </summary>
internal sealed class GatewayWebhookLog : Entity
{
    public const int MaxIdLength = 100;

    public const int MaxDetailLength = 500;

    private GatewayWebhookLog()
    {
    }

    /// <summary>E.g. <c>SslCommerz</c>.</summary>
    public string Gateway { get; private set; } = string.Empty;

    public GatewayCallbackSource Source { get; private set; }

    /// <summary>Every field the gateway posted, as a JSON object.</summary>
    public string Payload { get; private set; } = string.Empty;

    /// <summary>The transaction id the callback names (our attempt id); null when it names none.</summary>
    public string? TransactionId { get; private set; }

    /// <summary>The gateway's validation id the callback names (SSLCommerz <c>val_id</c>).</summary>
    public string? ValidationId { get; private set; }

    /// <summary>The status the callback claims, e.g. <c>VALID</c> or <c>FAILED</c>; never trusted on its own.</summary>
    public string? ReportedStatus { get; private set; }

    public DateTimeOffset ReceivedAt { get; private set; }

    public GatewayCallbackOutcome Outcome { get; private set; }

    /// <summary>Why the callback changed nothing, e.g. an error code; null when it settled the payment.</summary>
    public string? Detail { get; private set; }

    /// <summary>The payment the callback was matched to.</summary>
    public Guid? PaymentId { get; private set; }

    public DateTimeOffset? ProcessedAt { get; private set; }

    public static GatewayWebhookLog Receive(
        string gateway,
        GatewayCallbackSource source,
        string payload,
        string? transactionId,
        string? validationId,
        string? reportedStatus,
        DateTimeOffset now) => new()
        {
            Id = Guid.CreateVersion7(),
            Gateway = gateway,
            Source = source,
            Payload = payload,
            TransactionId = Clip(transactionId),
            ValidationId = Clip(validationId),
            ReportedStatus = Clip(reportedStatus),
            ReceivedAt = now,
            Outcome = GatewayCallbackOutcome.Received,
        };

    public void Complete(GatewayCallbackOutcome outcome, Guid? paymentId, string? detail, DateTimeOffset now)
    {
        Outcome = outcome;
        PaymentId = paymentId;
        Detail = detail is { Length: > MaxDetailLength } ? detail[..MaxDetailLength] : detail;
        ProcessedAt = now;
    }

    private static string? Clip(string? value)
    {
        var trimmed = value?.Trim();
        return string.IsNullOrEmpty(trimmed) ? null : trimmed.Length > MaxIdLength ? trimmed[..MaxIdLength] : trimmed;
    }
}

internal enum GatewayCallbackSource
{
    /// <summary>The gateway's server-to-server notification.</summary>
    Ipn,

    /// <summary>The shopper's browser sent back from the payment page after paying.</summary>
    SuccessRedirect,

    /// <summary>The shopper's browser sent back after the payment failed.</summary>
    FailRedirect,

    /// <summary>The shopper's browser sent back after cancelling on the payment page.</summary>
    CancelRedirect,
}

internal enum GatewayCallbackOutcome
{
    /// <summary>Stored, not processed yet (or processing stopped half way, e.g. the gateway was unavailable).</summary>
    Received,

    /// <summary>The gateway confirmed the payment and it succeeded.</summary>
    Settled,

    /// <summary>The gateway confirmed a payment that had already been settled; nothing changed.</summary>
    AlreadySettled,

    /// <summary>The gateway confirmed a second payment for a payment that had already succeeded; it needs a refund.</summary>
    PaidTwice,

    /// <summary>The callback does not claim a payment (failed, cancelled, still pending); nothing changed.</summary>
    NotPaid,

    /// <summary>The callback names no transaction of this store.</summary>
    UnknownPayment,

    /// <summary>The gateway did not confirm what the callback claimed, or confirmed something else.</summary>
    Rejected,

    /// <summary>The gateway could not be asked; the callback can be sent again.</summary>
    GatewayUnavailable,
}
