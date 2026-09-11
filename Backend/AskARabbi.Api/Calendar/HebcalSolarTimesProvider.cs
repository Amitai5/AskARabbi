using AskARabbiLIB.Calendar;

namespace AskARabbi.Api.Calendar;

/// <summary>Shares the calendar page's bounded, cached current solar data with chat tools.</summary>
public sealed class HebcalSolarTimesProvider(IHebcalCalendarClient provider) : ICalendarSolarTimesProvider
{
    /// <inheritdoc/>
    public async Task<CalendarSolarTimes?> GetAsync(CalendarLocation location, DateOnly date, CancellationToken cancellationToken)
    {
        var result = await provider.GetSolarTimesAsync(location, date.AddDays(-1), date.AddDays(1), cancellationToken).ConfigureAwait(false);
        var day = result.Data?.SolarDays.GetValueOrDefault(date);
        return day is null ? null : new(day.Sunset, day.Nightfall, result.IsStale);
    }
}
