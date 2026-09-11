using System.Text.RegularExpressions;
using AskARabbiLIB.Calendar;
using AskARabbiLIB.ConversationSettings;

namespace AskARabbi.Api.Calendar;

/// <summary>Validates current-location calendar choices without altering other account settings.</summary>
public sealed class CalendarPreferencesService(ICalendarPreferencesStore store, IHebcalCalendarClient provider, TimeProvider clock, ConversationSettingsService? personalization = null)
{
    /// <summary>Gets saved preferences or explicit defaults for an older account.</summary>
    /// <param name="userId">Authenticated owner.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Calendar preferences.</returns>
    public async Task<CalendarPreferences> GetAsync(Guid userId, CancellationToken cancellationToken)
    {
        var saved = await store.GetCalendarPreferencesAsync(userId, cancellationToken).ConfigureAwait(false) ?? new();
        var profile = personalization is null ? null : await personalization.GetPersonalizationAsync(userId, cancellationToken).ConfigureAwait(false);
        var location = profile?.CurrentLocation ?? saved.Location;
        return saved with { Location = location, InIsrael = location?.UsesIsraelSchedule() ?? saved.InIsrael, ShowLocalTimes = true, CandleLightingMinutes = null, Havdalah = "nightfall", HavdalahMinutes = 42 };
    }

    /// <summary>Resolves and atomically saves a validated calendar object.</summary>
    /// <param name="userId">Authenticated owner.</param>
    /// <param name="request">Desired settings; location labels/timezones are never trusted.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Saved preferences, or null when ZIP resolution is temporarily unavailable.</returns>
    public async Task<CalendarPreferences?> UpdateAsync(Guid userId, CalendarPreferences request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (request.CandleLightingMinutes is < 1 or > 90 || request.Havdalah is not ("nightfall" or "fixed") || request.HavdalahMinutes is not (42 or 50 or 72))
        {
            throw new ArgumentException("Choose a candle-lighting offset from 1–90 minutes and nightfall or 42, 50, or 72 minutes for Havdalah.", nameof(request));
        }

        CalendarLocation? location = null;
        if (request.Location is { } selected)
        {
            if (selected.Kind == "city")
            {
                location = CalendarLocationCatalog.Cities.FirstOrDefault(city => city.Id == selected.Id)
                    ?? throw new ArgumentException("Choose a supported city or enter a U.S. ZIP code.", nameof(request));
            }
            else if (selected.Kind == "zip" && Regex.IsMatch(selected.Id ?? string.Empty, "^[0-9]{5}$", RegexOptions.CultureInvariant))
            {
                var current = await GetAsync(userId, cancellationToken).ConfigureAwait(false);
                if (current.Location is { Kind: "zip" } previous && previous.Id == selected.Id)
                {
                    location = previous;
                }
                else
                {
                    var today = DateOnly.FromDateTime(clock.GetUtcNow().UtcDateTime);
                    var result = await provider.GetSolarTimesAsync(selected with { TimeZone = "", Label = "" }, today, today.AddDays(1), cancellationToken).ConfigureAwait(false);
                    if (result.Problem == "invalid_location")
                    {
                        throw new ArgumentException("That ZIP code is not supported. Check the five digits or choose a city.", nameof(request));
                    }
                    location = result.Data?.Location;
                    if (location is null)
                    {
                        return null;
                    }
                }
            }
            else
            {
                throw new ArgumentException("Choose a supported city or a five-digit U.S. ZIP code.", nameof(request));
            }
        }

        var saved = request with { Location = location };
        await store.UpsertCalendarPreferencesAsync(userId, saved, clock.GetUtcNow(), cancellationToken).ConfigureAwait(false);
        return saved;
    }
}
