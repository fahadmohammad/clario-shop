using System.Text.Json;
using ClarioShop.Payments.Contracts.Events;
using ClarioShop.Payments.Domain;
using ClarioShop.Payments.Gateways;
using ClarioShop.Payments.Persistence;
using Microsoft.Extensions.Logging;

namespace ClarioShop.Payments.Features.Gateways;

/// <summary>
/// Handles a gateway callback: the IPN, or the shopper's browser coming back from the payment page. The callback is
/// stored raw first (<see cref="GatewayWebhookLog"/>). A callback that claims a payment is only believed once the
/// gateway's validation API confirms it, for this transaction and the exact amount; then the payment succeeds and
/// <see cref="PaymentSucceeded"/> is published, once, however many callbacks arrive.
/// </summary>
internal sealed partial class GatewayCallbackProcessor(
    PaymentsDbContext db,
    IPaymentGateway gateway,
    TimeProvider clock,
    ILogger<GatewayCallbackProcessor> logger)
{
    private static readonly string[] PaidStatuses = ["VALID", "VALIDATED"];

    public async Task<GatewayCallbackResult> ProcessAsync(GatewayCallbackSource source, IReadOnlyDictionary<string, string> fields, CancellationToken cancellationToken)
    {
        var transactionId = fields.GetValueOrDefault("tran_id");
        var validationId = fields.GetValueOrDefault("val_id");
        var reportedStatus = fields.GetValueOrDefault("status");
        var log = GatewayWebhookLog.Receive(gateway.Name, source, JsonSerializer.Serialize(fields), transactionId, validationId, reportedStatus, clock.GetUtcNow());
        db.GatewayWebhookLogs.Add(log);
        await db.SaveChangesAsync(cancellationToken);

        var attempt = await FindAttemptAsync(log.TransactionId, cancellationToken);
        if (attempt is null)
        {
            return await CompleteAsync(log, GatewayCallbackOutcome.UnknownPayment, attempt, detail: null, cancellationToken);
        }

        var claimsPaid = source is GatewayCallbackSource.Ipn or GatewayCallbackSource.SuccessRedirect or GatewayCallbackSource.Reconciliation
            && PaidStatuses.Contains(log.ReportedStatus, StringComparer.OrdinalIgnoreCase)
            && log.ValidationId is not null;
        if (!claimsPaid)
        {
            // Failures are never taken from a callback alone; GatewayReconciliation asks the gateway about them.
            return await CompleteAsync(log, GatewayCallbackOutcome.NotPaid, attempt, detail: null, cancellationToken);
        }

        var validated = await gateway.ValidateAsync(log.ValidationId!, cancellationToken);
        if (validated.IsFailure)
        {
            var outcome = validated.Error.Type == ErrorType.Unavailable ? GatewayCallbackOutcome.GatewayUnavailable : GatewayCallbackOutcome.Rejected;
            return await CompleteAsync(log, outcome, attempt, validated.Error.Code, cancellationToken);
        }

        var transaction = validated.Value;
        if (transaction.Status == GatewayTransactionStatus.Invalid)
        {
            LogRejected(logger, log.Id, "the gateway does not confirm the payment");
            return await CompleteAsync(log, GatewayCallbackOutcome.Rejected, attempt, "payments.not_confirmed_by_gateway", cancellationToken);
        }

        if (transaction.TransactionId != attempt.AttemptId.ToString())
        {
            LogRejected(logger, log.Id, "the validated transaction is a different one");
            return await CompleteAsync(log, GatewayCallbackOutcome.Rejected, attempt, "payments.transaction_mismatch", cancellationToken);
        }

        return await SettleAsync(log, attempt, transaction, cancellationToken);
    }

    private async Task<GatewayCallbackResult> SettleAsync(GatewayWebhookLog log, AttemptRef attempt, GatewayTransaction transaction, CancellationToken cancellationToken)
    {
        await using var dbTransaction = await db.Database.BeginTransactionAsync(cancellationToken);
        await db.LockOrderAsync(attempt.OrderId, cancellationToken);
        var payment = await db.Payments.Include(p => p.Attempts).SingleAsync(p => p.Id == attempt.PaymentId, cancellationToken);

        var now = clock.GetUtcNow();
        var receipt = new GatewayReceipt(transaction.ValidationId, transaction.Amount, transaction.Currency, transaction.BankTransactionId, transaction.CardType, transaction.IsRisky);
        var settled = payment.Settle(attempt.AttemptId, receipt, now);
        if (settled.IsFailure)
        {
            LogRejected(logger, log.Id, settled.Error.Code);
            log.Complete(GatewayCallbackOutcome.Rejected, payment.Id, settled.Error.Code, now);
        }
        else
        {
            log.Complete(Outcome(settled.Value), payment.Id, detail: null, now);
            if (settled.Value == SettleOutcome.Settled)
            {
                db.Publish(new PaymentSucceeded(Guid.CreateVersion7(), now, payment.Id, payment.OrderId, payment.OrderNumber, payment.Method.ToString(), payment.Amount, now));
            }
            else if (settled.Value == SettleOutcome.PaidTwice)
            {
                LogPaidTwice(logger, payment.OrderNumber, payment.Id);
            }
        }

        await db.SaveChangesAsync(cancellationToken);
        await dbTransaction.CommitAsync(cancellationToken);
        return new GatewayCallbackResult(log.Outcome, attempt.OrderId);
    }

    /// <summary>The attempt a transaction id names, with its payment and order; null when it is not one of this store's.</summary>
    private async Task<AttemptRef?> FindAttemptAsync(string? transactionId, CancellationToken cancellationToken)
    {
        if (!Guid.TryParse(transactionId, out var attemptId))
        {
            return null;
        }

        return await db.Payments.AsNoTracking()
            .Where(payment => payment.Attempts.Any(a => a.Id == attemptId))
            .Select(payment => new AttemptRef(attemptId, payment.Id, payment.OrderId))
            .SingleOrDefaultAsync(cancellationToken);
    }

    private async Task<GatewayCallbackResult> CompleteAsync(
        GatewayWebhookLog log,
        GatewayCallbackOutcome outcome,
        AttemptRef? attempt,
        string? detail,
        CancellationToken cancellationToken)
    {
        log.Complete(outcome, attempt?.PaymentId, detail, clock.GetUtcNow());
        await db.SaveChangesAsync(cancellationToken);
        return new GatewayCallbackResult(outcome, attempt?.OrderId);
    }

    private static GatewayCallbackOutcome Outcome(SettleOutcome settled) => settled switch
    {
        SettleOutcome.Settled => GatewayCallbackOutcome.Settled,
        SettleOutcome.AlreadySettled => GatewayCallbackOutcome.AlreadySettled,
        SettleOutcome.PaidTwice => GatewayCallbackOutcome.PaidTwice,
        _ => throw new ArgumentOutOfRangeException(nameof(settled), settled, null),
    };

    [LoggerMessage(Level = LogLevel.Warning, Message = "Gateway callback {CallbackId} rejected: {Reason}")]
    private static partial void LogRejected(ILogger logger, Guid callbackId, string reason);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Order {OrderNumber} was paid twice online (payment {PaymentId}); refund the extra payment")]
    private static partial void LogPaidTwice(ILogger logger, string orderNumber, Guid paymentId);

    private sealed record AttemptRef(Guid AttemptId, Guid PaymentId, Guid OrderId);
}

/// <param name="OrderId">The order the callback's transaction belongs to; null when it names none of this store's.</param>
internal sealed record GatewayCallbackResult(GatewayCallbackOutcome Outcome, Guid? OrderId);
