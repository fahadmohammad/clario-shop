using ClarioShop.Inventory.Contracts;

namespace ClarioShop.Reporting.Features.Reports;

/// <summary>What the stock on hand cost, per location and per variant, from Inventory through <see cref="IInventoryQueries"/>.</summary>
public static class GetStockValuation
{
    /// <param name="LocationId">One location, or every active location when left out.</param>
    public sealed record Query(Guid? LocationId, int Page = 1, int PageSize = 50);

    internal sealed class Validator : AbstractValidator<Query>
    {
        public Validator()
        {
            RuleFor(query => query.Page).GreaterThanOrEqualTo(1);
            RuleFor(query => query.PageSize).InclusiveBetween(1, 200);
        }
    }

    internal sealed class Handler(IInventoryQueries inventory) : ICommandHandler<Query, StockValuation>
    {
        public Task<Result<StockValuation>> HandleAsync(Query query, CancellationToken cancellationToken) =>
            inventory.GetStockValuationAsync(new StockReportQuery(query.LocationId, query.Page, query.PageSize), cancellationToken);
    }
}
