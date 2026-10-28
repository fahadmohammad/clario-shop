using ClarioShop.Ordering.Domain;
using ClarioShop.Ordering.Persistence;

namespace ClarioShop.Ordering.Features.Orders;

/// <summary>One of a signed-in shopper's own orders; anyone else's order is not found, so ids reveal nothing.</summary>
public static class GetMyOrder
{
    public sealed record Query(Guid CustomerId, Guid OrderId);

    internal sealed class Handler(OrderingDbContext db) : ICommandHandler<Query, OrderDetails>
    {
        public async Task<Result<OrderDetails>> HandleAsync(Query query, CancellationToken cancellationToken) =>
            await db.FindOrderAsync(query.OrderId, cancellationToken) is { } order && order.CustomerId == query.CustomerId
                ? OrderDetails.ForShopper(order)
                : OrderingErrors.OrderNotFound;
    }
}
