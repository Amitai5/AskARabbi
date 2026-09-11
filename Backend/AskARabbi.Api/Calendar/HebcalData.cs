using AskARabbiLIB.Calendar;

namespace AskARabbi.Api.Calendar;

/// <summary>Validated provider data with date-only observances and offset-aware astronomical events.</summary>
public sealed record HebcalData(IReadOnlyList<HebcalData.Event> Events, IReadOnlyDictionary<DateOnly, HebcalData.SunTimes> SolarDays, CalendarLocation? Location)
{
    public sealed record Event(string Title, DateOnly Date, DateTimeOffset? Instant, string Category, string? Subcategory, string? Link, string? Memo);
    public sealed record SunTimes(DateTimeOffset? Sunset, DateTimeOffset? Nightfall, DateTimeOffset? Dawn);
}
