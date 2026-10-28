using ClarioShop.Settings.Domain;

namespace ClarioShop.Settings.Tests.Domain;

public sealed class DeliveryZoneTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 28, 9, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Create_TrimsNameAndDeduplicatesAreasIgnoringCase()
    {
        var zone = DeliveryZone.Create(new DeliveryZoneSpec(" Inside Dhaka ", [" Dhanmondi", "Mirpur", "dhanmondi ", " ", "Gulshan"], 60m, 2000m, true, 1), Now);

        Assert.Equal(("Inside Dhaka", "INSIDE DHAKA"), (zone.Name, zone.NormalizedName));
        Assert.Equal(["Dhanmondi", "Mirpur", "Gulshan"], zone.Areas);
        Assert.Equal((60m, 2000m, true, 1), (zone.Fee, zone.FreeDeliveryThreshold, zone.IsActive, zone.SortOrder));
        Assert.Equal((Now, Now), (zone.CreatedAt, zone.UpdatedAt));
    }

    [Fact]
    public void Apply_ReplacesEverythingButIdAndCreatedAt()
    {
        var zone = DeliveryZone.Create(new DeliveryZoneSpec("Inside Dhaka", ["Mirpur"], 60m, 2000m, true, 1), Now);
        var id = zone.Id;
        var later = Now.AddDays(1);

        zone.Apply(new DeliveryZoneSpec("Outside Dhaka", ["Gazipur"], 120m, null, false, 2), later);

        Assert.Equal((id, Now, later), (zone.Id, zone.CreatedAt, zone.UpdatedAt));
        Assert.Equal(("Outside Dhaka", 120m, (decimal?)null, false), (zone.Name, zone.Fee, zone.FreeDeliveryThreshold, zone.IsActive));
        Assert.Equal(["Gazipur"], zone.Areas);
    }
}
