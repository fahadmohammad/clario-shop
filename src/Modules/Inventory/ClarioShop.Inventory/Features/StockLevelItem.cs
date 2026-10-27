using ClarioShop.Inventory.Domain;

namespace ClarioShop.Inventory.Features;

/// <summary>Stock of one variant after a change, returned by the write endpoints.</summary>
public sealed record StockLevelItem(Guid VariantId, decimal OnHand, decimal Reserved, decimal Available)
{
    internal static IReadOnlyList<StockLevelItem> From(IEnumerable<StockLevel> levels) =>
        levels.Select(level => new StockLevelItem(level.VariantId, level.OnHand, level.Reserved, level.Available)).ToList();
}
