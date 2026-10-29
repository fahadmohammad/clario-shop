using ClarioShop.Inventory.Services;

namespace ClarioShop.Inventory.Tests.Services;

public sealed class DriftScheduleTests
{
    private static readonly TimeOnly NinePm = new(21, 0);

    [Fact]
    public void NextRunAfter_BeforeTheTime_RunsTheSameDay()
    {
        var now = new DateTimeOffset(2026, 10, 29, 8, 30, 0, TimeSpan.Zero);

        Assert.Equal(new DateTimeOffset(2026, 10, 29, 21, 0, 0, TimeSpan.Zero), DriftSchedule.NextRunAfter(now, NinePm));
    }

    [Theory]
    [InlineData(21, 0)]
    [InlineData(23, 59)]
    public void NextRunAfter_AtOrAfterTheTime_RunsTheNextDay(int hour, int minute)
    {
        var now = new DateTimeOffset(2026, 10, 29, hour, minute, 0, TimeSpan.Zero);

        Assert.Equal(new DateTimeOffset(2026, 10, 30, 21, 0, 0, TimeSpan.Zero), DriftSchedule.NextRunAfter(now, NinePm));
    }

    [Fact]
    public void NextRunAfter_NonUtcNow_UsesUtcTimeOfDay()
    {
        // 02:00 in Dhaka on the 30th is 20:00 UTC on the 29th.
        var now = new DateTimeOffset(2026, 10, 30, 2, 0, 0, TimeSpan.FromHours(6));

        Assert.Equal(new DateTimeOffset(2026, 10, 29, 21, 0, 0, TimeSpan.Zero), DriftSchedule.NextRunAfter(now, NinePm));
    }
}
