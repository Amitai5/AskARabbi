using System.Globalization;
using System.Net;
using System.Text.Json;
using AskARabbiLIB.Calendar;

namespace AskARabbi.Api.Calendar;

/// <summary>Bounded, single-flight Hebcal integration. Only public calendar parameters leave the application.</summary>
public sealed class HebcalCalendarClient(IHttpClientFactory clients, TimeProvider clock, ILogger<HebcalCalendarClient> logger) : IHebcalCalendarClient
{
    private const int CacheCapacity = 128;
    private readonly object synchronization = new();
    private readonly Dictionary<string, CacheEntry> cache = [];
    private readonly Dictionary<string, Lazy<Task<CalendarProviderResult>>> pending = [];
    private DateTimeOffset throttledUntil;
    private DateTimeOffset windowStart;
    private int windowRequests;

    /// <inheritdoc/>
    public Task<CalendarProviderResult> GetHolidaysAsync(int year, bool inIsrael, CancellationToken cancellationToken)
    {
        if (year is < 1583 or > 2239)
        {
            throw new ArgumentOutOfRangeException(nameof(year));
        }
        return GetAsync($"hebcal?v=1&cfg=json&year={year}&yt=G&maj=on&min=on&mf=on&nx=on&ss=on&mod=on&leyning=off&i={(inIsrael ? "on" : "off")}", null, null, null, cancellationToken);
    }

    /// <inheritdoc/>
    public Task<CalendarProviderResult> GetSolarTimesAsync(CalendarLocation location, DateOnly start, DateOnly end, CancellationToken cancellationToken)
    {
        ValidateRange(start, end);
        return GetAsync($"zmanim?cfg=json&{LocationQuery(location)}&start={start:yyyy-MM-dd}&end={end:yyyy-MM-dd}", location, start, end, cancellationToken);
    }

    /// <inheritdoc/>
    public Task<CalendarProviderResult> GetLocalEventsAsync(CalendarPreferences preferences, DateOnly start, DateOnly end, CancellationToken cancellationToken)
    {
        ValidateRange(start, end);
        var location = preferences.Location ?? throw new ArgumentException("Local times require a supported location.", nameof(preferences));
        var havdalah = preferences.Havdalah == "fixed" ? $"m={preferences.HavdalahMinutes}" : "M=on";
        var offset = preferences.CandleLightingMinutes ?? location.DefaultCandleLightingMinutes;
        return GetAsync($"hebcal?v=1&cfg=json&start={start:yyyy-MM-dd}&end={end:yyyy-MM-dd}&maj=on&mf=on&c=on&leyning=off&i={(preferences.InIsrael ? "on" : "off")}&{LocationQuery(location)}&b={offset}&{havdalah}", location, start, end, cancellationToken);
    }

    private async Task<CalendarProviderResult> GetAsync(string query, CalendarLocation? location, DateOnly? start, DateOnly? end, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        // Include the resolved timezone so stale results cannot cross location or convention changes.
        var key = query + "|" + location?.TimeZone;
        Lazy<Task<CalendarProviderResult>> request;
        lock (synchronization)
        {
            if (cache.TryGetValue(key, out var cached) && cached.RefreshAt > clock.GetUtcNow())
            {
                return ToResult(cached);
            }
            if (pending.TryGetValue(key, out var existing))
            {
                request = existing;
            }
            else
            {
                if (pending.Count >= 32)
                {
                    return Fallback(key, "busy", clock.GetUtcNow().AddMinutes(1));
                }
                request = new Lazy<Task<CalendarProviderResult>>(() => FetchAsync(key, query, location, start, end), LazyThreadSafetyMode.ExecutionAndPublication);
                pending.Add(key, request);
            }
        }
        // One user's cancellation must not cancel work shared with another request.
        return await request.Value.WaitAsync(cancellationToken).ConfigureAwait(false);
    }

    private async Task<CalendarProviderResult> FetchAsync(string key, string query, CalendarLocation? location, DateOnly? start, DateOnly? end)
    {
        try
        {
            for (var attempt = 0; attempt < 2; attempt++)
            {
                if (!TryReserveRequest())
                {
                    lock (synchronization)
                    {
                        return SaveFailure(key, "rate_limited", throttledUntil > clock.GetUtcNow() ? throttledUntil : clock.GetUtcNow().AddSeconds(10));
                    }
                }

                using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(6));
                using var response = await clients.CreateClient("HebcalCalendar").GetAsync(query, HttpCompletionOption.ResponseHeadersRead, timeout.Token).ConfigureAwait(false);
                if (response.StatusCode == HttpStatusCode.TooManyRequests || response.Headers.RetryAfter is not null)
                {
                    var retryAt = response.Headers.RetryAfter?.Date ?? clock.GetUtcNow().Add(response.Headers.RetryAfter?.Delta ?? TimeSpan.FromMinutes(1));
                    lock (synchronization)
                    {
                        throttledUntil = retryAt > clock.GetUtcNow() ? retryAt : clock.GetUtcNow().AddMinutes(1);
                        return SaveFailure(key, "rate_limited", throttledUntil);
                    }
                }
                if ((int)response.StatusCode >= 500 && attempt == 0)
                {
                    continue;
                }
                if (!response.IsSuccessStatusCode)
                {
                    lock (synchronization)
                    {
                        return SaveFailure(key, response.StatusCode == HttpStatusCode.BadRequest ? "invalid_location" : "unavailable", clock.GetUtcNow().AddMinutes(1));
                    }
                }

                await response.Content.LoadIntoBufferAsync(2_000_000, timeout.Token).ConfigureAwait(false);
                using var document = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync(timeout.Token).ConfigureAwait(false), cancellationToken: timeout.Token).ConfigureAwait(false);
                var data = Parse(document.RootElement, query.StartsWith("zmanim", StringComparison.Ordinal), location, start, end);
                var now = clock.GetUtcNow();
                var control = response.Headers.CacheControl;
                var freshness = control?.MaxAge is { } maxAge ? maxAge - (response.Headers.Age ?? TimeSpan.Zero) : TimeSpan.FromHours(6);
                var freshUntil = control?.MaxAge is not null ? now.Add(freshness) : response.Content.Headers.Expires ?? now.Add(freshness);
                if (control?.NoCache == true || control?.NoStore == true)
                {
                    freshUntil = now;
                }
                freshUntil = freshUntil < now ? now : freshUntil > now.AddDays(1) ? now.AddDays(1) : freshUntil;
                var entry = new CacheEntry(data, now, freshUntil, null, control?.MustRevalidate != true && control?.NoCache != true);
                lock (synchronization)
                {
                    if (control?.NoStore != true)
                    {
                        Save(key, entry);
                    }
                    else
                    {
                        cache.Remove(key);
                    }
                }
                return ToResult(entry);
            }
            throw new InvalidOperationException("The bounded provider attempt loop did not return a result.");
        }
        catch (Exception exception) when (exception is HttpRequestException or OperationCanceledException or JsonException or InvalidDataException or FormatException)
        {
            logger.LogWarning("Calendar provider request failed with {FailureType}; locally calculated dates and readings remain available.", exception.GetType().Name);
            lock (synchronization)
            {
                return SaveFailure(key, "unavailable", clock.GetUtcNow().AddMinutes(1));
            }
        }
        finally
        {
            lock (synchronization)
            {
                pending.Remove(key);
            }
        }
    }

    private bool TryReserveRequest()
    {
        lock (synchronization)
        {
            var now = clock.GetUtcNow();
            if (now < throttledUntil)
            {
                return false;
            }
            if (now >= windowStart.AddSeconds(10))
            {
                windowStart = now;
                windowRequests = 0;
            }
            // Below Hebcal's documented 90/10s limit; retries also consume the budget.
            return ++windowRequests <= 60;
        }
    }

    private CalendarProviderResult SaveFailure(string key, string problem, DateTimeOffset refreshAt)
    {
        var fallback = Fallback(key, problem, refreshAt);
        Save(key, new(fallback.Data, fallback.FetchedAtUtc, refreshAt, problem, true));
        return fallback;
    }

    private CalendarProviderResult Fallback(string key, string problem, DateTimeOffset refreshAt)
    {
        cache.TryGetValue(key, out var previous);
        var usable = previous?.AllowStale == true && previous.FetchedAt > clock.GetUtcNow().AddDays(-7);
        return new(usable ? previous?.Data : null, usable ? previous?.FetchedAt : null, refreshAt, usable && previous?.Data is not null, problem);
    }

    private void Save(string key, CacheEntry entry)
    {
        if (!cache.ContainsKey(key) && cache.Count >= CacheCapacity)
        {
            cache.Remove(cache.MinBy(item => item.Value.FetchedAt ?? DateTimeOffset.MinValue).Key);
        }
        cache[key] = entry;
    }

    private static CalendarProviderResult ToResult(CacheEntry entry) => new(entry.Data, entry.FetchedAt, entry.RefreshAt, entry.Problem is not null && entry.Data is not null, entry.Problem);

    private static string LocationQuery(CalendarLocation location)
    {
        ArgumentNullException.ThrowIfNull(location);
        if (location.Kind is not ("city" or "zip") || string.IsNullOrEmpty(location.Id) || location.Id.Length > 10 || !location.Id.All(char.IsAsciiDigit))
        {
            throw new ArgumentException("Unsupported calendar location.", nameof(location));
        }
        return $"geo={(location.Kind == "city" ? "geoname" : "zip")}&{(location.Kind == "city" ? "geonameid" : "zip")}={location.Id}";
    }

    private static void ValidateRange(DateOnly start, DateOnly end)
    {
        if (end < start || end.DayNumber - start.DayNumber > 16)
        {
            throw new ArgumentOutOfRangeException(nameof(end), "Local calculation ranges cannot exceed 16 days.");
        }
    }

    private static HebcalData Parse(JsonElement root, bool solar, CalendarLocation? selected, DateOnly? start, DateOnly? end)
    {
        if (root.ValueKind != JsonValueKind.Object || root.TryGetProperty("error", out _))
        {
            throw new InvalidDataException("Invalid calendar response.");
        }
        CalendarLocation? resolved = null;
        if (selected is not null)
        {
            if (!root.TryGetProperty("location", out var place) || place.ValueKind != JsonValueKind.Object)
            {
                throw new InvalidDataException("Missing resolved location.");
            }
            var timeZone = Text(place, "tzid");
            var locationId = selected.Kind == "zip" ? Text(place, "zip") : place.TryGetProperty("geonameid", out var id) ? id.ToString() : null;
            if (locationId != selected.Id || timeZone is null || !timeZone.Contains('/') || !TimeZoneInfo.TryFindSystemTimeZoneById(timeZone, out _) || (!string.IsNullOrEmpty(selected.TimeZone) && timeZone != selected.TimeZone))
            {
                throw new InvalidDataException("Location or timezone did not match the request.");
            }
            resolved = selected with { Label = Text(place, "title") ?? selected.Label, TimeZone = timeZone, DefaultCandleLightingMinutes = selected.DefaultCandleLightingMinutes };
        }
        if (solar)
        {
            if (!root.TryGetProperty("times", out var times) || times.ValueKind != JsonValueKind.Object || !times.TryGetProperty("sunset", out var sunsets) || sunsets.ValueKind != JsonValueKind.Object || start is null || end is null)
            {
                throw new InvalidDataException("Missing solar dates.");
            }
            Dictionary<DateOnly, HebcalData.SunTimes> days = [];
            for (var day = start.Value; day <= end.Value; day = day.AddDays(1))
            {
                days.Add(day, new(SolarInstant(times, "sunset", day), SolarInstant(times, "tzeit85deg", day), SolarInstant(times, "alotHaShachar", day)));
            }
            return new([], days, resolved);
        }
        if (!root.TryGetProperty("items", out var items) || items.ValueKind != JsonValueKind.Array || items.GetArrayLength() > 2000)
        {
            throw new InvalidDataException("Missing calendar occurrences.");
        }
        List<HebcalData.Event> events = [];
        foreach (var item in items.EnumerateArray())
        {
            var title = Text(item, "title") ?? throw new InvalidDataException("Missing event title.");
            var dateText = Text(item, "date") ?? throw new InvalidDataException("Missing event date.");
            var category = Text(item, "category") ?? throw new InvalidDataException("Missing event category.");
            if (dateText.Length < 10 || !DateOnly.TryParseExact(dateText[..10], "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var date))
            {
                throw new InvalidDataException("Invalid observance date.");
            }
            var instant = dateText.Length == 10 ? null : ParseInstant(dateText);
            if (dateText.Length > 10 && instant is null)
            {
                throw new InvalidDataException("A timing event requires an offset.");
            }
            var link = Text(item, "link");
            if (!Uri.TryCreate(link, UriKind.Absolute, out var source) || source.Scheme != "https" || source.Host is not ("hebcal.com" or "www.hebcal.com"))
            {
                link = null;
            }
            events.Add(new(title, date, instant, category, Text(item, "subcat"), link, Text(item, "memo")));
        }
        return new(events, new Dictionary<DateOnly, HebcalData.SunTimes>(), resolved);
    }

    private static DateTimeOffset? SolarInstant(JsonElement times, string name, DateOnly date)
    {
        if (!times.TryGetProperty(name, out var values) || values.ValueKind != JsonValueKind.Object || !values.TryGetProperty(date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture), out var value) || value.ValueKind == JsonValueKind.Null)
        {
            return null;
        }
        if (value.ValueKind != JsonValueKind.String)
        {
            throw new InvalidDataException("Invalid solar instant.");
        }
        var instant = ParseInstant(value.GetString());
        if (instant is null || DateOnly.FromDateTime(instant.Value.DateTime) != date)
        {
            throw new InvalidDataException("Solar time belongs to another date.");
        }
        return instant;
    }

    private static DateTimeOffset? ParseInstant(string? text) => text is { Length: >= 20 } && (text.EndsWith('Z') || text[^6] is '+' or '-') && DateTimeOffset.TryParse(text, CultureInfo.InvariantCulture, DateTimeStyles.None, out var value) ? value : null;
    private static string? Text(JsonElement value, string name) => value.ValueKind == JsonValueKind.Object && value.TryGetProperty(name, out var property) && property.ValueKind == JsonValueKind.String ? property.GetString() : null;
    private sealed record CacheEntry(HebcalData? Data, DateTimeOffset? FetchedAt, DateTimeOffset RefreshAt, string? Problem, bool AllowStale);
}
