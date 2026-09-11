using AskARabbi.Api.Calendar;
using AskARabbiLIB.Calendar;

namespace AskARabbi.Api.Tests;

internal sealed class FakeCalendarProvider : IHebcalCalendarClient
{
    internal List<(int Year, bool InIsrael)> HolidayRequests { get; } = [];
    internal List<CalendarPreferences> TimingRequests { get; } = [];
    internal List<(CalendarLocation Location, DateOnly Start, DateOnly End)> SolarRequests { get; } = [];
    internal Func<CalendarLocation, DateOnly, DateOnly, CalendarProviderResult>? SolarLookup { get; set; }
    internal CalendarProviderResult Holidays { get; set; } = Result(new([], new Dictionary<DateOnly, HebcalData.SunTimes>(), null));
    internal CalendarProviderResult Solar { get; set; } = Result(null);
    internal CalendarProviderResult Local { get; set; } = Result(null);

    public Task<CalendarProviderResult> GetHolidaysAsync(int year, bool inIsrael, CancellationToken cancellationToken)
    {
        HolidayRequests.Add((year, inIsrael));
        return Task.FromResult(Holidays);
    }
    public Task<CalendarProviderResult> GetSolarTimesAsync(CalendarLocation location, DateOnly start, DateOnly end, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        SolarRequests.Add((location, start, end));
        return Task.FromResult(SolarLookup?.Invoke(location, start, end) ?? Solar);
    }
    public Task<CalendarProviderResult> GetLocalEventsAsync(CalendarPreferences preferences, DateOnly start, DateOnly end, CancellationToken cancellationToken)
    {
        TimingRequests.Add(preferences);
        return Task.FromResult(Local);
    }

    internal static CalendarProviderResult Result(HebcalData? data) => new(data, new DateTimeOffset(2026, 9, 10, 0, 0, 0, TimeSpan.Zero), new DateTimeOffset(2026, 9, 11, 0, 0, 0, TimeSpan.Zero), false, data is null ? "unavailable" : null);
    internal static HebcalData.Event Holiday(string title, string date, string subcategory = "major", string category = "holiday") => new(title, DateOnly.Parse(date), null, category, subcategory, "https://www.hebcal.com/holidays/", null);
}
