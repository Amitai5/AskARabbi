using AskARabbiLIB.Calendar;

namespace AskARabbiLIB.DvarTorah;

/// <summary>Identifies one upcoming Shabbat and its configured Torah-reading cycle.</summary>
public sealed record WeeklyDvarTorahWeek
{
    /// <summary>Initializes a weekly Dvar Torah publication key.</summary>
    /// <param name="shabbatDate">Gregorian date of the relevant Shabbat.</param>
    /// <param name="hebrewDate">Display-ready Hebrew date for that Shabbat.</param>
    /// <param name="parashah">Regular weekly parashah, when one is assigned.</param>
    /// <param name="holiday">Festival reading that displaces the regular cycle, when applicable.</param>
    /// <param name="inIsrael">Whether this uses the Israel reading cycle.</param>
    public WeeklyDvarTorahWeek(DateOnly shabbatDate, string hebrewDate, string? parashah, string? holiday, bool inIsrael)
    {
        if (shabbatDate.DayOfWeek != DayOfWeek.Saturday)
        {
            throw new ArgumentException("A weekly Dvar Torah must be keyed to a Saturday.", nameof(shabbatDate));
        }
        if (string.IsNullOrWhiteSpace(hebrewDate))
        {
            throw new ArgumentException("The Hebrew date is required.", nameof(hebrewDate));
        }

        ShabbatDate = shabbatDate;
        HebrewDate = hebrewDate.Trim();
        Parashah = NormalizeOptional(parashah);
        Holiday = NormalizeOptional(holiday);
        InIsrael = inIsrael;
    }

    /// <summary>Gets the stable reading-cycle and Shabbat key.</summary>
    public string WeekKey => CreateWeekKey(ShabbatDate, InIsrael);

    /// <summary>Gets the Gregorian date of the relevant Shabbat.</summary>
    public DateOnly ShabbatDate { get; }

    /// <summary>Gets the display-ready Hebrew date.</summary>
    public string HebrewDate { get; }

    /// <summary>Gets the regular weekly parashah, when one is assigned.</summary>
    public string? Parashah { get; }

    /// <summary>Gets the festival reading that displaces the regular cycle, when applicable.</summary>
    public string? Holiday { get; }

    /// <summary>Gets whether this uses the Israel reading cycle.</summary>
    public bool InIsrael { get; }

    /// <summary>Creates a weekly publication key from a calculated reading.</summary>
    /// <param name="value">Calculated weekly reading.</param>
    /// <returns>The corresponding weekly publication key.</returns>
    public static WeeklyDvarTorahWeek FromParashah(WeeklyParashahInfo value)
    {
        ArgumentNullException.ThrowIfNull(value);
        return new WeeklyDvarTorahWeek(value.ShabbatDate, value.HebrewDate, value.Parashah, value.Holiday, value.InIsrael);
    }

    /// <summary>Creates the stable MongoDB identifier for a reading cycle and Shabbat.</summary>
    /// <param name="shabbatDate">Gregorian date of the relevant Shabbat.</param>
    /// <param name="inIsrael">Whether this uses the Israel reading cycle.</param>
    /// <returns>A stable, culture-invariant weekly key.</returns>
    public static string CreateWeekKey(DateOnly shabbatDate, bool inIsrael)
    {
        if (shabbatDate.DayOfWeek != DayOfWeek.Saturday)
        {
            throw new ArgumentException("A weekly Dvar Torah must be keyed to a Saturday.", nameof(shabbatDate));
        }

        return FormattableString.Invariant($"{(inIsrael ? "israel" : "diaspora")}:{shabbatDate:yyyy-MM-dd}");
    }

    private static string? NormalizeOptional(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
