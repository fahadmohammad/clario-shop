namespace ClarioShop.Inventory.Domain;

/// <summary>Why stock moved. The sign of a movement's quantity is fixed by its type (see <see cref="StockMovement"/>).</summary>
internal enum MovementType
{
    PurchaseReceipt,
    OnlineSale,
    PosSale,
    CustomerReturn,
    Adjustment,
    TransferOut,
    TransferIn,
}
