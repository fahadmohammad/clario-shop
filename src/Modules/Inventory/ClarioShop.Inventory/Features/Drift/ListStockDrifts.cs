using ClarioShop.Inventory.Domain;
using ClarioShop.Inventory.Persistence;

namespace ClarioShop.Inventory.Features.Drift;

/// <summary>Stock levels the drift check found out of step with the ledger and corrected, newest first.</summary>
public static class ListStockDrifts
{
    public sealed record Query(Guid? VariantId, Guid? LocationId, int Page = 1, int PageSize = 50);

    /// <param name="RecordedOnHand">What the stock level said before the correction.</param>
    /// <param name="LedgerOnHand">What the ledger adds up to; the level shows this now.</param>
    public sealed record DriftItem(
        Guid Id,
        Guid CheckId,
        Guid VariantId,
        Guid LocationId,
        decimal RecordedOnHand,
        decimal LedgerOnHand,
        decimal RecordedReserved,
        decimal HeldReserved,
        DateTimeOffset DetectedAt)
    {
        internal static DriftItem From(StockDrift drift) => new(
            drift.Id,
            drift.CheckId,
            drift.VariantId,
            drift.LocationId,
            drift.RecordedOnHand,
            drift.LedgerOnHand,
            drift.RecordedReserved,
            drift.HeldReserved,
            drift.DetectedAt);
    }

    public sealed record Response(IReadOnlyList<DriftItem> Items, int Page, int PageSize, int TotalCount);

    internal sealed class Validator : AbstractValidator<Query>
    {
        public Validator()
        {
            RuleFor(query => query.Page).GreaterThanOrEqualTo(1);
            RuleFor(query => query.PageSize).InclusiveBetween(1, 200);
        }
    }

    internal sealed class Handler(InventoryDbContext db) : ICommandHandler<Query, Response>
    {
        public async Task<Result<Response>> HandleAsync(Query query, CancellationToken cancellationToken)
        {
            var drifts = db.Drifts.AsNoTracking();
            if (query.VariantId is { } variantId)
            {
                drifts = drifts.Where(drift => drift.VariantId == variantId);
            }

            if (query.LocationId is { } locationId)
            {
                drifts = drifts.Where(drift => drift.LocationId == locationId);
            }

            var total = await drifts.CountAsync(cancellationToken);
            var page = await drifts
                .OrderByDescending(drift => drift.DetectedAt)
                .ThenByDescending(drift => drift.Id)
                .Skip((query.Page - 1) * query.PageSize)
                .Take(query.PageSize)
                .ToListAsync(cancellationToken);

            return new Response(page.Select(DriftItem.From).ToList(), query.Page, query.PageSize, total);
        }
    }
}
