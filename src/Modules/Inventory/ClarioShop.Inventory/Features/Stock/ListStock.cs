using ClarioShop.Inventory.Persistence;

namespace ClarioShop.Inventory.Features.Stock;

public static class ListStock
{
    public sealed record Query(Guid? LocationId, Guid? VariantId, string? Search, int Page = 1, int PageSize = 50);

    public sealed record StockItem(
        Guid VariantId,
        string Sku,
        string ProductName,
        string VariantName,
        Guid LocationId,
        string LocationCode,
        decimal OnHand,
        decimal Reserved,
        decimal Available);

    public sealed record Response(IReadOnlyList<StockItem> Items, int Page, int PageSize, int TotalCount);

    internal sealed class Validator : AbstractValidator<Query>
    {
        public Validator()
        {
            RuleFor(query => query.Page).GreaterThanOrEqualTo(1);
            RuleFor(query => query.PageSize).InclusiveBetween(1, 200);
            RuleFor(query => query.Search).MaximumLength(100);
        }
    }

    internal sealed class Handler(InventoryDbContext db) : ICommandHandler<Query, Response>
    {
        public async Task<Result<Response>> HandleAsync(Query query, CancellationToken cancellationToken)
        {
            var rows = from level in db.StockLevels.AsNoTracking()
                       join item in db.Items.AsNoTracking() on level.VariantId equals item.VariantId
                       join location in db.Locations.AsNoTracking() on level.LocationId equals location.Id
                       select new { level, item, location };

            if (query.LocationId is { } locationId)
            {
                rows = rows.Where(row => row.level.LocationId == locationId);
            }

            if (query.VariantId is { } variantId)
            {
                rows = rows.Where(row => row.level.VariantId == variantId);
            }

            if (!string.IsNullOrWhiteSpace(query.Search))
            {
                var pattern = $"%{EscapeLike(query.Search.Trim())}%";
                rows = rows.Where(row => EF.Functions.ILike(row.item.Sku, pattern, "\\") || EF.Functions.ILike(row.item.ProductName, pattern, "\\"));
            }

            var total = await rows.CountAsync(cancellationToken);
            var items = await rows
                .OrderBy(row => row.item.ProductName)
                .ThenBy(row => row.item.Sku)
                .ThenBy(row => row.location.Code)
                .Skip((query.Page - 1) * query.PageSize)
                .Take(query.PageSize)
                .Select(row => new StockItem(
                    row.item.VariantId,
                    row.item.Sku,
                    row.item.ProductName,
                    row.item.VariantName,
                    row.location.Id,
                    row.location.Code,
                    row.level.OnHand,
                    row.level.Reserved,
                    row.level.OnHand - row.level.Reserved))
                .ToListAsync(cancellationToken);

            return new Response(items, query.Page, query.PageSize, total);
        }

        private static string EscapeLike(string value) =>
            value.Replace("\\", "\\\\", StringComparison.Ordinal)
                .Replace("%", "\\%", StringComparison.Ordinal)
                .Replace("_", "\\_", StringComparison.Ordinal);
    }
}
