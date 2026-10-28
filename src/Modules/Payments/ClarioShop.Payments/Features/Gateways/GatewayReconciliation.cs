using System.Globalization;
using ClarioShop.Payments.Contracts.Events;
using ClarioShop.Payments.Domain;
using ClarioShop.Payments.Gateways;
using ClarioShop.Payments.Persistence;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace ClarioShop.Payments.Features.Gateways;

/// <summary>
/// Checks one store's pending online payments with the gateway, by transaction id, for when a callback never came.
/// A paid try is settled exactly as its IPN would be (<see cref="GatewayCallbackProcessor"/>: validated first, once). A
/// payment with no paid try and no try still in progress fails once the shopper has stopped trying for
/// <see cref="GatewayReconciliationOptions.PaymentWindow"/>, publishing <see cref="PaymentFailed"/> so Ordering cancels
/// the order. Callbacks and redirects never fail a payment; only this check does.
/// </summary>
internal sealed partial class GatewayReconciliation(
    PaymentsDbContext db,
    IPaymentGateway gateway,
    GatewayCallbackProcessor processor,
    IOptions<GatewayReconciliationOptions> options,
    ILogger<GatewayReconciliation> logger)
{
    private static readonly GatewayCallbackOutcome[] PaidOutcomes = [GatewayCallbackOutcome.Settled, GatewayCallbackOutcome.AlreadySettled, GatewayCallbackOutcome.PaidTwice];

    public async Task<ReconciliationRun> ReconcileAsync(DateTimeOffset now, CancellationToken cancellationToken)
    {
        var method = Enum.Parse<PaymentMethod>(gateway.Name);
        var oldest = now - options.Value.MaxAge;
        var pending = await db.Payments.AsNoTracking()
            .Include(payment => payment.Attempts)
            .Where(payment => payment.Method == method && payment.Status == PaymentStatus.Pending && payment.CreatedAt >= oldest)
            .OrderBy(payment => payment.CreatedAt)
            .Take(options.Value.BatchSize)
            .ToListAsync(cancellationToken);

        var run = new ReconciliationRun();
        foreach (var payment in pending)
        {
            run = run.Add(await ReconcileAsync(payment, now, cancellationToken));
            db.ChangeTracker.Clear();
        }

        return run;
    }

    private async Task<ReconciliationOutcome> ReconcileAsync(Payment payment, DateTimeOffset now, CancellationToken cancellationToken)
    {
        var reasons = new Dictionary<Guid, string>();
        var inProgress = false;
        foreach (var attempt in payment.Attempts.Where(a => a.Outcome == AttemptOutcome.Pending))
        {
            var found = await gateway.FindAttemptsAsync(attempt.Id.ToString(), cancellationToken);
            if (found.IsFailure)
            {
                // Nothing is decided without the gateway's answer; the next run asks again.
                LogLookupFailed(logger, payment.OrderNumber, found.Error.Code);
                return ReconciliationOutcome.Unavailable;
            }

            if (found.Value.FirstOrDefault(a => a.State == GatewayAttemptState.Paid) is { } paid)
            {
                return await SettleAsync(attempt.Id, paid, cancellationToken);
            }

            inProgress |= found.Value.Any(a => a.State == GatewayAttemptState.InProgress);
            reasons[attempt.Id] = found.Value.Count == 0
                ? $"{gateway.Name} has no payment for this session."
                : $"{gateway.Name} reports {string.Join(", ", found.Value.Select(a => a.ReportedStatus).Distinct(StringComparer.Ordinal))}.";
        }

        return inProgress || !PastWindow(payment, now)
            ? ReconciliationOutcome.StillPending
            : await FailAsync(payment.Id, payment.OrderId, reasons, now, cancellationToken);
    }

    private async Task<ReconciliationOutcome> SettleAsync(Guid attemptId, GatewayAttempt paid, CancellationToken cancellationToken)
    {
        var fields = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["tran_id"] = attemptId.ToString(),
            ["val_id"] = paid.ValidationId!,
            ["status"] = paid.ReportedStatus,
        };
        var result = await processor.ProcessAsync(GatewayCallbackSource.Reconciliation, fields, cancellationToken);
        return result.Outcome switch
        {
            _ when PaidOutcomes.Contains(result.Outcome) => ReconciliationOutcome.Settled,
            GatewayCallbackOutcome.GatewayUnavailable => ReconciliationOutcome.Unavailable,

            // Paid, but not as asked (another amount, held as risky): never failed, it is for staff to sort out.
            _ => ReconciliationOutcome.StillPending,
        };
    }

    /// <summary>
    /// Fails the payment under the order's lock, unless a callback settled it or the shopper opened another attempt
    /// since it was read.
    /// </summary>
    private async Task<ReconciliationOutcome> FailAsync(Guid paymentId, Guid orderId, Dictionary<Guid, string> reasons, DateTimeOffset now, CancellationToken cancellationToken)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        await db.LockOrderAsync(orderId, cancellationToken);
        var payment = await db.Payments.Include(p => p.Attempts).SingleAsync(p => p.Id == paymentId, cancellationToken);
        if (payment.Status != PaymentStatus.Pending || !PastWindow(payment, now))
        {
            return ReconciliationOutcome.StillPending;
        }

        var reason = string.Create(CultureInfo.InvariantCulture, $"No payment confirmed by {gateway.Name} within {options.Value.PaymentWindow.TotalMinutes:0} minutes of the last try.");
        foreach (var (attemptId, attemptReason) in reasons.Where(pair => payment.Attempts.Any(a => a.Id == pair.Key && a.Outcome == AttemptOutcome.Pending)))
        {
            payment.FailAttempt(attemptId, attemptReason, now);
        }

        payment.Fail(reason, now);
        db.Publish(new PaymentFailed(Guid.CreateVersion7(), now, payment.Id, payment.OrderId, payment.OrderNumber, payment.Method.ToString(), payment.Amount, reason));
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        LogFailed(logger, payment.OrderNumber, payment.Id);
        return ReconciliationOutcome.Failed;
    }

    /// <summary>True when the shopper's last try (or the payment itself, with none) is older than the payment window.</summary>
    private bool PastWindow(Payment payment, DateTimeOffset now)
    {
        var lastTry = payment.Attempts.Count == 0 ? payment.CreatedAt : payment.Attempts.Max(a => a.AttemptedAt);
        return now - lastTry >= options.Value.PaymentWindow;
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "Could not check the payment for order {OrderNumber} with the gateway: {Reason}")]
    private static partial void LogLookupFailed(ILogger logger, string orderNumber, string reason);

    [LoggerMessage(Level = LogLevel.Information, Message = "The online payment {PaymentId} for order {OrderNumber} failed; the order will be cancelled")]
    private static partial void LogFailed(ILogger logger, string orderNumber, Guid paymentId);
}

internal enum ReconciliationOutcome
{
    /// <summary>The gateway had a paid try, now settled (or settled before by a callback).</summary>
    Settled,

    /// <summary>Nothing was paid and the shopper stopped trying; the payment failed.</summary>
    Failed,

    /// <summary>Not paid yet, but the shopper may still pay, or staff must look at it.</summary>
    StillPending,

    /// <summary>The gateway could not be asked; the next run tries again.</summary>
    Unavailable,
}

/// <summary>What one run did, for the job's log.</summary>
internal readonly record struct ReconciliationRun(int Settled, int Failed, int StillPending, int Unavailable)
{
    public ReconciliationRun Add(ReconciliationOutcome outcome) => outcome switch
    {
        ReconciliationOutcome.Settled => this with { Settled = Settled + 1 },
        ReconciliationOutcome.Failed => this with { Failed = Failed + 1 },
        ReconciliationOutcome.StillPending => this with { StillPending = StillPending + 1 },
        _ => this with { Unavailable = Unavailable + 1 },
    };
}
