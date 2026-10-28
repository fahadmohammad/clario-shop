using ClarioShop.SharedKernel.Domain;
using ClarioShop.SharedKernel.Results;

namespace ClarioShop.Payments.Domain;

/// <summary>
/// Money toward one order. An order may have several payments (part prepaid, part cash on delivery), so a payment
/// carries its own amount, not the order's total. Every try at collecting it is a <see cref="PaymentAttempt"/>.
/// </summary>
internal sealed class Payment : Entity
{
    /// <summary>Every amount is in Bangladeshi taka.</summary>
    public const string Currency = "BDT";

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

    /// <summary>
    /// A payment the shopper makes on a gateway's hosted page. It stays Pending until the gateway confirms it; each
    /// session opened for it is an attempt (<see cref="StartAttempt"/>), e.g. when the shopper retries.
    /// </summary>
    public static Payment StartOnline(Guid orderId, string orderNumber, PaymentMethod method, decimal amount, DateTimeOffset now)
    {
        if (method == PaymentMethod.CashOnDelivery)
        {
            throw new ArgumentException("Cash is recorded with CashCollected.", nameof(method));
        }

        if (amount <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(amount), amount, "A payment must be for more than zero.");
        }

        return new Payment
        {
            Id = Guid.CreateVersion7(),
            OrderId = orderId,
            OrderNumber = orderNumber,
            Method = method,
            Status = PaymentStatus.Pending,
            Amount = amount,
            CreatedAt = now,
        };
    }

    /// <summary>Opens a new gateway attempt; its id is the transaction id the gateway reports back.</summary>
    public PaymentAttempt StartAttempt(DateTimeOffset now)
    {
        if (Status != PaymentStatus.Pending)
        {
            throw new InvalidOperationException($"A {Status} payment takes no more attempts.");
        }

        var attempt = PaymentAttempt.Create(Id, AttemptOutcome.Pending, now, userId: null, note: null);
        attempts.Add(attempt);
        return attempt;
    }

    /// <summary>
    /// Settles the payment from what the gateway confirmed for one of its attempts. The receipt must be for exactly the
    /// amount asked, in taka, and not held for review. Settling the same attempt again changes nothing; another
    /// attempt paid after the payment already succeeded is recorded as paid twice, for staff to refund. A payment that
    /// had failed succeeds after all: the money arrived, even if the order is cancelled by now.
    /// </summary>
    public Result<SettleOutcome> Settle(Guid attemptId, GatewayReceipt receipt, DateTimeOffset now)
    {
        var attempt = attempts.SingleOrDefault(a => a.Id == attemptId)
            ?? throw new ArgumentException($"Attempt {attemptId} is not one of payment {Id}'s.", nameof(attemptId));

        if (attempt.Outcome == AttemptOutcome.Succeeded)
        {
            return SettleOutcome.AlreadySettled;
        }

        if (!string.Equals(receipt.Currency, Currency, StringComparison.OrdinalIgnoreCase) || receipt.Amount != Amount)
        {
            return PaymentsErrors.AmountMismatch(Amount, receipt.Amount, receipt.Currency);
        }

        if (receipt.IsRisky)
        {
            return PaymentsErrors.HeldForReview;
        }

        if (Status == PaymentStatus.Succeeded)
        {
            attempt.Succeed(receipt, now, "Paid again after the payment had already succeeded; refund this one.");
            return SettleOutcome.PaidTwice;
        }

        attempt.Succeed(receipt, now, note: null);
        Status = PaymentStatus.Succeeded;
        CompletedAt = now;
        return SettleOutcome.Settled;
    }

    /// <summary>
    /// The gateway has no paid try for any attempt and the shopper stopped trying: the payment fails, with every attempt
    /// still open. Fail the attempts the gateway gave a reason for first (<see cref="FailAttempt"/>). A payment the
    /// gateway confirms later still settles (<see cref="Settle"/>), so the money is never lost from view.
    /// </summary>
    public void Fail(string reason, DateTimeOffset now)
    {
        if (Status != PaymentStatus.Pending)
        {
            throw new InvalidOperationException($"A {Status} payment cannot fail.");
        }

        foreach (var attempt in attempts.Where(a => a.Outcome == AttemptOutcome.Pending))
        {
            attempt.Fail(reason, now);
        }

        Status = PaymentStatus.Failed;
        CompletedAt = now;
    }

    /// <summary>The attempt did not pay, e.g. the gateway would not open its session; the payment stays pending for another try.</summary>
    public void FailAttempt(Guid attemptId, string reason, DateTimeOffset now) =>
        attempts.Single(a => a.Id == attemptId).Fail(reason, now);
}
