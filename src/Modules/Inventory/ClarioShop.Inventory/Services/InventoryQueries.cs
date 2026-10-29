using System.Linq.Expressions;
using ClarioShop.Inventory.Contracts;
using ClarioShop.Inventory.Domain;
using ClarioShop.Inventory.Persistence;
using Microsoft.Extensions.Options;

namespace ClarioShop.Inventory.Services;

internal sealed class InventoryQueries(InventoryDbContext db, IOptions<InventoryOptions> options) : IInventoryQueries
{
    public async Task<Result<StockValuation>> GetStockValuationAsync(StockReportQuery query, CancellationToken cancellationToken)
    {
        var locations = await LocationsInScopeAsync(query.LocationId, cancellationToken);
        if (locations.IsFailure)
        {
            return locations.Error;
        }

        // Weighted average of every receipt: what was paid in total over how much was received.
        var costs = db.Movements.AsNoTracking()
            .Where(movement => movement.Type == MovementType.PurchaseReceipt && movement.UnitCost != null)
            .GroupBy(movement => movement.VariantId)
            .Select(variant => new
            {
                VariantId = variant.Key,
                Cost = variant.Sum(movement => movement.Quantity * movement.UnitCost!.Value) / variant.Sum(movement => movement.Quantity),
            });

        var rows = from row in Rows(query.LocationId).Where(row => row.Level.OnHand > 0)
                   join cost in costs on row.Item.VariantId equals cost.VariantId into matched
                   from cost in matched.DefaultIfEmpty()
                   select new
                   {
                       row.Item,
                       row.Location,
                       row.Level.OnHand,
                       Cost = (decimal?)cost.Cost,
                   };

        var valued = rows.Select(row => new
        {
            row.Item,
            row.Location,
            row.OnHand,
            UnitCost = row.Cost == null ? null : (decimal?)Math.Round(row.Cost.Value, 2),
            Value = row.Cost == null ? null : (decimal?)Math.Round(row.OnHand * row.Cost.Value, 2),
        });

        var totals = await valued
            .GroupBy(row => row.Location.Id)
            .Select(location => new
            {
                LocationId = location.Key,
                Value = location.Sum(row => row.Value ?? 0),
                Uncosted = location.Count(row => row.Value == null),
            })
            .ToDictionaryAsync(location => location.LocationId, cancellationToken);

        var total = await valued.CountAsync(cancellationToken);
        var items = await valued
            .OrderBy(row => row.Value == null)
            .ThenByDescending(row => row.Value)
            .ThenBy(row => row.Item.Sku)
            .ThenBy(row => row.Location.Code)
            .Skip((query.Page - 1) * query.PageSize)
            .Take(query.PageSize)
            .Select(row => new StockValueItem(
                row.Item.VariantId,
                row.Item.Sku,
                row.Item.ProductName,
                row.Item.VariantName,
                row.Location.Id,
                row.Location.Code,
                row.OnHand,
                row.UnitCost,
                row.Value))
            .ToListAsync(cancellationToken);

        var byLocation = locations.Value
            .Select(location => totals.TryGetValue(location.Id, out var sum)
                ? new LocationStockValue(location.Id, location.Code, sum.Value, sum.Uncosted)
                : new LocationStockValue(location.Id, location.Code, 0, 0))
            .ToList();
        return new StockValuation(
            byLocation.Sum(location => location.Value),
            byLocation.Sum(location => location.UncostedItems),
            byLocation,
            items,
            query.Page,
            query.PageSize,
            total);
    }

    public async Task<Result<StockAlertPage>> GetLowStockAsync(StockReportQuery query, CancellationToken cancellationToken)
    {
        var fallback = options.Value.DefaultReorderLevel;
        return await AlertPageAsync(
            query,
            row => row.Level.OnHand - row.Level.Reserved <= (row.Item.ReorderLevel ?? fallback),
            row => row.Level.OnHand - row.Level.Reserved,
            cancellationToken);
    }

    public Task<Result<StockAlertPage>> GetNegativeStockAsync(StockReportQuery query, CancellationToken cancellationToken) =>
        AlertPageAsync(query, row => row.Level.OnHand < 0, row => row.Level.OnHand, cancellationToken);

    private async Task<Result<StockAlertPage>> AlertPageAsync(
        StockReportQuery query,
        Expression<Func<StockRow, bool>> alert,
        Expression<Func<StockRow, decimal>> lowestFirst,
        CancellationToken cancellationToken)
    {
        var locations = await LocationsInScopeAsync(query.LocationId, cancellationToken);
        if (locations.IsFailure)
        {
            return locations.Error;
        }

        var fallback = options.Value.DefaultReorderLevel;
        var rows = Rows(query.LocationId).Where(alert);
        var total = await rows.CountAsync(cancellationToken);
        var items = await rows
            .OrderBy(lowestFirst)
            .ThenBy(row => row.Item.Sku)
            .ThenBy(row => row.Location.Code)
            .Skip((query.Page - 1) * query.PageSize)
            .Take(query.PageSize)
            .Select(row => new StockAlertItem(
                row.Item.VariantId,
                row.Item.Sku,
                row.Item.ProductName,
                row.Item.VariantName,
                row.Location.Id,
                row.Location.Code,
                row.Level.OnHand,
                row.Level.Reserved,
                row.Level.OnHand - row.Level.Reserved,
                row.Item.ReorderLevel ?? fallback))
            .ToListAsync(cancellationToken);
        return new StockAlertPage(items, query.Page, query.PageSize, total);
    }

    /// <summary>The active locations a report covers: the one asked for, or all of them.</summary>
    private async Task<Result<List<(Guid Id, string Code)>>> LocationsInScopeAsync(Guid? locationId, CancellationToken cancellationToken)
    {
        var locations = await db.Locations.AsNoTracking()
            .Where(location => location.IsActive && (locationId == null || location.Id == locationId))
            .OrderByDescending(location => location.IsDefault)
            .ThenBy(location => location.Code)
            .Select(location => new { location.Id, location.Code })
            .ToListAsync(cancellationToken);

        if (locationId is not null && locations.Count == 0)
        {
            return InventoryErrors.LocationNotFound;
        }

        return locations.Select(location => (location.Id, location.Code)).ToList();
    }

    private IQueryable<StockRow> Rows(Guid? locationId) =>
        from level in db.StockLevels.AsNoTracking()
        join item in db.Items.AsNoTracking() on level.VariantId equals item.VariantId
        join location in db.Locations.AsNoTracking() on level.LocationId equals location.Id
        where location.IsActive && (locationId == null || location.Id == locationId)
        select new StockRow { Level = level, Item = item, Location = location };

    private sealed class StockRow
    {
        public required StockLevel Level { get; init; }

        public required InventoryItem Item { get; init; }

        public required Location Location { get; init; }
    }
}
