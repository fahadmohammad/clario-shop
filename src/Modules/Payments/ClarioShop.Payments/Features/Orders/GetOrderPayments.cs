using ClarioShop.Ordering.Contracts;
using ClarioShop.Payments.Domain;
using ClarioShop.Payments.Persistence;

namespace ClarioShop.Payments.Features.Orders;

public static class GetOrderPayments
{
    public sealed record Query(Guid OrderId);

    internal sealed class Handler(PaymentsDbContext db, IOrderQueries orders) : ICommandHandler<Query, OrderPayments>
    {
        public async Task<Result<OrderPayments>> HandleAsync(Query query, CancellationToken cancellationToken)
        {
            var order = await orders.GetForPaymentAsync(query.OrderId, cancellationToken);
            if (order is null)
            {
                return PaymentsErrors.OrderNotFound;
            }

            return OrderPayments.From(order, await db.PaymentsForOrderAsync(order.OrderId, cancellationToken));
        }
    }
}
