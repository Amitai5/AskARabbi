namespace AskARabbiLIB.Calendar;

/// <summary>Contains the regular Torah portion or festival reading status for one Shabbat.</summary>
/// <param name="RequestedDate">Date from which the Shabbat was selected.</param>
/// <param name="ShabbatDate">Selected Shabbat date.</param>
/// <param name="Parashah">Regular weekly parashah, or null when no regular portion is assigned.</param>
/// <param name="Holiday">Festival name when the regular weekly cycle is displaced, when available.</param>
/// <param name="HebrewDate">Hebrew date of the selected Shabbat.</param>
/// <param name="InIsrael">Whether the Israel reading cycle was used.</param>
/// <param name="CalculationNote">Important assumptions or calendar qualifications.</param>
public sealed record WeeklyParashahInfo(DateOnly RequestedDate, DateOnly ShabbatDate, string? Parashah, string? Holiday, string HebrewDate, bool InIsrael, string CalculationNote);
