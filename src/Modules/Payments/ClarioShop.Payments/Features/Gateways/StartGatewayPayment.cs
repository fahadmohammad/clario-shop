using ClarioShop.Ordering.Contracts;
using ClarioShop.Payments.Domain;
using ClarioShop.Payments.Gateways;
using ClarioShop.Payments.Persistence;
using ClarioShop.Settings.Contracts;

namespace ClarioShop.Payments.Features.Gateways;

/// <summary>
/// Opens a payment session on the gateway for a prepaid order waiting for its payment, and returns the gateway's page
/// to send the shopper to. Calling it again (the shopper retries, or comes back later) opens a new session for the same
/// pending payment. The payment only succeeds when the gateway confirms it (<see cref="GatewayCallbackProcessor"/>).
/// </summary>
public static class StartGatewayPayment
{
    /// <param name="CallbackBase">The store's own address (scheme and host) the gateway calls back on.</param>
    public sealed record Command(Guid OrderId, Uri CallbackBase);

    /// <param name="TransactionId">Our id for this session at the gateway.</param>
    /// <param name="RedirectUrl">The gateway's payment page; send the shopper there.</param>
    public sealed record Response(Guid PaymentId, Guid TransactionId, decimal Amount, Uri RedirectUrl);

    internal sealed class Handler(
        PaymentsDbContext db,
        IOrderQueries orders,
        IPaymentGateway gateway,
        ISettingsQueries settings,
        TimeProvider clock) : ICommandHandler<Command, Response>
    {
        public async Task<Result<Response>> HandleAsync(Command command, CancellationToken cancellationToken)
        {
            if (await orders.GetForGatewayAsync(command.OrderId, cancellationToken) is not { } details)
            {
                return PaymentsErrors.OrderNotFound;
            }

            var order = details.Order;
            if (order.PaymentMethod != gateway.Name)
            {
                return PaymentsErrors.NotPrepaid;
            }

            if (order.Status != OrderStatuses.PendingPayment)
            {
                return PaymentsErrors.NotAwaitingPayment(order.Status);
            }

            var now = clock.GetUtcNow();
            Payment payment;
            PaymentAttempt attempt;
            await using (var transaction = await db.Database.BeginTransactionAsync(cancellationToken))
            {
                await db.LockOrderAsync(order.OrderId, cancellationToken);
                var payments = await db.Payments.Include(p => p.Attempts).Where(p => p.OrderId == order.OrderId).ToListAsync(cancellationToken);
                var method = Enum.Parse<PaymentMethod>(gateway.Name);
                if (payments.Any(p => p.Method == method && p.Status == PaymentStatus.Failed))
                {
                    // Ordering is about to cancel the order; a new payment would only be one more to refund.
                    return PaymentsErrors.PaymentFailed;
                }

                var balance = OrderBalance.Of(order.Total, payments);
                if (balance.Outstanding == 0m)
                {
                    return PaymentsErrors.ExceedsOutstanding(balance.Outstanding, 0m);
                }

                payment = payments.FirstOrDefault(p => p.Method == method && p.Status == PaymentStatus.Pending)
                    ?? db.Payments.Add(Payment.StartOnline(order.OrderId, order.Number, method, balance.Outstanding, now)).Entity;
                attempt = payment.StartAttempt(now);
                await db.SaveChangesAsync(cancellationToken);
                await transaction.CommitAsync(cancellationToken);
            }

            // The gateway is called outside the transaction, so a slow gateway never holds the order's lock.
            var session = await gateway.CreateSessionAsync(await SessionRequestAsync(details, payment, attempt, command.CallbackBase, cancellationToken), cancellationToken);
            if (session.IsFailure)
            {
                payment.FailAttempt(attempt.Id, session.Error.Code, clock.GetUtcNow());
                await db.SaveChangesAsync(CancellationToken.None);
                return session.Error;
            }

            attempt.SessionOpened(session.Value.SessionKey);
            await db.SaveChangesAsync(CancellationToken.None);
            return new Response(payment.Id, attempt.Id, payment.Amount, session.Value.RedirectUrl);
        }

        private async Task<GatewaySessionRequest> SessionRequestAsync(
            OrderGatewayInfo details,
            Payment payment,
            PaymentAttempt attempt,
            Uri callbackBase,
            CancellationToken cancellationToken)
        {
            // The gateway insists on an email; shoppers sign in by phone, so the store's contact address stands in.
            var email = (await settings.GetStoreSettingsAsync(cancellationToken)).Profile.ContactEmail ?? $"orders@{callbackBase.Host}";
            return new GatewaySessionRequest(
                attempt.Id.ToString(),
                payment.Amount,
                Payment.Currency,
                details.Order.Number,
                details.ItemCount,
                new GatewayCustomer(details.CustomerName, details.CustomerPhone, email, details.AddressLine, details.City, details.PostalCode),
                GatewayCallbackRoutes.For(gateway.Name, callbackBase));
        }
    }
}
