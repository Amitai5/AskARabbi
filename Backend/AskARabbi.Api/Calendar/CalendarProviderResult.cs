namespace AskARabbi.Api.Calendar;

/// <summary>Distinguishes an empty successful schedule, stale fallback, and unavailable data.</summary>
public sealed record CalendarProviderResult(HebcalData? Data, DateTimeOffset? FetchedAtUtc, DateTimeOffset RefreshAtUtc, bool IsStale, string? Problem);
