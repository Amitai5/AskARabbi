using AskARabbiLIB.Calendar;
using AskARabbiLIB.Profiles;

namespace AskARabbiLIB.AI.Tools;

/// <summary>Exposes privacy-preserving Hebrew-calendar calculations to the grounded answer engine.</summary>
public sealed class CalendarAITools
{
    private readonly IHebrewCalendarService calendar;

    /// <summary>Creates calendar tools backed by deterministic local calculations.</summary>
    /// <param name="calendar">Hebrew calendar calculation service.</param>
    public CalendarAITools(IHebrewCalendarService calendar)
    {
        this.calendar = calendar ?? throw new ArgumentNullException(nameof(calendar));
    }

    /// <summary>Converts a supplied birth date, or the private saved profile birth date when omitted, to a Hebrew date.</summary>
    /// <param name="context">Private request context injected by the server and excluded from the provider schema.</param>
    /// <param name="birthDateTime">Optional ISO-8601 Gregorian birth date and time. Omit it to use the authenticated user's saved profile without exposing that date to the model.</param>
    /// <param name="occurredAfterSunset">True only when the birth is known to have occurred after local sunset; otherwise false.</param>
    /// <returns>A citable deterministic Hebrew-date calculation.</returns>
    [AITool("convert_birthdate_to_hebrew", "Convert a Gregorian birth date to a Hebrew date. Omit birthDateTime when asking about the authenticated user's saved profile; the server will use it privately. This is a calendar calculation, not a religious ruling.", "hebrew birthday", "hebrew birth date", "jewish birthday", "jewish birth date", "convert my birthday")]
    public AIToolExecutionResult ConvertBirthdateToHebrew(AIToolExecutionContext context, [AIToolParameter("Optional ISO-8601 Gregorian birth date and time. Omit this to use the authenticated user's saved profile privately.")] DateTime? birthDateTime = null, [AIToolParameter("Whether the birth definitely occurred after local sunset. Use false when unknown and explain the sunset caveat.")] bool occurredAfterSunset = false)
    {
        var usesPrivateProfile = birthDateTime is null;
        var effectiveBirthDateTime = birthDateTime ?? GetProfileBirthDateTime(context.UserProfile);
        if (effectiveBirthDateTime is null)
        {
            return AIToolExecutionResult.Failure("No birth date was supplied and the authenticated profile has no usable birth date.");
        }

        var converted = calendar.ConvertToHebrew(effectiveBirthDateTime.Value, occurredAfterSunset);
        var englishText = HebrewDateDisplayFormatter.Format(converted.EnglishText, context.UserProfile?.JewishHeritage);
        var subject = usesPrivateProfile ? "The saved profile's" : "The supplied";
        var exactText = $"{subject} Hebrew birth date is {englishText} ({converted.HebrewText}). The birth is treated as {(occurredAfterSunset ? "after" : "before")} local sunset. Hebrew dates begin at sunset, so the result may move by one Hebrew day if that assumption is wrong.";
        var data = new
        {
            EnglishText = englishText,
            converted.HebrewText,
            converted.HebrewYear,
            converted.HebrewMonth,
            converted.HebrewDay,
            usedPrivateProfile = usesPrivateProfile,
            occurredAfterSunset,
        };
        return AIToolExecutionResult.Success(data, new AIToolEvidence("Hebrew birth date", exactText));
    }

    /// <summary>Finds the weekly parashah for a date or for a saved profile's Hebrew birthday anniversary.</summary>
    /// <param name="context">Private request context injected by the server and excluded from the provider schema.</param>
    /// <param name="dateTime">Optional ISO-8601 date whose upcoming Shabbat should be inspected.</param>
    /// <param name="hebrewAnniversaryAge">Optional Hebrew birthday age, such as 13 for a typical bar mitzvah calculation; it uses the saved profile birth date privately.</param>
    /// <param name="inIsrael">Whether to use the Israel reading cycle. False uses the Diaspora cycle.</param>
    /// <param name="occurredAfterSunset">Whether the saved birth occurred after local sunset; used only with hebrewAnniversaryAge.</param>
    /// <returns>A citable deterministic weekly-reading calculation.</returns>
    [AITool("find_parashah_for_week", "Find the regular weekly Torah portion, or identify a festival-displaced week, for the Shabbat on or after a date. For a bar or bat mitzvah question, pass hebrewAnniversaryAge and omit dateTime so the private saved profile date is used.", "parasha", "parashah", "parashat", "parsha", "torah portion", "bar mitzvah", "bat mitzvah", "weekly portion", "sedra")]
    public AIToolExecutionResult FindParashahForWeek(AIToolExecutionContext context, [AIToolParameter("Optional ISO-8601 date in the relevant local calendar week. Omit when using hebrewAnniversaryAge or to use today's date.")] DateTime? dateTime = null, [AIToolParameter("Optional Hebrew birthday anniversary age. Use 13 for a typical bar mitzvah calculation and 12 for a typical bat mitzvah calculation only when that matches the user's question.")] int? hebrewAnniversaryAge = null, [AIToolParameter("True for the Israel Torah-reading cycle; false for the Diaspora cycle. Default to false unless the user specifies Israel.")] bool inIsrael = false, [AIToolParameter("Whether the saved birth definitely occurred after local sunset. This applies only to Hebrew anniversary calculations.")] bool occurredAfterSunset = false)
    {
        if (dateTime is not null && hebrewAnniversaryAge is not null)
        {
            return AIToolExecutionResult.Failure("Supply either dateTime or hebrewAnniversaryAge, not both.");
        }

        WeeklyParashahInfo reading;
        string basis;
        if (hebrewAnniversaryAge is not null)
        {
            var profileBirthDateTime = GetProfileBirthDateTime(context.UserProfile);
            if (profileBirthDateTime is null)
            {
                return AIToolExecutionResult.Failure("A Hebrew anniversary calculation requires a saved profile birth date.");
            }
            reading = calendar.FindHebrewAnniversaryParashah(profileBirthDateTime.Value, hebrewAnniversaryAge.Value, inIsrael, occurredAfterSunset);
            basis = $"the Shabbat on or after the saved profile's {hebrewAnniversaryAge.Value}th Hebrew birthday";
        }
        else
        {
            var requestedDate = dateTime ?? GetCurrentLocalDateTime(context);
            reading = calendar.FindParashahForWeek(requestedDate, inIsrael);
            basis = dateTime is null ? "the Shabbat on or after today's date" : $"the Shabbat on or after {requestedDate:MMMM d, yyyy}";
        }

        var cycle = inIsrael ? "Israel" : "Diaspora";
        var hebrewDate = HebrewDateDisplayFormatter.Format(reading.HebrewDate, context.UserProfile?.JewishHeritage);
        var readingText = reading.Parashah is not null
            ? $"The regular parashah for {basis} is {reading.Parashah}."
            : $"There is no regular weekly parashah for {basis}; the regular cycle is displaced{(reading.Holiday is null ? " by a festival reading" : $" by {reading.Holiday}")}.";
        var exactText = $"{readingText} The selected Shabbat is {reading.ShabbatDate:MMMM d, yyyy}, corresponding to {hebrewDate}, using the {cycle} reading cycle. {reading.CalculationNote}";
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

    /// <summary>Gets today's Gregorian and Hebrew dates in the authenticated profile's configured time zone.</summary>
    /// <param name="context">Private request context injected by the server and excluded from the provider schema.</param>
    /// <param name="occurredAfterSunset">Whether it is already after local sunset.</param>
    /// <returns>A citable deterministic current-date calculation.</returns>
    [AITool("get_today_as_hebrew_and_gregorian", "Return today's Gregorian and Hebrew dates using the saved profile time zone when available. Ask or explain the sunset assumption because Hebrew dates change at local sunset.", "today's date", "todays date", "date today", "today's hebrew date", "hebrew date today", "jewish date today", "what day is it")]
    public AIToolExecutionResult GetTodayAsHebrewAndGregorian(AIToolExecutionContext context, [AIToolParameter("Whether the current local time is known to be after sunset. Use false when unknown and explain that the Hebrew date may advance at sunset.")] bool occurredAfterSunset = false)
    {
        var localDateTime = GetCurrentLocalDateTime(context);
        var converted = calendar.ConvertToHebrew(localDateTime, occurredAfterSunset);
        var englishText = HebrewDateDisplayFormatter.Format(converted.EnglishText, context.UserProfile?.JewishHeritage);
        var timeZoneId = NormalizeTimeZoneId(context.UserProfile?.BirthTimeZone);
        var exactText = $"Today is {converted.GregorianDate:MMMM d, yyyy} in time zone {timeZoneId}. Its Hebrew date is {englishText} ({converted.HebrewText}) when the current time is treated as {(occurredAfterSunset ? "after" : "before")} local sunset. Hebrew dates begin at sunset, so the Hebrew date can advance before the Gregorian date changes.";
        var data = new
        {
            converted.GregorianDate,
            EnglishText = englishText,
            converted.HebrewText,
            converted.HebrewYear,
            converted.HebrewMonth,
            converted.HebrewDay,
            timeZoneId,
            occurredAfterSunset,
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
        var timeZoneId = NormalizeTimeZoneId(context.UserProfile?.BirthTimeZone);
        var timeZone = TimeZoneInfo.FindSystemTimeZoneById(timeZoneId);
        return TimeZoneInfo.ConvertTime(context.CurrentUtc, timeZone).DateTime;
    }

    private static string NormalizeTimeZoneId(string? value) => string.IsNullOrWhiteSpace(value) ? TimeZoneInfo.Utc.Id : value.Trim();
}
