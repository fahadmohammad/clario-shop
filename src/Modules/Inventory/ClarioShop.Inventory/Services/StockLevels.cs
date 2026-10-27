using ClarioShop.Inventory.Domain;
using ClarioShop.Inventory.Persistence;

namespace ClarioShop.Inventory.Services;

internal static class StockLevels
{
    /// <summary>Tracked stock levels for the variants at one location, creating (but not saving) missing rows.</summary>
    public static async Task<Dictionary<Guid, StockLevel>> LoadOrCreateAsync(
        this InventoryDbContext db,
        Guid locationId,
        IReadOnlyCollection<Guid> variantIds,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var levels = await db.StockLevels
            .Where(level => level.LocationId == locationId && variantIds.Contains(level.VariantId))
            .ToDictionaryAsync(level => level.VariantId, cancellationToken);

        foreach (var variantId in variantIds.Where(id => !levels.ContainsKey(id)))
        {
            var level = StockLevel.Create(variantId, locationId, now);
            db.StockLevels.Add(level);
            levels[variantId] = level;
        }

        return levels;
    }
}
