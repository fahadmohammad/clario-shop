using ClarioShop.Reporting.Domain;
using ClarioShop.Reporting.Persistence;

namespace ClarioShop.Reporting.Features.Reports;

/// <summary>
/// Sales per store day: the orders confirmed that day, net of those later cancelled or returned. Every day of the
/// period is listed, with zeros on days without sales.
/// </summary>
public static class GetDailySales
{
    /// <param name="From">First day, e.g. <c>2026-10-01</c>; 29 days before <paramref name="To"/> when left out.</param>
    /// <param name="To">Last day; today in the store's time zone when left out.</param>
    public sealed record Query(DateOnly? From, DateOnly? To);

    /// <param name="Subtotal">The goods, before delivery and VAT.</param>
    /// <param name="Total">What shoppers pay: subtotal, delivery fees and VAT.</param>
    public sealed record SalesFigures(int Orders, decimal Subtotal, decimal DeliveryFees, decimal Vat, decimal Total);

    public sealed record SalesDay(DateOnly Date, SalesFigures Sales);

    public sealed record Response(DateOnly From, DateOnly To, IReadOnlyList<SalesDay> Days, SalesFigures Totals);

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
            var days = await db.SalesOrders.AsNoTracking()
                .Where(order => order.SoldOn >= period.From && order.SoldOn <= period.To)
                .Where(order => order.Status != SalesOrderStatus.Cancelled && order.Status != SalesOrderStatus.Returned)
                .GroupBy(order => order.SoldOn!.Value)
                .Select(day => new
                {
                    Date = day.Key,
                    Figures = new SalesFigures(
                        day.Count(),
                        day.Sum(order => order.Subtotal),
                        day.Sum(order => order.DeliveryFee),
                        day.Sum(order => order.Vat),
                        day.Sum(order => order.Total)),
                })
                .ToDictionaryAsync(day => day.Date, day => day.Figures, cancellationToken);

            var none = new SalesFigures(0, 0m, 0m, 0m, 0m);
            var listed = Enumerable.Range(period.From.DayNumber, period.To.DayNumber - period.From.DayNumber + 1)
                .Select(DateOnly.FromDayNumber)
                .Select(date => new SalesDay(date, days.GetValueOrDefault(date, none)))
                .ToList();
            var totals = new SalesFigures(
                days.Values.Sum(day => day.Orders),
                days.Values.Sum(day => day.Subtotal),
                days.Values.Sum(day => day.DeliveryFees),
                days.Values.Sum(day => day.Vat),
                days.Values.Sum(day => day.Total));
            return new Response(period.From, period.To, listed, totals);
        }
    }
}
