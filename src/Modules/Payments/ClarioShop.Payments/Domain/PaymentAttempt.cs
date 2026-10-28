using ClarioShop.SharedKernel.Domain;

namespace ClarioShop.Payments.Domain;

/// <summary>One try at collecting a payment: a rider handing over cash, or (later) one gateway session.</summary>
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

    internal static PaymentAttempt Create(Guid paymentId, AttemptOutcome outcome, DateTimeOffset now, Guid? userId, string? note) => new()
    {
        Id = Guid.CreateVersion7(),
        PaymentId = paymentId,
        Outcome = outcome,
        AttemptedAt = now,
        RecordedByUserId = userId,
        Note = string.IsNullOrWhiteSpace(note) ? null : note.Trim(),
    };
}
