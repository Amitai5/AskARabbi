using AskARabbi.Api.Contracts.ConversationSettings;
using AskARabbiLIB.Calendar;

namespace AskARabbi.Api.Calendar;

/// <summary>Resolves shared personalization locations using public location data, never a birth date.</summary>
public sealed class PersonalizationLocationResolver(IHebcalCalendarClient provider, TimeProvider clock)
{
    /// <summary>Resolves timezone and coordinates while reusing unchanged, already resolved locations.</summary>
    /// <param name="request">Untrusted ZIP or city selection.</param>
    /// <param name="previous">Previously resolved location for this account.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A resolved location, or null when the provider is unavailable.</returns>
    public async Task<CalendarLocation?> ResolveAsync(PersonalizationLocationRequest request, CalendarLocation? previous, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var id = request.Id?.Trim() ?? string.Empty;
        var selected = request.Kind switch
        {
            "city" => CalendarLocationCatalog.Cities.FirstOrDefault(city => city.Id == id) ?? throw new ArgumentException("Choose a supported city or enter a U.S. ZIP code.", nameof(request)),
            "zip" when id.Length == 5 && id.All(char.IsAsciiDigit) => new CalendarLocation { Kind = "zip", Id = id, Label = "", TimeZone = "" },
            _ => throw new ArgumentException("Choose a supported city or a five-digit U.S. ZIP code.", nameof(request)),
        };
        if (previous is not null && previous.Kind == selected.Kind && previous.Id == selected.Id && HasCoordinates(previous))
        {
            return previous;
        }

        var today = DateOnly.FromDateTime(clock.GetUtcNow().UtcDateTime);
        var result = await provider.GetSolarTimesAsync(selected, today, today.AddDays(1), cancellationToken).ConfigureAwait(false);
        if (result.Problem == "invalid_location")
        {
            throw new ArgumentException("That location is not supported. Check the ZIP code or choose a supported city.", nameof(request));
        }
        return result.Data?.Location is { } location && HasCoordinates(location) ? location : null;
    }

    private static bool HasCoordinates(CalendarLocation location) => location.Latitude is >= -90 and <= 90 && location.Longitude is >= -180 and <= 180;
}
