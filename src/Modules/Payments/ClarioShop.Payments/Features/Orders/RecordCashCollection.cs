using ClarioShop.Ordering.Contracts;
using ClarioShop.Payments.Contracts.Events;
using ClarioShop.Payments.Domain;
using ClarioShop.Payments.Persistence;

namespace ClarioShop.Payments.Features.Orders;

/// <summary>
/// A rider or staff member records cash the shopper paid on delivery. Several collections may add up to the order's
/// total (e.g. the rest of a part-prepaid order), never more. Publishes <see cref="PaymentSucceeded"/>.
/// </summary>
public static class RecordCashCollection
{
    public sealed record Request(decimal Amount, string? Note);

    public sealed record Command(Guid OrderId, decimal Amount, string? Note, Guid? UserId);

    internal sealed class Validator : AbstractValidator<Request>
    {
        public Validator()
        {
            RuleFor(request => request.Amount).GreaterThan(0m).PrecisionScale(18, 2, ignoreTrailingZeros: true);
            RuleFor(request => request.Note).MaximumLength(500);
        }
    }

    internal sealed class Handler(PaymentsDbContext db, IOrderQueries orders, TimeProvider clock) : ICommandHandler<Command, OrderPayments>
    {
        public async Task<Result<OrderPayments>> HandleAsync(Command command, CancellationToken cancellationToken)
        {
            var order = await orders.GetForPaymentAsync(command.OrderId, cancellationToken);
            if (order is null)
            {
                return PaymentsErrors.OrderNotFound;
            }

            if (order.Status is not (OrderStatuses.Shipped or OrderStatuses.Delivered))
            {
                return PaymentsErrors.NotOutForDelivery(order.Status);
            }

            await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
            await db.LockOrderAsync(order.OrderId, cancellationToken);

            var payments = await db.PaymentsForOrderAsync(order.OrderId, cancellationToken);
            var accepted = OrderBalance.Of(order.Total, payments).CanAccept(command.Amount);
            if (accepted.IsFailure)
            {
                return accepted.Error;
            }

            var now = clock.GetUtcNow();
            var payment = Payment.CashCollected(order.OrderId, order.Number, command.Amount, command.UserId, command.Note, now);
            db.Payments.Add(payment);
            db.Publish(new PaymentSucceeded(
                Guid.CreateVersion7(),
                now,
                payment.Id,
                order.OrderId,
                order.Number,
                payment.Method.ToString(),
                payment.Amount,
                now));
            await db.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);

            payments.Add(payment);
            return OrderPayments.From(order, payments);
        }
    }
}
