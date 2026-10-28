using ClarioShop.Ordering.Domain;
using ClarioShop.Ordering.Persistence;

namespace ClarioShop.Ordering.Features.Orders;

/// <summary>A signed-in shopper's own orders, newest first.</summary>
public static class ListMyOrders
{
    public sealed record Request(int Page = 1, int PageSize = 20);

    public sealed record Query(Guid CustomerId, int Page, int PageSize);

    public sealed record OrderItem(
        Guid Id,
        string Number,
        string Status,
        string PaymentMethod,
        int LineCount,
        decimal Total,
        DateTimeOffset PlacedAt,
        DateTimeOffset UpdatedAt);

    public sealed record Response(IReadOnlyList<OrderItem> Items, int Page, int PageSize, int TotalCount);

    internal sealed class Validator : AbstractValidator<Request>
    {
        public Validator()
        {
            RuleFor(request => request.Page).GreaterThanOrEqualTo(1);
            RuleFor(request => request.PageSize).InclusiveBetween(1, 100);
        }
    }

    internal sealed class Handler(OrderingDbContext db) : ICommandHandler<Query, Response>
    {
        public async Task<Result<Response>> HandleAsync(Query query, CancellationToken cancellationToken)
        {
            var orders = db.Orders.AsNoTracking().Where(order => order.CustomerId == query.CustomerId);

            var total = await orders.CountAsync(cancellationToken);
            var rows = await orders
                .OrderByDescending(order => order.Number)
                .Skip((query.Page - 1) * query.PageSize)
                .Take(query.PageSize)
                .Select(order => new
                {
                    order.Id,
                    order.Number,
                    order.Status,
                    order.PaymentMethod,
                    LineCount = order.Lines.Count,
                    order.Total,
                    order.PlacedAt,
                    order.UpdatedAt,
                })
                .ToListAsync(cancellationToken);

            var items = rows
                .Select(order => new OrderItem(
                    order.Id,
                    OrderNumber.Format(order.Number),
                    order.Status.ToString(),
                    order.PaymentMethod.ToString(),
                    order.LineCount,
                    order.Total,
                    order.PlacedAt,
                    order.UpdatedAt))
                .ToList();
            return new Response(items, query.Page, query.PageSize, total);
        }
    }
}
