using ClarioShop.Reporting.Domain;

namespace ClarioShop.Reporting.Features.Reports;

/// <summary>The store days a report covers, both included: the last 30 days up to today unless the caller picks them.</summary>
internal sealed record ReportPeriod(DateOnly From, DateOnly To)
{
    public const int DefaultDays = 30;

    public const int MaxDays = 366;

    public static Result<ReportPeriod> Resolve(DateOnly? from, DateOnly? to, StoreCalendar calendar, TimeProvider clock)
    {
        var end = to ?? calendar.DayOf(clock.GetUtcNow());
        var start = from ?? end.AddDays(1 - DefaultDays);
        if (start > end)
        {
            return Error.Validation("from", "From must be on or before to.");
        }

        if (end.DayNumber - start.DayNumber + 1 > MaxDays)
        {
            return Error.Validation("to", $"A report covers at most {MaxDays} days.");
        }

        return new ReportPeriod(start, end);
    }
}
