using ClarioShop.Catalog.Contracts;
using ClarioShop.Inventory.Domain;
using ClarioShop.Inventory.Persistence;

namespace ClarioShop.Inventory.Services;

/// <summary>
/// Knows which catalog variants Inventory tracks. Items normally arrive through ProductCreated; when stock
/// is recorded before that event is delivered, the variant is looked up through Catalog's contract instead.
/// </summary>
internal sealed class ItemRegistry(InventoryDbContext db, ICatalogQueries catalog, TimeProvider clock)
{
    /// <summary>Adds items that are new to Inventory; returns how many were added. Call SaveChanges after.</summary>
    public async Task<int> AddMissingAsync(IReadOnlyCollection<InventoryItem> items, CancellationToken cancellationToken)
    {
        var ids = items.Select(item => item.VariantId).ToList();
        var known = await db.Items.Where(item => ids.Contains(item.VariantId)).Select(item => item.VariantId).ToListAsync(cancellationToken);
        var added = items.Where(item => !known.Contains(item.VariantId)).DistinctBy(item => item.VariantId).ToList();
        db.Items.AddRange(added);
        return added.Count;
    }

    /// <summary>Makes sure every variant is tracked. Fails with the ids neither Inventory nor Catalog knows.</summary>
    public async Task<Result<bool>> EnsureKnownAsync(IReadOnlyCollection<Guid> variantIds, CancellationToken cancellationToken)
    {
        var ids = variantIds.Distinct().ToList();
        var known = await db.Items.Where(item => ids.Contains(item.VariantId)).Select(item => item.VariantId).ToListAsync(cancellationToken);
        var missing = ids.Except(known).ToList();
        if (missing.Count == 0)
        {
            return true;
        }

        var now = clock.GetUtcNow();
        var fromCatalog = await catalog.GetVariantsAsync(missing, cancellationToken);
        var unknown = missing.Except(fromCatalog.Select(variant => variant.VariantId)).ToList();
        if (unknown.Count > 0)
        {
            return InventoryErrors.UnknownVariants(unknown);
        }

        db.Items.AddRange(fromCatalog.Select(variant =>
            InventoryItem.Create(variant.VariantId, variant.ProductId, variant.Sku, variant.ProductName, variant.Name, now)));
        await db.SaveChangesAsync(cancellationToken);
        return true;
    }
}
