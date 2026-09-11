using Zmanim;
using Zmanim.TimeZone;
using Zmanim.Utilities;

namespace AskARabbiLIB.Calendar;

/// <summary>Calculates birth-sunset context locally, without disclosing a private birth date to a provider.</summary>
public static class BirthSunsetCalculator
{
    /// <summary>Determines whether a local birth time is at or after sea-level sunset.</summary>
    /// <param name="birthDateTime">Birth date and time in the birthplace's local clock.</param>
    /// <param name="location">Server-resolved birthplace and coordinates.</param>
    /// <returns>The sunset relation, or null when location or solar information is unavailable.</returns>
    public static bool? IsAfterSunset(DateTime birthDateTime, CalendarLocation? location)
    {
        if (location?.Latitude is not double latitude || location.Longitude is not double longitude || !double.IsFinite(latitude) || !double.IsFinite(longitude) || Math.Abs(latitude) > 90 || Math.Abs(longitude) > 180 || !TimeZoneInfo.TryFindSystemTimeZoneById(location.TimeZone, out var zone))
        {
            return null;
        }
        var localBirth = DateTime.SpecifyKind(birthDateTime, DateTimeKind.Unspecified);
        if (zone.IsInvalidTime(localBirth) || zone.IsAmbiguousTime(localBirth))
        {
            return null;
        }
        var coordinates = new GeoLocation(location.Label, latitude, longitude, new WindowsTimeZone(zone));
        var sunset = new AstronomicalCalendar(localBirth.Date, coordinates).GetSeaLevelSunset();
        return sunset is null ? null : localBirth >= sunset.Value;
    }
}
