namespace ClarioShop.Reporting.Domain;

/// <summary>Turns instants into the store's calendar days, so a sale at 2 a.m. in Dhaka counts on that day, not the UTC one.</summary>
internal sealed class StoreCalendar(TimeZoneInfo zone)
{
    public TimeZoneInfo Zone => zone;

    public DateOnly DayOf(DateTimeOffset instant) => DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(instant, zone).DateTime);
}
