namespace ClarioShop.Inventory.Domain;

/// <summary>The document a movement belongs to.</summary>
internal readonly record struct StockReference(string Type, Guid Id)
{
    public static StockReference PurchaseReceipt(Guid id) => new("PurchaseReceipt", id);

    public static StockReference Adjustment(Guid id) => new("StockAdjustment", id);

    public static StockReference Order(Guid id) => new("Order", id);
}
