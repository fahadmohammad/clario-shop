namespace ClarioShop.Inventory.Domain;

internal enum AdjustmentReason
{
    /// <summary>Broken or spoiled stock written off. Decrease only.</summary>
    Damaged,

    /// <summary>Past its expiry date and removed from sale. Decrease only.</summary>
    Expired,

    /// <summary>Stolen or missing without explanation. Decrease only.</summary>
    Theft,

    /// <summary>A physical count differs from the system. Either direction.</summary>
    CountCorrection,

    /// <summary>Anything else; a note saying why is required. Either direction.</summary>
    Other,
}
