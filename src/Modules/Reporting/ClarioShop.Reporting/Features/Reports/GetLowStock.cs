using ClarioShop.Inventory.Contracts;

namespace ClarioShop.Reporting.Features.Reports;

/// <summary>Stock available at or below its reorder level (including stock that ran out), lowest first.</summary>
public static class GetLowStock
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

    internal sealed class Handler(IInventoryQueries inventory) : ICommandHandler<Query, StockAlertPage>
    {
        public Task<Result<StockAlertPage>> HandleAsync(Query query, CancellationToken cancellationToken) =>
            inventory.GetLowStockAsync(new StockReportQuery(query.LocationId, query.Page, query.PageSize), cancellationToken);
    }
}
