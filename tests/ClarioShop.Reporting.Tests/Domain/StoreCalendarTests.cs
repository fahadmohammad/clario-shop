using ClarioShop.Reporting.Domain;
using ClarioShop.Reporting.Services;

namespace ClarioShop.Reporting.Tests.Domain;

public sealed class StoreCalendarTests
{
    private static readonly StoreCalendar Dhaka = new ReportingOptions().Calendar();

    [Theory]
    [InlineData("2026-10-28T17:59:59Z", "2026-10-28")]
    [InlineData("2026-10-28T18:00:00Z", "2026-10-29")]
    [InlineData("2026-10-28T23:30:00+06:00", "2026-10-28")]
    public void DayOf_Dhaka_CountsInBangladeshTime(string instant, string day)
    {
        var result = Dhaka.DayOf(DateTimeOffset.Parse(instant, System.Globalization.CultureInfo.InvariantCulture));

        Assert.Equal(DateOnly.Parse(day, System.Globalization.CultureInfo.InvariantCulture), result);
    }

    [Fact]
    public void Calendar_OtherZone_UsesIt()
    {
        var calendar = new ReportingOptions { TimeZone = "UTC" }.Calendar();

        var result = calendar.DayOf(new DateTimeOffset(2026, 10, 28, 18, 0, 0, TimeSpan.Zero));

        Assert.Equal(new DateOnly(2026, 10, 28), result);
    }
}
