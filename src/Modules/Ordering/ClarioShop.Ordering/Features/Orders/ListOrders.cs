using ClarioShop.Ordering.Domain;
using ClarioShop.Ordering.Persistence;

namespace ClarioShop.Ordering.Features.Orders;

/// <summary>The staff order queue, newest first, e.g. every Placed order still to be confirmed.</summary>
public static class ListOrders
{
    /// <param name="Status">Only orders in this status, e.g. <c>Placed</c>.</param>
    public sealed record Query(string? Status, int Page = 1, int PageSize = 25);

    public sealed record OrderItem(
        Guid Id,
        string Number,
        string Status,
        string PaymentMethod,
        Guid CustomerId,
        string CustomerName,
        string CustomerPhone,
        decimal Total,
        string PaymentStatus,
        DateTimeOffset PlacedAt,
        DateTimeOffset UpdatedAt);

    public sealed record Response(IReadOnlyList<OrderItem> Items, int Page, int PageSize, int TotalCount);

    internal sealed class Validator : AbstractValidator<Query>
    {
        public Validator()
        {
            RuleFor(query => query.Page).GreaterThanOrEqualTo(1);
            RuleFor(query => query.PageSize).InclusiveBetween(1, 100);
            RuleFor(query => query.Status)
                .IsEnumName(typeof(OrderStatus), caseSensitive: false)
                .When(query => !string.IsNullOrEmpty(query.Status))
                .WithMessage($"Status must be one of: {string.Join(", ", Enum.GetNames<OrderStatus>())}.");
        }
    }

    internal sealed class Handler(OrderingDbContext db) : ICommandHandler<Query, Response>
    {
        public async Task<Result<Response>> HandleAsync(Query query, CancellationToken cancellationToken)
        {
            var orders = db.Orders.AsNoTracking();
            if (!string.IsNullOrEmpty(query.Status))
            {
                var status = Enum.Parse<OrderStatus>(query.Status, ignoreCase: true);
                orders = orders.Where(order => order.Status == status);
            }

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
                    order.CustomerId,
                    order.CustomerName,
                    order.CustomerPhone,
                    order.Total,
                    order.AmountPaid,
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
                    order.CustomerId,
                    order.CustomerName,
                    order.CustomerPhone,
                    order.Total,
                    Order.PaymentStatusFor(order.Total, order.AmountPaid).ToString(),
                    order.PlacedAt,
                    order.UpdatedAt))
                .ToList();
            return new Response(items, query.Page, query.PageSize, total);
        }
    }
}
