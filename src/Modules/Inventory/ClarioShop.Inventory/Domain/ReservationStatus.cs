namespace ClarioShop.Inventory.Domain;

internal enum ReservationStatus
{
    Active,
    Committed,
    Released,
    Expired,

    /// <summary>Committed, then the order was cancelled before shipping and the sale was reversed.</summary>
    Reversed,
}
