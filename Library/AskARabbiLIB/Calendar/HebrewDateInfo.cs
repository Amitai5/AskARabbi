namespace AskARabbiLIB.Calendar;

/// <summary>Contains one deterministic Gregorian-to-Hebrew date conversion.</summary>
/// <param name="GregorianDate">Civil date used for the calculation.</param>
/// <param name="HebrewYear">Calculated Hebrew year.</param>
/// <param name="HebrewMonth">Calculated Hebrew month number in the .NET Hebrew calendar.</param>
/// <param name="HebrewDay">Calculated day of the Hebrew month.</param>
/// <param name="EnglishText">Transliterated Hebrew date.</param>
/// <param name="HebrewText">Hebrew-script date.</param>
/// <param name="WasAdvancedAfterSunset">Whether the supplied civil date was advanced because the event occurred after sunset.</param>
public sealed record HebrewDateInfo(DateOnly GregorianDate, int HebrewYear, int HebrewMonth, int HebrewDay, string EnglishText, string HebrewText, bool WasAdvancedAfterSunset);
