namespace ClarioShop.Inventory.Domain;

internal sealed class PurchaseReceiptLine
{
    private PurchaseReceiptLine()
    {
    }

    public Guid Id { get; private set; }

    public Guid ReceiptId { get; private set; }

    public Guid VariantId { get; private set; }

    public decimal Quantity { get; private set; }

    public decimal UnitCost { get; private set; }

    internal static PurchaseReceiptLine Create(Guid receiptId, Guid variantId, decimal quantity, decimal unitCost) => new()
    {
        Id = Guid.CreateVersion7(),
        ReceiptId = receiptId,
        VariantId = variantId,
        Quantity = quantity,
        UnitCost = decimal.Round(unitCost, 2),
    };
}
