namespace ClarioShop.Inventory;

public sealed class InventoryOptions
{
    public const string SectionName = "Inventory";

    /// <summary>How often the background sweep looks for expired reservations.</summary>
    public TimeSpan ReservationSweepInterval { get; set; } = TimeSpan.FromSeconds(30);

    /// <summary>The reorder level of items that have none of their own: available stock at or below it is low.</summary>
    public decimal DefaultReorderLevel { get; set; } = 5;

    /// <summary>Whether the nightly drift check runs; staff can still run it on demand.</summary>
    public bool DriftCheckEnabled { get; set; } = true;

    /// <summary>When the nightly drift check runs, in UTC. 21:00 UTC is 3 a.m. in Dhaka, when shops are closed.</summary>
    public TimeOnly DriftCheckAtUtc { get; set; } = new(21, 0);
}
