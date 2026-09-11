using AskARabbiLIB.Calendar;

namespace AskARabbi.Api.Calendar;

/// <summary>External calendar boundary; no identity, birth details, chat text, or AI requests cross it.</summary>
public interface IHebcalCalendarClient
{
    /// <summary>Gets common holiday dates for a Gregorian year and reading cycle.</summary>
    /// <param name="year">Gregorian year.</param>
    /// <param name="inIsrael">Israel rather than Diaspora schedule.</param>
    /// <param name="cancellationToken">Caller cancellation.</param>
    /// <returns>A cached or unavailable schedule.</returns>
    Task<CalendarProviderResult> GetHolidaysAsync(int year, bool inIsrael, CancellationToken cancellationToken);

    /// <summary>Gets solar boundaries and resolved location for a small civil-date range.</summary>
    /// <param name="location">Supported city or U.S. ZIP.</param>
    /// <param name="start">First civil date.</param>
    /// <param name="end">Last civil date.</param>
    /// <param name="cancellationToken">Caller cancellation.</param>
    /// <returns>Applicable astronomical boundaries.</returns>
    Task<CalendarProviderResult> GetSolarTimesAsync(CalendarLocation location, DateOnly start, DateOnly end, CancellationToken cancellationToken);

    /// <summary>Gets provider-calculated festival and Shabbat transitions, not a universal sunset offset.</summary>
    /// <param name="preferences">Resolved location and timing conventions.</param>
    /// <param name="start">First civil date.</param>
    /// <param name="end">Last civil date.</param>
    /// <param name="cancellationToken">Caller cancellation.</param>
    /// <returns>Applicable candle-lighting, Havdalah and fast events.</returns>
    Task<CalendarProviderResult> GetLocalEventsAsync(CalendarPreferences preferences, DateOnly start, DateOnly end, CancellationToken cancellationToken);
}
