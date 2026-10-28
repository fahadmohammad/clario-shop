using ClarioShop.Reporting.Domain;
using ClarioShop.Reporting.Persistence;

namespace ClarioShop.Reporting.Features.Reports;

/// <summary>How the orders placed in a period stand now: how many are in each status, and what they are worth.</summary>
public static class GetOrdersByStatus
{
    /// <param name="From">First day the orders were placed on; 29 days before <paramref name="To"/> when left out.</param>
    /// <param name="To">Last day; today in the store's time zone when left out.</param>
    public sealed record Query(DateOnly? From, DateOnly? To);

    /// <param name="Total">What the orders are worth, delivery and VAT included.</param>
    public sealed record StatusCount(string Status, int Orders, decimal Total);

    /// <param name="Statuses">Every status in lifecycle order, with zeros for those no order is in.</param>
    public sealed record Response(DateOnly From, DateOnly To, IReadOnlyList<StatusCount> Statuses, int TotalOrders);

    internal sealed class Handler(ReportingDbContext db, StoreCalendar calendar, TimeProvider clock) : ICommandHandler<Query, Response>
    {
        public async Task<Result<Response>> HandleAsync(Query query, CancellationToken cancellationToken)
        {
            var resolved = ReportPeriod.Resolve(query.From, query.To, calendar, clock);
            if (resolved.IsFailure)
            {
                return resolved.Error;
            }

            var period = resolved.Value;
            var counts = await db.SalesOrders.AsNoTracking()
                .Where(order => order.PlacedOn >= period.From && order.PlacedOn <= period.To)
                .GroupBy(order => order.Status)
                .Select(status => new { Status = status.Key, Orders = status.Count(), Total = status.Sum(order => order.Total) })
                .ToDictionaryAsync(status => status.Status, cancellationToken);

            var statuses = Enum.GetValues<SalesOrderStatus>()
                .Select(status => counts.TryGetValue(status, out var count)
                    ? new StatusCount(status.ToString(), count.Orders, count.Total)
                    : new StatusCount(status.ToString(), 0, 0m))
                .ToList();
            return new Response(period.From, period.To, statuses, statuses.Sum(status => status.Orders));
        }
    }
}
