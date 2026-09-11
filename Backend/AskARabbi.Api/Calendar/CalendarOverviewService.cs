using AskARabbiLIB.Calendar;

namespace AskARabbi.Api.Calendar;

/// <summary>Combines deterministic Hebrew calculations and shared provider schedules without using AI or quota services.</summary>
public sealed class CalendarOverviewService(CalendarPreferencesService settings, IHebrewCalendarService calendar, IHebcalCalendarClient provider, TimeProvider clock)
{
    /// <summary>Builds a bounded agenda with local boundary and freshness information.</summary>
    /// <param name="userId">Authenticated owner.</param>
    /// <param name="days">One of 30, 90, 180, 360, or 365 days.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <param name="includeAllCategories">Returns an unfiltered schedule without modifying the owner's preferences.</param>
    /// <returns>A complete or explicitly partial overview.</returns>
    public async Task<CalendarOverview> GetAsync(Guid userId, int days, CancellationToken cancellationToken, bool includeAllCategories = false)
    {
        if (days is not (30 or 90 or 180 or 360 or 365))
        {
            throw new ArgumentOutOfRangeException(nameof(days), "Choose a 90-, 180-, or 360-day agenda.");
        }
        var preferences = await settings.GetAsync(userId, cancellationToken).ConfigureAwait(false);
        var now = clock.GetUtcNow();
        var zone = preferences.Location is { } location ? TimeZoneInfo.FindSystemTimeZoneById(location.TimeZone) : TimeZoneInfo.Utc;
        var localNow = TimeZoneInfo.ConvertTime(now, zone);
        var today = DateOnly.FromDateTime(localNow.DateTime);
        var rangeStart = today.AddDays(-14);
        // Read beyond the visible range so expandable groups never lose their final days.
        var visibleEnd = today.AddDays(days - 1);
        var rangeEnd = visibleEnd.AddDays(14);
        var holidayTasks = Enumerable.Range(rangeStart.Year, rangeEnd.Year - rangeStart.Year + 1).Select(year => provider.GetHolidaysAsync(year, preferences.InIsrael, cancellationToken)).ToArray();
        var solarTask = preferences.Location is null ? Task.FromResult<CalendarProviderResult?>(null) : GetSolarAsync(preferences.Location, today, cancellationToken);
        var localTask = preferences.Location is null || !preferences.ShowLocalTimes ? Task.FromResult<CalendarProviderResult?>(null) : GetLocalAsync(preferences, today, cancellationToken);
        await Task.WhenAll(holidayTasks.Cast<Task>().Append(solarTask).Append(localTask)).ConfigureAwait(false);
        var schedules = await Task.WhenAll(holidayTasks).ConfigureAwait(false);
        var solar = await solarTask.ConfigureAwait(false);
        var local = await localTask.ConfigureAwait(false);
        var solarDays = solar?.Data?.SolarDays;
        var todaysSun = solarDays?.GetValueOrDefault(today);
        var afterSunset = todaysSun?.Sunset is { } sunset && now >= sunset;
        var isDaytimeOnly = todaysSun?.Sunset is null;
        var solarStatus = solar is null ? null : new CalendarOverview.Availability(!isDaytimeOnly, solar.IsStale, solar.FetchedAtUtc, solar.IsStale ? "Using saved solar times for this location and date. The last successful update is shown below." : null);
        var hebrew = calendar.ConvertToHebrew(today.ToDateTime(TimeOnly.MinValue), afterSunset);
        var shabbatEnd = preferences.Havdalah == "fixed" ? todaysSun?.Sunset?.AddMinutes(preferences.HavdalahMinutes) : todaysSun?.Nightfall;
        var readingDate = today.DayOfWeek == DayOfWeek.Saturday && shabbatEnd is { } nightfall && now >= nightfall ? today.AddDays(1) : today;
        var shabbat = calendar.FindParashahForWeek(readingDate.ToDateTime(TimeOnly.MinValue), preferences.InIsrael);
        var occurrences = schedules.Where(result => result.Data is not null).SelectMany(result => result.Data!.Events)
            .Where(item => item.Instant is null && item.Category is "holiday" or "roshchodesh" && item.Date >= rangeStart && item.Date <= rangeEnd && !item.Title.StartsWith("Erev ", StringComparison.Ordinal))
            .DistinctBy(item => (item.Title, item.Date)).OrderBy(item => item.Date).ToArray();
        var events = GroupEvents(occurrences, preferences, today, visibleEnd, now, solarDays, includeAllCategories);
        var highlight = events.FirstOrDefault(item => item.IsOngoing) ?? events.FirstOrDefault();
        var hasAllHolidays = schedules.All(result => result.Data is not null);
        var holidayStatus = new CalendarOverview.Availability(hasAllHolidays, schedules.Any(result => result.IsStale), OldestFetch(schedules), hasAllHolidays ? schedules.Any(result => result.IsStale) ? "Showing a saved schedule while Hebcal is unavailable." : null : "Some holiday dates are temporarily unavailable. Dates and weekly readings still work.");
        var localTimes = local?.Data?.Events.Where(item => item.Instant >= now && item.Category is "candles" or "havdalah" or "zmanim")
            .OrderBy(item => item.Instant).Select(item => new CalendarOverview.LocalEvent(TimingTitle(item), DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(item.Instant!.Value, zone).DateTime), item.Instant.Value, zone.Id, preferences.Location!.Label, item.Memo)).ToArray() ?? [];
        var timingAvailable = local?.Data is not null && localTimes.Length > 0;
        var timing = new CalendarOverview.Availability(timingAvailable, local?.IsStale == true, local?.FetchedAtUtc,
            preferences.Location is null ? "Choose a location to see local times."
            : !preferences.ShowLocalTimes ? "Local times are turned off in your calendar preferences."
            : timingAvailable ? local?.IsStale == true ? "Showing saved times for this location and calculation convention." : null
            : "Local times are unavailable for these dates. No estimated times are substituted.");
        var offset = preferences.CandleLightingMinutes ?? preferences.Location?.DefaultCandleLightingMinutes ?? 18;
        var convention = $"Candle-lighting: {offset} minutes before sunset where applicable; later festival lighting uses Hebcal's transition rules. Havdalah: {(preferences.Havdalah == "fixed" ? $"{preferences.HavdalahMinutes} minutes after sunset" : "nightfall, sun 8.5° below the horizon")}. Times use sea-level calculations.";
        var nextMidnight = new DateTimeOffset(TimeZoneInfo.ConvertTimeToUtc(today.AddDays(1).ToDateTime(TimeOnly.MinValue), zone));
        List<DateTimeOffset> boundaries = [nextMidnight];
        boundaries.AddRange(schedules.Select(result => result.RefreshAtUtc));
        if (solar is not null)
        {
            boundaries.Add(solar.RefreshAtUtc);
        }
        if (local is not null)
        {
            boundaries.Add(local.RefreshAtUtc);
            boundaries.AddRange(localTimes.Select(item => item.At));
        }
        if (todaysSun is not null)
        {
            boundaries.AddRange(new[] { todaysSun.Sunset, todaysSun.Nightfall, todaysSun.Dawn, shabbatEnd }.OfType<DateTimeOffset>());
        }
        // Expired/no-cache provider responses must not turn the browser into a polling loop.
        var nextRefresh = boundaries.Where(value => value > now).DefaultIfEmpty(now.AddMinutes(1)).Min();
        return new(preferences, new(today, hebrew.EnglishText, hebrew.HebrewText, zone.Id, afterSunset, isDaytimeOnly, solarStatus), shabbat, events, highlight, holidayStatus, localTimes, timing, convention, now, nextRefresh);
    }

    private async Task<CalendarProviderResult?> GetSolarAsync(CalendarLocation location, DateOnly today, CancellationToken cancellationToken) => await provider.GetSolarTimesAsync(location, today.AddDays(-1), today.AddDays(1), cancellationToken).ConfigureAwait(false);
    private async Task<CalendarProviderResult?> GetLocalAsync(CalendarPreferences preferences, DateOnly today, CancellationToken cancellationToken) => await provider.GetLocalEventsAsync(preferences, today, today.AddDays(14), cancellationToken).ConfigureAwait(false);
    private static DateTimeOffset? OldestFetch(IEnumerable<CalendarProviderResult> values) => values.Select(value => value.FetchedAtUtc).OfType<DateTimeOffset>().Select(value => (DateTimeOffset?)value).DefaultIfEmpty(null).Min();
    private static string TimingTitle(HebcalData.Event item) => item.Category switch { "candles" => "Candle-lighting", "havdalah" => "Havdalah", _ => item.Title };

    private static IReadOnlyList<CalendarOverview.AgendaEvent> GroupEvents(IReadOnlyList<HebcalData.Event> occurrences, CalendarPreferences preferences, DateOnly today, DateOnly visibleEnd, DateTimeOffset now, IReadOnlyDictionary<DateOnly, HebcalData.SunTimes>? solarDays, bool includeAllCategories)
    {
        List<CalendarOverview.AgendaEvent> result = [];
        foreach (var group in occurrences.GroupBy(item => CalendarEventDescriptions.Describe(item).Key))
        {
            // Same named holiday can occur again inside a 365-day agenda; only join adjacent dates.
            List<List<HebcalData.Event>> runs = [];
            foreach (var occurrence in group.OrderBy(item => item.Date))
            {
                if (runs.Count == 0 || occurrence.Date.DayNumber - runs[^1][^1].Date.DayNumber > 1)
                {
                    runs.Add([]);
                }
                runs[^1].Add(occurrence);
            }
            foreach (var run in runs)
            {
                var description = CalendarEventDescriptions.Describe(run[0]);
                if (!includeAllCategories && !IsCategoryEnabled(description.Category, preferences))
                {
                    continue;
                }
                var start = run[0].Date;
                var end = run[^1].Date;
                var beginning = description.Beginning == "previousSunset" ? start.AddDays(-1) : start;
                if (end < today || beginning > visibleEnd)
                {
                    continue;
                }
                var beginSun = solarDays?.GetValueOrDefault(beginning);
                DateTimeOffset? beginsAt = description.Beginning switch
                {
                    "previousSunset" or "sameEvening" => beginSun?.Sunset,
                    "nightfall" => beginSun?.Nightfall,
                    "dawn" => beginSun?.Dawn,
                    _ => null,
                };
                var endsAt = description.Beginning == "civilDate" ? null : solarDays?.GetValueOrDefault(end)?.Nightfall;
                if (end == today && endsAt is { } finish && now >= finish)
                {
                    continue;
                }
                var ongoing = beginning < today || (beginning == today && (beginsAt is { } begin ? now >= begin : description.Beginning is "dawn" or "civilDate"));
                result.Add(new($"{description.Key}:{start:yyyy-MM-dd}", description.Key, description.Title, description.Category, start, end, beginning, description.Beginning, description.Explanation, run[0].Link ?? "https://www.hebcal.com/holidays/", ongoing, run.Select(item => new CalendarOverview.Occurrence(item.Title, item.Date)).ToArray()));
            }
        }
        return result.OrderBy(item => item.BeginningDate).ThenBy(item => item.StartDate).ThenBy(item => item.Title, StringComparer.Ordinal).ToArray();
    }

    private static bool IsCategoryEnabled(string category, CalendarPreferences preferences) => category switch
    {
        "major" => preferences.MajorHolidays,
        "minor" => preferences.MinorHolidays,
        "fast" => preferences.FastDays,
        "roshChodesh" => preferences.RoshChodesh,
        "specialShabbat" => preferences.SpecialShabbatot,
        "modern" => preferences.ModernObservances,
        _ => false,
    };
}
