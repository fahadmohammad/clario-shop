using ClarioShop.SharedKernel.Domain;

namespace ClarioShop.Payments.Domain;

/// <summary>
/// One try at collecting a payment: a rider handing over cash, or one gateway session. A gateway attempt's id is the
/// transaction id sent to the gateway (SSLCommerz <c>tran_id</c>).
/// </summary>
internal sealed class PaymentAttempt : Entity
{
    private PaymentAttempt()
    {
    }

    public Guid PaymentId { get; private set; }

    public AttemptOutcome Outcome { get; private set; }

    public DateTimeOffset AttemptedAt { get; private set; }

    /// <summary>The staff user or rider who recorded it; null when the system did.</summary>
    public Guid? RecordedByUserId { get; private set; }

    public string? Note { get; private set; }

    /// <summary>The gateway's id for the session opened for this attempt (SSLCommerz <c>sessionkey</c>).</summary>
    public string? SessionKey { get; private set; }

    /// <summary>The gateway's id for the paid transaction (SSLCommerz <c>val_id</c>).</summary>
    public string? ValidationId { get; private set; }

    /// <summary>The bank's or wallet's reference for the paid transaction.</summary>
    public string? BankTransactionId { get; private set; }

    /// <summary>How the shopper paid, as the gateway names it, e.g. <c>BKASH-BKash</c>.</summary>
    public string? CardType { get; private set; }

    /// <summary>When the gateway attempt succeeded or failed; null while pending.</summary>
    public DateTimeOffset? CompletedAt { get; private set; }

    internal static PaymentAttempt Create(Guid paymentId, AttemptOutcome outcome, DateTimeOffset now, Guid? userId, string? note) => new()
    {
        Id = Guid.CreateVersion7(),
        PaymentId = paymentId,
        Outcome = outcome,
        AttemptedAt = now,
        RecordedByUserId = userId,
        Note = string.IsNullOrWhiteSpace(note) ? null : note.Trim(),
        CompletedAt = outcome == AttemptOutcome.Pending ? null : now,
    };

    internal void SessionOpened(string sessionKey) => SessionKey = sessionKey;

    internal void Succeed(GatewayReceipt receipt, DateTimeOffset now, string? note)
    {
        Outcome = AttemptOutcome.Succeeded;
        ValidationId = receipt.ValidationId;
        BankTransactionId = receipt.BankTransactionId;
        CardType = receipt.CardType;
        CompletedAt = now;
        Note = note;
    }

    internal void Fail(string reason, DateTimeOffset now)
    {
        Outcome = AttemptOutcome.Failed;
        CompletedAt = now;
        Note = reason;
    }
}
