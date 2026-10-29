namespace ClarioShop.Inventory;

public sealed class InventoryOptions
{
    public const string SectionName = "Inventory";

    /// <summary>How often the background sweep looks for expired reservations.</summary>
    public TimeSpan ReservationSweepInterval { get; set; } = TimeSpan.FromSeconds(30);

    /// <summary>The reorder level of items that have none of their own: available stock at or below it is low.</summary>
    public decimal DefaultReorderLevel { get; set; } = 5;
}
