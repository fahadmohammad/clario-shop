namespace ClarioShop.Inventory.Domain;

/// <summary>Why stock moved. The sign of a movement's quantity is fixed by its type (see <see cref="StockMovement"/>).</summary>
internal enum MovementType
{
    PurchaseReceipt,
    OnlineSale,

    /// <summary>Puts back an online sale whose order was cancelled before it shipped (the goods never left).</summary>
    OnlineSaleReversal,
    PosSale,
    CustomerReturn,
    Adjustment,
    TransferOut,
    TransferIn,
}
