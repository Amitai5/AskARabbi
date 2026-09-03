using System.Globalization;
using System.Reflection;
using Zmanim.JewishCalendar;
using ZmanimCalendar = Zmanim.JewishCalendar.JewishCalendar;

namespace AskARabbiLIB.Calendar;

/// <summary>Calculates Hebrew dates and weekly Torah readings with the pinned Zmanim calendar tables.</summary>
public sealed class HebrewCalendarService : IHebrewCalendarService
{
    private static readonly FieldInfo ParshaListField = typeof(ZmanimCalendar).GetField("ParshaList", BindingFlags.Instance | BindingFlags.NonPublic) ?? throw new InvalidOperationException("Zmanim 1.5.0 no longer exposes the expected pinned parashah table.");
    private readonly HebrewCalendar calendar = new();

    /// <inheritdoc/>
    public HebrewDateInfo ConvertToHebrew(DateTime gregorianDateTime, bool occurredAfterSunset = false)
    {
        var civilDate = gregorianDateTime.Date;
        var effectiveDate = occurredAfterSunset ? civilDate.AddDays(1) : civilDate;
        ValidateSupportedDate(effectiveDate, nameof(gregorianDateTime));

        var englishFormatter = new HebrewDateFormatter();
        var hebrewFormatter = new HebrewDateFormatter { HebrewFormat = true };
        return new HebrewDateInfo(
            DateOnly.FromDateTime(civilDate),
            calendar.GetYear(effectiveDate),
            calendar.GetMonth(effectiveDate),
            calendar.GetDayOfMonth(effectiveDate),
            englishFormatter.Format(effectiveDate),
            hebrewFormatter.Format(effectiveDate),
            occurredAfterSunset);
    }

    /// <inheritdoc/>
    public WeeklyParashahInfo FindParashahForWeek(DateTime dateTime, bool inIsrael = false)
    {
        var requestedDate = dateTime.Date;
        ValidateSupportedDate(requestedDate, nameof(dateTime));
        var daysUntilSaturday = ((int)DayOfWeek.Saturday - (int)requestedDate.DayOfWeek + 7) % 7;
        var shabbat = requestedDate.AddDays(daysUntilSaturday);
        ValidateSupportedDate(shabbat, nameof(dateTime));
        return CreateParashahInfo(requestedDate, shabbat, inIsrael, "The Shabbat on or after the requested civil date was used.");
    }

    /// <inheritdoc/>
    public WeeklyParashahInfo FindHebrewAnniversaryParashah(DateTime birthDateTime, int anniversaryAge, bool inIsrael = false, bool occurredAfterSunset = false)
    {
        if (anniversaryAge is < 1 or > 130)
        {
            throw new ArgumentOutOfRangeException(nameof(anniversaryAge), "Anniversary age must be between 1 and 130.");
        }

        var effectiveBirthDate = occurredAfterSunset ? birthDateTime.Date.AddDays(1) : birthDateTime.Date;
        ValidateSupportedDate(effectiveBirthDate, nameof(birthDateTime));
        var birthYear = calendar.GetYear(effectiveBirthDate);
        var birthMonth = calendar.GetMonth(effectiveBirthDate);
        var birthDay = calendar.GetDayOfMonth(effectiveBirthDate);
        var anniversaryYear = checked(birthYear + anniversaryAge);
        var anniversaryMonth = MapAnniversaryMonth(birthYear, birthMonth, anniversaryYear);
        var anniversaryDay = Math.Min(birthDay, calendar.GetDaysInMonth(anniversaryYear, anniversaryMonth));
        var anniversaryDate = calendar.ToDateTime(anniversaryYear, anniversaryMonth, anniversaryDay, 12, 0, 0, 0);
        var daysUntilSaturday = ((int)DayOfWeek.Saturday - (int)anniversaryDate.DayOfWeek + 7) % 7;
        var shabbat = anniversaryDate.AddDays(daysUntilSaturday);
        var sunsetNote = occurredAfterSunset
            ? "The birth was treated as occurring after local sunset."
            : "The birth was treated as occurring before local sunset; if it occurred after sunset, the anniversary may shift.";
        var note = $"The Shabbat on or after the {anniversaryAge}th Hebrew birthday was used. {sunsetNote} Communities may choose a different celebration or reading date.";
        if (anniversaryDay != birthDay)
        {
            note = $"The Hebrew birthday day was clamped to the final day of its anniversary month. {note}";
        }
        if (!calendar.IsLeapYear(birthYear) && birthMonth == 6 && calendar.IsLeapYear(anniversaryYear))
        {
            note = $"A birth in Adar of a common year was mapped to Adar II in the leap anniversary year; customs can differ. {note}";
        }

        return CreateParashahInfo(anniversaryDate, shabbat, inIsrael, note);
    }

    private WeeklyParashahInfo CreateParashahInfo(DateTime requestedDate, DateTime shabbat, bool inIsrael, string note)
    {
        var zmanimCalendar = new ZmanimCalendar();
        var parashah = GetCorrectedParashah(zmanimCalendar, shabbat, inIsrael);
        var formatter = new HebrewDateFormatter();
        var parashahName = parashah == ZmanimCalendar.Parsha.NONE ? null : formatter.TransliteratedParshiosList[(int)parashah];
        var holiday = parashahName is null ? NormalizeOptional(formatter.FormatYomTov(shabbat, inIsrael)) : null;
        return new WeeklyParashahInfo(DateOnly.FromDateTime(requestedDate), DateOnly.FromDateTime(shabbat), parashahName, holiday, formatter.Format(shabbat), inIsrael, note);
    }

    private ZmanimCalendar.Parsha GetCorrectedParashah(ZmanimCalendar zmanimCalendar, DateTime shabbat, bool inIsrael)
    {
        var year = calendar.GetYear(shabbat);
        var roshHashanah = calendar.ToDateTime(year, 1, 1, 12, 0, 0, 0);
        var dayIndex = (int)calendar.GetDayOfWeek(roshHashanah) + (shabbat.Date - roshHashanah.Date).Days;
        var yearType = GetCorrectedYearType(year, inIsrael);
        var parshaList = ParshaListField.GetValue(zmanimCalendar) as ZmanimCalendar.Parsha[,] ?? throw new InvalidOperationException("Zmanim 1.5.0 returned an invalid pinned parashah table.");
        var weekIndex = dayIndex / 7;
        if (yearType < 0 || yearType >= parshaList.GetLength(0) || weekIndex < 0 || weekIndex >= parshaList.GetLength(1))
        {
            throw new InvalidOperationException("The requested date falls outside the pinned Zmanim parashah table.");
        }

        return parshaList[yearType, weekIndex];
    }

    private int GetCorrectedYearType(int year, bool inIsrael)
    {
        var roshHashanah = calendar.ToDateTime(year, 1, 1, 12, 0, 0, 0);
        var dayOfWeek = calendar.GetDayOfWeek(roshHashanah);
        var isLeapYear = calendar.IsLeapYear(year);
        var isCheshvanLong = calendar.GetDaysInMonth(year, 2) == 30;
        var isKislevShort = calendar.GetDaysInMonth(year, 3) == 29;

        return (isLeapYear, dayOfWeek, isCheshvanLong, isKislevShort, inIsrael) switch
        {
            (true, DayOfWeek.Monday, false, true, true) => 14,
            (true, DayOfWeek.Monday, false, true, false) => 6,
            (true, DayOfWeek.Monday, true, false, true) => 15,
            (true, DayOfWeek.Monday, true, false, false) => 7,
            (true, DayOfWeek.Tuesday, _, _, true) => 15,
            (true, DayOfWeek.Tuesday, _, _, false) => 7,
            (true, DayOfWeek.Thursday, false, true, _) => 8,
            (true, DayOfWeek.Thursday, true, false, _) => 9,
            (true, DayOfWeek.Saturday, false, true, _) => 10,
            (true, DayOfWeek.Saturday, true, false, true) => 16,
            (true, DayOfWeek.Saturday, true, false, false) => 11,
            (false, DayOfWeek.Monday, false, true, _) => 0,
            (false, DayOfWeek.Monday, true, false, true) => 12,
            (false, DayOfWeek.Monday, true, false, false) => 1,
            (false, DayOfWeek.Tuesday, _, _, true) => 12,
            (false, DayOfWeek.Tuesday, _, _, false) => 1,
            (false, DayOfWeek.Thursday, true, false, _) => 3,
            (false, DayOfWeek.Thursday, false, false, true) => 13,
            (false, DayOfWeek.Thursday, false, false, false) => 2,
            (false, DayOfWeek.Saturday, false, true, _) => 4,
            (false, DayOfWeek.Saturday, true, false, _) => 5,
            _ => throw new InvalidOperationException($"Hebrew year {year} has an unsupported calendar pattern."),
        };
    }

    private int MapAnniversaryMonth(int birthYear, int birthMonth, int anniversaryYear)
    {
        var birthYearIsLeap = calendar.IsLeapYear(birthYear);
        var anniversaryYearIsLeap = calendar.IsLeapYear(anniversaryYear);
        if (birthYearIsLeap == anniversaryYearIsLeap)
        {
            return birthMonth;
        }
        if (!birthYearIsLeap && anniversaryYearIsLeap)
        {
            return birthMonth == 6 ? 7 : birthMonth >= 7 ? birthMonth + 1 : birthMonth;
        }

        return birthMonth is 6 or 7 ? 6 : birthMonth >= 8 ? birthMonth - 1 : birthMonth;
    }

    private void ValidateSupportedDate(DateTime value, string parameterName)
    {
        if (value < calendar.MinSupportedDateTime.Date || value > calendar.MaxSupportedDateTime.Date)
        {
            throw new ArgumentOutOfRangeException(parameterName, $"Date must be between {calendar.MinSupportedDateTime:yyyy-MM-dd} and {calendar.MaxSupportedDateTime:yyyy-MM-dd}.");
        }
    }

    private static string? NormalizeOptional(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
