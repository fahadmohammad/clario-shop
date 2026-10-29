using ClarioShop.Inventory.Persistence;

namespace ClarioShop.Inventory.Features.Transfers;

public static class GetStockTransfer
{
    public sealed record Query(Guid Id);

    public sealed record LineItem(Guid VariantId, string Sku, string ProductName, string VariantName, decimal Quantity);

    public sealed record Response(
        Guid Id,
        Guid FromLocationId,
        string FromLocationCode,
        Guid ToLocationId,
        string ToLocationCode,
        string? Note,
        DateTimeOffset RecordedAt,
        Guid? RecordedBy,
        IReadOnlyList<LineItem> Lines);

    internal sealed class Handler(InventoryDbContext db) : ICommandHandler<Query, Response>
    {
        private static readonly Error NotFound = Error.NotFound("inventory.transfer_not_found", "The stock transfer does not exist.");

        public async Task<Result<Response>> HandleAsync(Query query, CancellationToken cancellationToken)
        {
            var transfer = await db.Transfers.AsNoTracking().SingleOrDefaultAsync(t => t.Id == query.Id, cancellationToken);
            if (transfer is null)
            {
                return NotFound;
            }

            var codes = await db.Locations.AsNoTracking()
                .Where(location => location.Id == transfer.FromLocationId || location.Id == transfer.ToLocationId)
                .ToDictionaryAsync(location => location.Id, location => location.Code, cancellationToken);
            var variantIds = transfer.Lines.Select(line => line.VariantId).ToList();
            var names = await db.Items.AsNoTracking()
                .Where(item => variantIds.Contains(item.VariantId))
                .ToDictionaryAsync(item => item.VariantId, cancellationToken);

            var lines = transfer.Lines
                .Select(line => names.GetValueOrDefault(line.VariantId) is { } item
                    ? new LineItem(line.VariantId, item.Sku, item.ProductName, item.VariantName, line.Quantity)
                    : new LineItem(line.VariantId, string.Empty, string.Empty, string.Empty, line.Quantity))
                .OrderBy(line => line.Sku, StringComparer.Ordinal)
                .ToList();

            return new Response(
                transfer.Id,
                transfer.FromLocationId,
                codes[transfer.FromLocationId],
                transfer.ToLocationId,
                codes[transfer.ToLocationId],
                transfer.Note,
                transfer.RecordedAt,
                transfer.RecordedBy,
                lines);
        }
    }
}
