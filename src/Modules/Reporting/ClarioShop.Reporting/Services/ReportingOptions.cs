using ClarioShop.Reporting.Domain;

namespace ClarioShop.Reporting.Services;

/// <summary>Reports, section "Reporting".</summary>
internal sealed class ReportingOptions
{
    public const string SectionName = "Reporting";

    private const string Dhaka = "Asia/Dhaka";

    /// <summary>The IANA time zone whose calendar days the reports count in; every store today is in Bangladesh.</summary>
    public string TimeZone { get; set; } = Dhaka;

    public StoreCalendar Calendar()
    {
        try
        {
            return new StoreCalendar(TimeZoneInfo.FindSystemTimeZoneById(TimeZone));
        }
        catch (TimeZoneNotFoundException) when (TimeZone == Dhaka)
        {
            // A slim container image without tzdata; Bangladesh has kept UTC+6 all year since 2009.
            return new StoreCalendar(TimeZoneInfo.CreateCustomTimeZone(Dhaka, TimeSpan.FromHours(6), Dhaka, Dhaka));
        }
    }
}
