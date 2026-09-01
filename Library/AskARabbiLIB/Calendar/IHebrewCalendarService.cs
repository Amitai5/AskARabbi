namespace AskARabbiLIB.Calendar;

/// <summary>Provides deterministic Hebrew-date and weekly Torah-reading calculations.</summary>
public interface IHebrewCalendarService
{
    /// <summary>Converts a Gregorian date and time to its Hebrew date.</summary>
    /// <param name="gregorianDateTime">Gregorian civil date and time.</param>
    /// <param name="occurredAfterSunset">Whether to advance to the next Hebrew day because the event occurred after local sunset.</param>
    /// <returns>The calculated Hebrew date in numeric, transliterated, and Hebrew-script forms.</returns>
    HebrewDateInfo ConvertToHebrew(DateTime gregorianDateTime, bool occurredAfterSunset = false);

    /// <summary>Finds the regular parashah or festival status for the Shabbat on or after a date.</summary>
    /// <param name="dateTime">Date whose week should be inspected.</param>
    /// <param name="inIsrael">Whether to use the Israel rather than Diaspora reading cycle.</param>
    /// <returns>The selected Shabbat and its calculated reading status.</returns>
    WeeklyParashahInfo FindParashahForWeek(DateTime dateTime, bool inIsrael = false);

    /// <summary>Finds the Shabbat on or after a Hebrew birthday anniversary and its reading.</summary>
    /// <param name="birthDateTime">Gregorian birth date and time.</param>
    /// <param name="anniversaryAge">Hebrew birthday anniversary age, such as 13 for a typical bar mitzvah calculation.</param>
    /// <param name="inIsrael">Whether to use the Israel rather than Diaspora reading cycle.</param>
    /// <param name="occurredAfterSunset">Whether the birth occurred after local sunset.</param>
    /// <returns>The anniversary date's selected Shabbat and its calculated reading status.</returns>
    WeeklyParashahInfo FindHebrewAnniversaryParashah(DateTime birthDateTime, int anniversaryAge, bool inIsrael = false, bool occurredAfterSunset = false);
}
