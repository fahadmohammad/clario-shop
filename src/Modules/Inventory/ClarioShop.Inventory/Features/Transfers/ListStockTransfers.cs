using ClarioShop.Inventory.Persistence;

namespace ClarioShop.Inventory.Features.Transfers;

/// <summary>Stock transfers, newest first.</summary>
public static class ListStockTransfers
{
    /// <param name="LocationId">Transfers leaving or arriving at this location.</param>
    /// <param name="VariantId">Transfers with a line for this variant.</param>
    public sealed record Query(Guid? LocationId, Guid? VariantId, int Page = 1, int PageSize = 50);

    public sealed record TransferItem(
        Guid Id,
        Guid FromLocationId,
        string FromLocationCode,
        Guid ToLocationId,
        string ToLocationCode,
        int LineCount,
        decimal TotalQuantity,
        string? Note,
        DateTimeOffset RecordedAt,
        Guid? RecordedBy);

    public sealed record Response(IReadOnlyList<TransferItem> Items, int Page, int PageSize, int TotalCount);

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
            var transfers = db.Transfers.AsNoTracking();
            if (query.LocationId is { } locationId)
            {
                transfers = transfers.Where(transfer => transfer.FromLocationId == locationId || transfer.ToLocationId == locationId);
            }

            if (query.VariantId is { } variantId)
            {
                transfers = transfers.Where(transfer => transfer.Lines.Any(line => line.VariantId == variantId));
            }

            var total = await transfers.CountAsync(cancellationToken);
            var page = await transfers
                .OrderByDescending(transfer => transfer.RecordedAt)
                .ThenByDescending(transfer => transfer.Id)
                .Skip((query.Page - 1) * query.PageSize)
                .Take(query.PageSize)
                .Select(transfer => new
                {
                    transfer.Id,
                    transfer.FromLocationId,
                    transfer.ToLocationId,
                    LineCount = transfer.Lines.Count,
                    TotalQuantity = transfer.Lines.Sum(line => line.Quantity),
                    transfer.Note,
                    transfer.RecordedAt,
                    transfer.RecordedBy,
                })
                .ToListAsync(cancellationToken);
            var codes = await db.Locations.AsNoTracking().ToDictionaryAsync(location => location.Id, location => location.Code, cancellationToken);

            var items = page.Select(transfer => new TransferItem(
                    transfer.Id,
                    transfer.FromLocationId,
                    codes[transfer.FromLocationId],
                    transfer.ToLocationId,
                    codes[transfer.ToLocationId],
                    transfer.LineCount,
                    transfer.TotalQuantity,
                    transfer.Note,
                    transfer.RecordedAt,
                    transfer.RecordedBy))
                .ToList();
            return new Response(items, query.Page, query.PageSize, total);
        }
    }
}
