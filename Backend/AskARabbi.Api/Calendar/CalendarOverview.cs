using AskARabbiLIB.Calendar;

namespace AskARabbi.Api.Calendar;

/// <summary>Account-specific calendar overview. Civil dates must never be treated as UTC instants.</summary>
public sealed record CalendarOverview(CalendarPreferences Preferences, CalendarOverview.TodayInfo Today, WeeklyParashahInfo Shabbat, IReadOnlyList<CalendarOverview.AgendaEvent> Events, CalendarOverview.AgendaEvent? Highlight, CalendarOverview.Availability Holidays, IReadOnlyList<CalendarOverview.LocalEvent> LocalTimes, CalendarOverview.Availability Timing, string TimingConvention, DateTimeOffset GeneratedAtUtc, DateTimeOffset NextRefreshAtUtc)
{
    public sealed record TodayInfo(DateOnly GregorianDate, string HebrewDate, string HebrewScript, string TimeZone, bool IsAfterSunset, bool IsDaytimeOnly, Availability? SolarData = null);
    public sealed record Availability(bool IsAvailable, bool IsStale, DateTimeOffset? FetchedAtUtc, string? Message);
    public sealed record AgendaEvent(string Id, string Kind, string Title, string Category, DateOnly StartDate, DateOnly EndDate, DateOnly BeginningDate, string BeginningRule, string Explanation, string SourceUrl, bool IsOngoing, IReadOnlyList<Occurrence> Occurrences);
    public sealed record Occurrence(string Title, DateOnly Date);
    public sealed record LocalEvent(string Title, DateOnly Date, DateTimeOffset At, string TimeZone, string Location, string? Context);
}
