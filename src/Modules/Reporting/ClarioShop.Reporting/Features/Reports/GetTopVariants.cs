using ClarioShop.Reporting.Domain;
using ClarioShop.Reporting.Persistence;

namespace ClarioShop.Reporting.Features.Reports;

/// <summary>The best-selling variants over the sales of a period (orders confirmed in it, net of cancellations and returns).</summary>
public static class GetTopVariants
{
    public const string ByRevenue = "revenue";

    public const string ByQuantity = "quantity";

    /// <param name="From">First sale day; 29 days before <paramref name="To"/> when left out.</param>
    /// <param name="To">Last sale day; today in the store's time zone when left out.</param>
    /// <param name="SortBy"><c>revenue</c> (the default) or <c>quantity</c>.</param>
    /// <param name="Limit">How many variants, 1 to 50.</param>
    public sealed record Query(DateOnly? From, DateOnly? To, string? SortBy, int Limit = 10);

    /// <param name="ProductName">As on the variant's latest order, should the names have changed over the period.</param>
    /// <param name="Quantity">Units, or kg for items sold by weight.</param>
    /// <param name="Revenue">What the lines came to, before delivery and VAT.</param>
    /// <param name="Orders">How many orders the variant was on.</param>
    public sealed record VariantSales(
        Guid VariantId,
        string Sku,
        string ProductName,
        string VariantName,
        decimal Quantity,
        decimal Revenue,
        int Orders);

    public sealed record Response(DateOnly From, DateOnly To, string SortBy, IReadOnlyList<VariantSales> Items);

    internal sealed class Validator : AbstractValidator<Query>
    {
        public Validator()
        {
            RuleFor(query => query.Limit).InclusiveBetween(1, 50);
            RuleFor(query => query.SortBy)
                .Must(sortBy => string.Equals(sortBy, ByRevenue, StringComparison.OrdinalIgnoreCase) || string.Equals(sortBy, ByQuantity, StringComparison.OrdinalIgnoreCase))
                .When(query => !string.IsNullOrEmpty(query.SortBy))
                .WithMessage($"SortBy must be {ByRevenue} or {ByQuantity}.");
        }
    }

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
            var byQuantity = string.Equals(query.SortBy, ByQuantity, StringComparison.OrdinalIgnoreCase);
            var variants = db.SalesOrders.AsNoTracking()
                .Where(order => order.SoldOn >= period.From && order.SoldOn <= period.To)
                .Where(order => order.Status != SalesOrderStatus.Cancelled && order.Status != SalesOrderStatus.Returned)
                .SelectMany(order => order.Lines)
                .GroupBy(line => line.VariantId)
                .Select(variant => new
                {
                    VariantId = variant.Key,
                    Quantity = variant.Sum(line => line.Quantity),
                    Revenue = variant.Sum(line => line.LineTotal),
                    Orders = variant.Select(line => line.OrderId).Distinct().Count(),
                });

            var top = await (byQuantity
                    ? variants.OrderByDescending(variant => variant.Quantity).ThenByDescending(variant => variant.Revenue)
                    : variants.OrderByDescending(variant => variant.Revenue).ThenByDescending(variant => variant.Quantity))
                .ThenBy(variant => variant.VariantId)
                .Take(query.Limit)
                .ToListAsync(cancellationToken);

            var names = await LatestNamesAsync([.. top.Select(variant => variant.VariantId)], cancellationToken);
            var items = top
                .Select(variant =>
                {
                    var name = names[variant.VariantId];
                    return new VariantSales(variant.VariantId, name.Sku, name.ProductName, name.VariantName, variant.Quantity, variant.Revenue, variant.Orders);
                })
                .ToList();
            return new Response(period.From, period.To, byQuantity ? ByQuantity : ByRevenue, items);
        }

        /// <summary>The SKU and names on each variant's latest order.</summary>
        private async Task<Dictionary<Guid, (string Sku, string ProductName, string VariantName)>> LatestNamesAsync(
            Guid[] variantIds,
            CancellationToken cancellationToken)
        {
            var lines = await db.SalesOrders.AsNoTracking()
                .SelectMany(order => order.Lines, (order, line) => new { line.VariantId, line.Sku, line.ProductName, line.VariantName, order.PlacedAt })
                .Where(line => variantIds.Contains(line.VariantId))
                .GroupBy(line => line.VariantId)
                .Select(variant => variant.OrderByDescending(line => line.PlacedAt).First())
                .ToListAsync(cancellationToken);
            return lines.ToDictionary(line => line.VariantId, line => (line.Sku, line.ProductName, line.VariantName));
        }
    }
}
