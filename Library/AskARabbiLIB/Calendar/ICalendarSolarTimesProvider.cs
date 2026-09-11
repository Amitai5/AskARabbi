namespace AskARabbiLIB.Calendar;

/// <summary>Provides the same location-specific solar boundaries used by the calendar page.</summary>
public interface ICalendarSolarTimesProvider
{
    /// <summary>Gets available solar boundaries for one local civil date.</summary>
    /// <param name="location">Server-resolved current location.</param>
    /// <param name="date">Local civil date.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Solar times, or null when unavailable.</returns>
    Task<CalendarSolarTimes?> GetAsync(CalendarLocation location, DateOnly date, CancellationToken cancellationToken);
}
