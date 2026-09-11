namespace AskARabbiLIB.Calendar;

/// <summary>A ZIP-code or city location whose metadata is resolved by the server.</summary>
public sealed record CalendarLocation
{
    public required string Kind { get; init; }
    public required string Id { get; init; }
    public required string Label { get; init; }
    public required string TimeZone { get; init; }
    public int DefaultCandleLightingMinutes { get; init; } = 18;

    /// <summary>Gets the latitude used for private, server-side birth-sunset calculations.</summary>
    public double? Latitude { get; init; }

    /// <summary>Gets the longitude used for private, server-side birth-sunset calculations.</summary>
    public double? Longitude { get; init; }

    /// <summary>Determines the regional holiday and weekly reading schedule.</summary>
    /// <returns>Whether this location follows the Israel schedule.</returns>
    public bool UsesIsraelSchedule() => TimeZone == "Asia/Jerusalem";
}
