using ClarioShop.SharedKernel.Domain;

namespace ClarioShop.Payments.Domain;

/// <summary>
/// Money toward one order. An order may have several payments (part prepaid, part cash on delivery), so a payment
/// carries its own amount, not the order's total. Every try at collecting it is a <see cref="PaymentAttempt"/>.
/// </summary>
internal sealed class Payment : Entity
{
    private readonly List<PaymentAttempt> attempts = [];

    private Payment()
    {
    }

    public Guid OrderId { get; private set; }

    /// <summary>Copied from the order for staff lists, e.g. <c>CS-000123</c>.</summary>
    public string OrderNumber { get; private set; } = string.Empty;

    public PaymentMethod Method { get; private set; }

    public PaymentStatus Status { get; private set; }

    public decimal Amount { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    /// <summary>When the payment succeeded or failed; null while pending.</summary>
    public DateTimeOffset? CompletedAt { get; private set; }

    /// <summary>Concurrency token (PostgreSQL xmin).</summary>
    public uint RowVersion { get; private set; }

    public IReadOnlyCollection<PaymentAttempt> Attempts => attempts;

    /// <summary>Cash handed to the rider or courier on delivery: succeeded as soon as it is recorded.</summary>
    public static Payment CashCollected(Guid orderId, string orderNumber, decimal amount, Guid? userId, string? note, DateTimeOffset now)
    {
        if (amount <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(amount), amount, "A payment must be for more than zero.");
        }

        var payment = new Payment
        {
            Id = Guid.CreateVersion7(),
            OrderId = orderId,
            OrderNumber = orderNumber,
            Method = PaymentMethod.CashOnDelivery,
            Status = PaymentStatus.Succeeded,
            Amount = amount,
            CreatedAt = now,
            CompletedAt = now,
        };
        payment.attempts.Add(PaymentAttempt.Create(payment.Id, AttemptOutcome.Succeeded, now, userId, note));
        return payment;
    }
}
