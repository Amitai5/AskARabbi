namespace AskARabbiLIB.Calendar;

/// <summary>A supported current location resolved by the calendar provider, never a birth location.</summary>
public sealed record CalendarLocation
{
    public required string Kind { get; init; }
    public required string Id { get; init; }
    public required string Label { get; init; }
    public required string TimeZone { get; init; }
    public int DefaultCandleLightingMinutes { get; init; } = 18;
}
