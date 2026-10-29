using ClarioShop.SharedKernel.Results;

namespace ClarioShop.Inventory.Domain;

/// <summary>
/// Inventory's own copy of a catalog variant (from ProductCreated), so stock can be listed and
/// validated without reading Catalog's tables.
/// </summary>
internal sealed class InventoryItem
{
    private InventoryItem()
    {
    }

    public Guid VariantId { get; private set; }

    public Guid ProductId { get; private set; }

    public string Sku { get; private set; } = string.Empty;

    public string ProductName { get; private set; } = string.Empty;

    public string VariantName { get; private set; } = string.Empty;

    public DateTimeOffset CreatedAt { get; private set; }

    /// <summary>
    /// Available stock at or below this, at any location, is low and due for reordering. Null uses the
    /// store-wide default (Inventory:DefaultReorderLevel).
    /// </summary>
    public decimal? ReorderLevel { get; private set; }

    public static InventoryItem Create(Guid variantId, Guid productId, string sku, string productName, string variantName, DateTimeOffset now) => new()
    {
        VariantId = variantId,
        ProductId = productId,
        Sku = sku,
        ProductName = productName,
        VariantName = variantName,
        CreatedAt = now,
    };

    public Result SetReorderLevel(decimal? level)
    {
        if (level is < 0)
        {
            return Error.Validation("reorderLevel", "The reorder level cannot be negative.");
        }

        ReorderLevel = level;
        return Result.Success();
    }
}
