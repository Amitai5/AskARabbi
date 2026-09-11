namespace AskARabbiLIB.Calendar;

/// <summary>Independent account-backed calendar choices; null candle offset uses the location's documented default.</summary>
public sealed record CalendarPreferences
{
    public CalendarLocation? Location { get; init; }
    public bool InIsrael { get; init; }
    public bool ShowLocalTimes { get; init; } = true;
    public int? CandleLightingMinutes { get; init; }
    public string Havdalah { get; init; } = "nightfall";
    public int HavdalahMinutes { get; init; } = 42;
    public bool MajorHolidays { get; init; } = true;
    public bool MinorHolidays { get; init; } = true;
    public bool FastDays { get; init; } = true;
    public bool RoshChodesh { get; init; } = true;
    public bool SpecialShabbatot { get; init; } = true;
    public bool ModernObservances { get; init; } = true;
}
