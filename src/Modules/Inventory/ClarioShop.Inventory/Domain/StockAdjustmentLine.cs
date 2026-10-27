namespace ClarioShop.Inventory.Domain;

internal sealed class StockAdjustmentLine
{
    private StockAdjustmentLine()
    {
    }

    public Guid Id { get; private set; }

    public Guid AdjustmentId { get; private set; }

    public Guid VariantId { get; private set; }

    /// <summary>Signed change to on-hand stock.</summary>
    public decimal QuantityChange { get; private set; }

    internal static StockAdjustmentLine Create(Guid adjustmentId, Guid variantId, decimal quantityChange) => new()
    {
        Id = Guid.CreateVersion7(),
        AdjustmentId = adjustmentId,
        VariantId = variantId,
        QuantityChange = quantityChange,
    };
}
