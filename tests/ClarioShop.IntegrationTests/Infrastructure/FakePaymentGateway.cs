using System.Collections.Concurrent;
using ClarioShop.Payments.Domain;
using ClarioShop.Payments.Gateways;
using ClarioShop.SharedKernel.Results;

namespace ClarioShop.IntegrationTests.Infrastructure;

/// <summary>
/// SSLCommerz without the network: opens sessions, lets a test pay on one, and validates what was paid the way the
/// validation API does (VALID the first time, VALIDATED after that, INVALID for anything it does not know).
/// Transaction ids are unique per attempt, so tests running in parallel never see each other's sessions.
/// </summary>
internal sealed class FakePaymentGateway : IPaymentGateway
{
    private readonly ConcurrentDictionary<string, GatewaySessionRequest> sessions = new();
    private readonly ConcurrentDictionary<string, PaidTransaction> paid = new();
    private readonly ConcurrentDictionary<string, int> unavailableValidations = new();
    private readonly ConcurrentDictionary<string, bool> refusedCustomers = new();

    public string Name => "SslCommerz";

    /// <summary>The session opened for a transaction id.</summary>
    public GatewaySessionRequest SessionFor(Guid transactionId) => sessions[transactionId.ToString()];

    /// <summary>The shopper pays on the gateway's page; returns the validation id the gateway reports (val_id).</summary>
    /// <param name="amount">What was paid; the session's amount when null.</param>
    public string Pay(Guid transactionId, decimal? amount = null, string currency = "BDT", bool risky = false)
    {
        var session = sessions[transactionId.ToString()];
        var validationId = $"VAL-{Guid.NewGuid():N}";
        paid[validationId] = new PaidTransaction(session.TransactionId, amount ?? session.Amount, currency, risky);
        return validationId;
    }

    /// <summary>The validation API does not answer for this validation id the next <paramref name="times"/> times.</summary>
    public void FailValidations(string validationId, int times) => unavailableValidations[validationId] = times;

    /// <summary>The gateway refuses to open sessions for orders to this customer name (unique per test).</summary>
    public void RefuseSessionsFor(string customerName) => refusedCustomers[customerName] = true;

    public Task<Result<GatewaySession>> CreateSessionAsync(GatewaySessionRequest request, CancellationToken cancellationToken)
    {
        if (refusedCustomers.ContainsKey(request.Customer.Name))
        {
            return Task.FromResult(Result.Failure<GatewaySession>(PaymentsErrors.GatewayRefused(Name)));
        }

        sessions[request.TransactionId] = request;
        return Task.FromResult(Result.Success(new GatewaySession($"SESS-{request.TransactionId}", new Uri($"https://sandbox.sslcommerz.test/pay/{request.TransactionId}"))));
    }

    public Task<Result<GatewayTransaction>> ValidateAsync(string validationId, CancellationToken cancellationToken)
    {
        if (unavailableValidations.TryGetValue(validationId, out var remaining) && remaining > 0)
        {
            unavailableValidations[validationId] = remaining - 1;
            return Task.FromResult(Result.Failure<GatewayTransaction>(PaymentsErrors.GatewayUnavailable(Name)));
        }

        if (!paid.TryGetValue(validationId, out var transaction))
        {
            return Task.FromResult(Result.Success(new GatewayTransaction(string.Empty, validationId, GatewayTransactionStatus.Invalid, 0m, string.Empty, null, null, false)));
        }

        var status = transaction.Validate() == 1 ? GatewayTransactionStatus.Valid : GatewayTransactionStatus.AlreadyValidated;
        return Task.FromResult(Result.Success(new GatewayTransaction(
            transaction.TransactionId,
            validationId,
            status,
            transaction.Amount,
            transaction.Currency,
            $"BANK-{validationId[4..12]}",
            "BKASH-BKash",
            transaction.Risky)));
    }

    private sealed record PaidTransaction(string TransactionId, decimal Amount, string Currency, bool Risky)
    {
        private int validations;

        /// <summary>How many times it has been validated, this time included.</summary>
        public int Validate() => Interlocked.Increment(ref validations);
    }
}
