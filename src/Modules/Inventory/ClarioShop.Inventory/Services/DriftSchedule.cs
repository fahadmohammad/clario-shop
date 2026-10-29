namespace ClarioShop.Inventory.Services;

internal static class DriftSchedule
{
    /// <summary>The first time after <paramref name="now"/> that falls on <paramref name="timeOfDayUtc"/>.</summary>
    public static DateTimeOffset NextRunAfter(DateTimeOffset now, TimeOnly timeOfDayUtc)
    {
        var utc = now.ToUniversalTime();
        var today = new DateTimeOffset(DateOnly.FromDateTime(utc.UtcDateTime).ToDateTime(timeOfDayUtc), TimeSpan.Zero);
        return today > utc ? today : today.AddDays(1);
    }
}
