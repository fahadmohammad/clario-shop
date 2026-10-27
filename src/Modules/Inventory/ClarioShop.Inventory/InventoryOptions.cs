namespace ClarioShop.Inventory;

public sealed class InventoryOptions
{
    public const string SectionName = "Inventory";

    /// <summary>How often the background sweep looks for expired reservations.</summary>
    public TimeSpan ReservationSweepInterval { get; set; } = TimeSpan.FromSeconds(30);
}
