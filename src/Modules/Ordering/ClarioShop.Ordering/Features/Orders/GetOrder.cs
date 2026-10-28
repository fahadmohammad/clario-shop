using ClarioShop.Ordering.Domain;
using ClarioShop.Ordering.Persistence;

namespace ClarioShop.Ordering.Features.Orders;

public static class GetOrder
{
    public sealed record Query(Guid Id);

    internal sealed class Handler(OrderingDbContext db) : ICommandHandler<Query, OrderDetails>
    {
        public async Task<Result<OrderDetails>> HandleAsync(Query query, CancellationToken cancellationToken) =>
            await db.FindOrderAsync(query.Id, cancellationToken) is { } order
                ? OrderDetails.From(order)
                : OrderingErrors.OrderNotFound;
    }
}
