namespace AskARabbiLIB.Calendar;

/// <summary>Contains available sea-level sunset and nightfall boundaries for a civil date.</summary>
/// <param name="Sunset">Sunset, or null when unavailable.</param>
/// <param name="Nightfall">Nightfall at 8.5 degrees, or null when unavailable.</param>
/// <param name="IsStale">Whether the provider is using previously fetched times.</param>
public sealed record CalendarSolarTimes(DateTimeOffset? Sunset, DateTimeOffset? Nightfall, bool IsStale);
