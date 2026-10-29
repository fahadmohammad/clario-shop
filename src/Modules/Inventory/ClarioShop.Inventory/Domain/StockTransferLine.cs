namespace ClarioShop.Inventory.Domain;

internal sealed class StockTransferLine
{
    private StockTransferLine()
    {
    }

    public Guid Id { get; private set; }

    public Guid TransferId { get; private set; }

    public Guid VariantId { get; private set; }

    /// <summary>How much moved; always above zero.</summary>
    public decimal Quantity { get; private set; }

    internal static StockTransferLine Create(Guid transferId, Guid variantId, decimal quantity) => new()
    {
        Id = Guid.CreateVersion7(),
        TransferId = transferId,
        VariantId = variantId,
        Quantity = quantity,
    };
}
