using AskARabbiLIB.Calendar;
using AskARabbiLIB.Profiles;

namespace AskARabbiLIB.AI.Tools;

/// <summary>Exposes privacy-preserving Hebrew-calendar calculations to the grounded answer engine.</summary>
public sealed class CalendarAITools
{
    private readonly IHebrewCalendarService calendar;
    private readonly ICalendarSolarTimesProvider? solarTimes;

    /// <summary>Creates calendar tools backed by deterministic local calculations.</summary>
    /// <param name="calendar">Hebrew calendar calculation service.</param>
    /// <param name="solarTimes">Optional shared current-location solar boundaries.</param>
    public CalendarAITools(IHebrewCalendarService calendar, ICalendarSolarTimesProvider? solarTimes = null)
    {
        this.calendar = calendar ?? throw new ArgumentNullException(nameof(calendar));
        this.solarTimes = solarTimes;
    }

    /// <summary>Converts a supplied birth date, or the private saved profile birth date when omitted, to a Hebrew date.</summary>
    /// <param name="context">Private request context injected by the server and excluded from the provider schema.</param>
    /// <param name="birthDateTime">Optional ISO-8601 Gregorian birth date and time. Omit it to use the authenticated user's saved profile without exposing that date to the model.</param>
    /// <param name="occurredAfterSunset">Explicit user-provided sunset information; omit to calculate from the saved birthplace.</param>
    /// <returns>A citable deterministic Hebrew-date calculation.</returns>
    [AITool("convert_birthdate_to_hebrew", "Convert a Gregorian birth date to a Hebrew date. Omit birthDateTime when asking about the authenticated user's saved profile; the server will use it privately. This is a calendar calculation, not a religious ruling.", "hebrew birthday", "hebrew birth date", "jewish birthday", "jewish birth date", "convert my birthday")]
    public AIToolExecutionResult ConvertBirthdateToHebrew(AIToolExecutionContext context, [AIToolParameter("Optional ISO-8601 Gregorian birth date and time. Omit this to use the authenticated user's saved profile privately.")] DateTime? birthDateTime = null, [AIToolParameter("Omit to calculate sunset from the saved birthplace. Only override when the user explicitly supplies before/after-sunset information.")] bool? occurredAfterSunset = null)
    {
        var usesPrivateProfile = birthDateTime is null;
        var effectiveBirthDateTime = birthDateTime ?? GetProfileBirthDateTime(context.UserProfile);
        if (effectiveBirthDateTime is null)
        {
            return AIToolExecutionResult.Failure("No birth date was supplied and the authenticated profile has no usable birth date.");
        }

        var sunset = occurredAfterSunset ?? (usesPrivateProfile && context.UserProfile?.TimeOfBirth is not null ? BirthSunsetCalculator.IsAfterSunset(effectiveBirthDateTime.Value, context.UserProfile.BirthLocation) : null);
        var converted = calendar.ConvertToHebrew(effectiveBirthDateTime.Value, sunset ?? false);
        var englishText = HebrewDateDisplayFormatter.Format(converted.EnglishText, context.UserProfile?.JewishHeritage);
        var subject = usesPrivateProfile ? "The saved profile's" : "The supplied";
        var exactText = $"{subject} Hebrew birth date is {englishText} ({converted.HebrewText}). The birth is treated as {(sunset == true ? "after" : "before")} local sunset. {BirthSunsetNote(sunset, occurredAfterSunset)}";
        var data = new
        {
            EnglishText = englishText,
            converted.HebrewText,
            converted.HebrewYear,
            converted.HebrewMonth,
            converted.HebrewDay,
            usedPrivateProfile = usesPrivateProfile,
            occurredAfterSunset = sunset ?? false,
            sunsetWasResolved = sunset is not null,
        };
        return AIToolExecutionResult.Success(data, new AIToolEvidence("Hebrew birth date", exactText));
    }

    /// <summary>Finds the weekly parashah for a date or for a saved profile's Hebrew birthday anniversary.</summary>
    /// <param name="context">Private request context injected by the server and excluded from the provider schema.</param>
    /// <param name="dateTime">Optional ISO-8601 date whose upcoming Shabbat should be inspected.</param>
    /// <param name="hebrewAnniversaryAge">Optional Hebrew birthday age, such as 13 for a typical bar mitzvah calculation; it uses the saved profile birth date privately.</param>
    /// <param name="inIsrael">Explicit reading-cycle override; otherwise uses the current personalization location.</param>
    /// <param name="occurredAfterSunset">Whether the saved birth occurred after local sunset; used only with hebrewAnniversaryAge.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A citable deterministic weekly-reading calculation.</returns>
    [AITool("find_parashah_for_week", "Find the regular weekly Torah portion, or identify a festival-displaced week, for the Shabbat on or after a date. For a bar or bat mitzvah question, pass hebrewAnniversaryAge and omit dateTime so the private saved profile date is used.", "parasha", "parashah", "parashat", "parsha", "torah portion", "bar mitzvah", "bat mitzvah", "weekly portion", "sedra")]
    public async Task<AIToolExecutionResult> FindParashahForWeekAsync(AIToolExecutionContext context, [AIToolParameter("Optional ISO-8601 date in the relevant local calendar week. Omit when using hebrewAnniversaryAge or to use today's date.")] DateTime? dateTime = null, [AIToolParameter("Optional Hebrew birthday anniversary age. Use 13 for a typical bar mitzvah calculation and 12 for a typical bat mitzvah calculation only when that matches the user's question.")] int? hebrewAnniversaryAge = null, [AIToolParameter("Omit to use the saved current location's Israel/Diaspora schedule. Only override if the user explicitly requests a different reading cycle.")] bool? inIsrael = null, [AIToolParameter("Omit to calculate sunset from the private saved birthplace. Only override for explicit user-provided before/after-sunset information.")] bool? occurredAfterSunset = null, CancellationToken cancellationToken = default)
    {
        if (dateTime is not null && hebrewAnniversaryAge is not null)
        {
            return AIToolExecutionResult.Failure("Supply either dateTime or hebrewAnniversaryAge, not both.");
        }

        WeeklyParashahInfo reading;
        string basis;
        var useIsrael = inIsrael ?? context.UserProfile?.CurrentLocation?.UsesIsraelSchedule() ?? false;
        var sunsetNote = string.Empty;
        if (hebrewAnniversaryAge is not null)
        {
            var profileBirthDateTime = GetProfileBirthDateTime(context.UserProfile);
            if (profileBirthDateTime is null)
            {
                return AIToolExecutionResult.Failure("A Hebrew anniversary calculation requires a saved profile birth date.");
            }
            var sunset = occurredAfterSunset ?? (context.UserProfile?.TimeOfBirth is not null ? BirthSunsetCalculator.IsAfterSunset(profileBirthDateTime.Value, context.UserProfile.BirthLocation) : null);
            reading = calendar.FindHebrewAnniversaryParashah(profileBirthDateTime.Value, hebrewAnniversaryAge.Value, useIsrael, sunset ?? false);
            sunsetNote = BirthSunsetNote(sunset, occurredAfterSunset);
            basis = $"the Shabbat on or after the saved profile's {hebrewAnniversaryAge.Value}th Hebrew birthday";
        }
        else
        {
            var requestedDate = dateTime ?? GetCurrentLocalDateTime(context);
            if (dateTime is null && requestedDate.DayOfWeek == DayOfWeek.Saturday)
            {
                var times = await GetSolarTimesAsync(context, requestedDate, cancellationToken).ConfigureAwait(false);
                if (times?.Nightfall is { } nightfall && context.CurrentUtc >= nightfall)
                {
                    requestedDate = requestedDate.AddDays(1);
                }
            }
            reading = calendar.FindParashahForWeek(requestedDate, useIsrael);
            basis = dateTime is null ? "the Shabbat on or after today's date" : $"the Shabbat on or after {requestedDate:MMMM d, yyyy}";
        }

        var cycle = useIsrael ? "Israel" : "Diaspora";
        var hebrewDate = HebrewDateDisplayFormatter.Format(reading.HebrewDate, context.UserProfile?.JewishHeritage);
        var readingText = reading.Parashah is not null
            ? $"The regular parashah for {basis} is {reading.Parashah}."
            : $"There is no regular weekly parashah for {basis}; the regular cycle is displaced{(reading.Holiday is null ? " by a festival reading" : $" by {reading.Holiday}")}.";
        var exactText = $"{readingText} The selected Shabbat is {reading.ShabbatDate:MMMM d, yyyy}, corresponding to {hebrewDate}, using the {cycle} reading cycle. {reading.CalculationNote} {sunsetNote}".Trim();
        var data = new
        {
            reading.RequestedDate,
            reading.ShabbatDate,
            reading.Parashah,
            reading.Holiday,
            HebrewDate = hebrewDate,
            reading.InIsrael,
            reading.CalculationNote,
            hebrewAnniversaryAge,
        };
        return AIToolExecutionResult.Success(data, new AIToolEvidence("Weekly Torah reading", exactText));
    }

    /// <summary>Gets today's dates using the same current location and sunset data as the calendar page.</summary>
    /// <param name="context">Private request context injected by the server and excluded from the provider schema.</param>
    /// <param name="occurredAfterSunset">Whether it is already after local sunset.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A citable deterministic current-date calculation.</returns>
    [AITool("get_today_as_hebrew_and_gregorian", "Return today's Gregorian and Hebrew dates using the current location saved in Personalization and the calendar's local sunset data. Never use the birth location for today's date. Explain any unavailable-location or unavailable-sunset caveat returned by the server.", "today's date", "todays date", "date today", "today's hebrew date", "hebrew date today", "jewish date today", "what day is it")]
    public async Task<AIToolExecutionResult> GetTodayAsHebrewAndGregorianAsync(AIToolExecutionContext context, [AIToolParameter("Omit to use calculated local sunset. Override only if the user explicitly states before/after sunset.")] bool? occurredAfterSunset = null, CancellationToken cancellationToken = default)
    {
        var localDateTime = GetCurrentLocalDateTime(context);
        var times = await GetSolarTimesAsync(context, localDateTime, cancellationToken).ConfigureAwait(false);
        var sunset = occurredAfterSunset ?? (times?.Sunset is { } at ? context.CurrentUtc >= at : (bool?)null);
        var converted = calendar.ConvertToHebrew(localDateTime, sunset ?? false);
        var englishText = HebrewDateDisplayFormatter.Format(converted.EnglishText, context.UserProfile?.JewishHeritage);
        var timeZoneId = NormalizeTimeZoneId(context.UserProfile?.CurrentLocation?.TimeZone);
        var caveat = sunset is null ? "Sunset data is unavailable: this is the daytime Hebrew date and may be one day behind after sunset." : "Hebrew dates advance at local sunset, before the Gregorian date changes.";
        if (context.UserProfile?.CurrentLocation is null)
        {
            caveat += " No current location is saved; UTC is used. Set your current location in Personalization for local dates.";
        }
        if (times?.IsStale == true)
        {
            caveat += " Previously fetched solar times are being used.";
        }
        var exactText = $"Today is {converted.GregorianDate:MMMM d, yyyy} in time zone {timeZoneId}. Its Hebrew date is {englishText} ({converted.HebrewText}) when the current time is treated as {(sunset == true ? "after" : "before")} local sunset. {caveat}";
        var data = new
        {
            converted.GregorianDate,
            EnglishText = englishText,
            converted.HebrewText,
            converted.HebrewYear,
            converted.HebrewMonth,
            converted.HebrewDay,
            timeZoneId,
            occurredAfterSunset = sunset ?? false,
            sunsetWasResolved = sunset is not null,
        };
        return AIToolExecutionResult.Success(data, new AIToolEvidence("Current Gregorian and Hebrew date", exactText));
    }

    private static DateTime? GetProfileBirthDateTime(UserProfile? profile)
    {
        if (profile is null || profile.DateOfBirth == default)
        {
            return null;
        }
        return profile.DateOfBirth.ToDateTime(profile.TimeOfBirth ?? TimeOnly.MinValue, DateTimeKind.Unspecified);
    }

    private static DateTime GetCurrentLocalDateTime(AIToolExecutionContext context)
    {
        var timeZoneId = NormalizeTimeZoneId(context.UserProfile?.CurrentLocation?.TimeZone);
        var timeZone = TimeZoneInfo.FindSystemTimeZoneById(timeZoneId);
        return TimeZoneInfo.ConvertTime(context.CurrentUtc, timeZone).DateTime;
    }

    private static string NormalizeTimeZoneId(string? value) => string.IsNullOrWhiteSpace(value) ? TimeZoneInfo.Utc.Id : value.Trim();

    private Task<CalendarSolarTimes?> GetSolarTimesAsync(AIToolExecutionContext context, DateTime localDateTime, CancellationToken cancellationToken) => solarTimes is not null && context.UserProfile?.CurrentLocation is { } location ? solarTimes.GetAsync(location, DateOnly.FromDateTime(localDateTime), cancellationToken) : Task.FromResult<CalendarSolarTimes?>(null);

    private static string BirthSunsetNote(bool? sunset, bool? supplied) => sunset is null ? "Sunset is unknown: this is a before-sunset estimate and may shift by one Hebrew day. Add your birthplace in Personalization or supply known sunset information."
        : supplied is not null ? "The user's explicit before/after-sunset information was used."
        : "Local sunset was calculated privately from the saved birth time and birthplace at sea level; ZIP/city coordinates are approximate, so confirm births very near sunset with a qualified rabbi.";
}
