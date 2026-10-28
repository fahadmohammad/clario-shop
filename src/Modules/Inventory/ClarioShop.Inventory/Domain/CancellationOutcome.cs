namespace ClarioShop.Inventory.Domain;

/// <summary>What cancelling an order means for the stock its reservation held.</summary>
internal enum CancellationOutcome
{
    /// <summary>Nothing left to give back (already released, expired or reversed).</summary>
    Nothing,

    /// <summary>The stock is still held: drop the hold.</summary>
    ReturnHold,

    /// <summary>The stock was sold: put it back with reversal movements.</summary>
    ReverseSale,
}
