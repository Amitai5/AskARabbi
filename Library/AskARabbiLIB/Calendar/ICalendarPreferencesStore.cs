namespace AskARabbiLIB.Calendar;

/// <summary>Reads and atomically replaces only the calendar object in an owner's account settings.</summary>
public interface ICalendarPreferencesStore
{
    /// <summary>Gets an owner's preferences, or null for an existing account without calendar settings.</summary>
    /// <param name="userId">Authenticated account identifier.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The saved calendar preferences.</returns>
    Task<CalendarPreferences?> GetCalendarPreferencesAsync(Guid userId, CancellationToken cancellationToken = default);

    /// <summary>Replaces only an owner's calendar preferences.</summary>
    /// <param name="userId">Authenticated account identifier.</param>
    /// <param name="preferences">Validated preferences.</param>
    /// <param name="updatedAtUtc">UTC update time.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Completion of the update.</returns>
    Task UpsertCalendarPreferencesAsync(Guid userId, CalendarPreferences preferences, DateTimeOffset updatedAtUtc, CancellationToken cancellationToken = default);
}
