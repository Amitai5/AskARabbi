namespace AskARabbiLIB.Profiles;

/// <summary>Contains user-provided context for tailoring an AskARabbi conversation.</summary>
public sealed record UserProfile
{
    /// <summary>Gets the display name used in conversation.</summary>
    public required string Name { get; init; }

    /// <summary>Gets the birth date used locally to calculate age.</summary>
    public required DateOnly DateOfBirth { get; init; }

    /// <summary>Gets optional user-provided background context.</summary>
    public string? Bio { get; init; }

    /// <summary>Gets the optional denomination or practice background.</summary>
    public string? ReligiousBackground { get; init; }

    /// <summary>Gets the required Jewish cultural or community heritage.</summary>
    public required string JewishHeritage { get; init; }

    /// <summary>Calculates the profile holder's age on a specified date.</summary>
    /// <param name="currentDate">Date on which to calculate the age.</param>
    /// <returns>The completed age in years.</returns>
    public int CalculateAge(DateOnly currentDate)
    {
        if (DateOfBirth == default)
        {
            throw new InvalidOperationException("Date of birth is required before age can be calculated.");
        }
        if (currentDate < DateOfBirth)
        {
            throw new ArgumentOutOfRangeException(nameof(currentDate), "Current date cannot be before the date of birth.");
        }

        var age = currentDate.Year - DateOfBirth.Year;
        if (DateOfBirth.AddYears(age) > currentDate)
        {
            age--;
        }

        return age;
    }

    /// <summary>Validates required fields, lengths, and the birth-date range.</summary>
    /// <param name="currentDate">Current date used to validate the date of birth and maximum age.</param>
    public void Validate(DateOnly currentDate)
    {
        ValidateRequiredText(Name, 120, nameof(Name));
        ValidateOptionalText(Bio, 2_000, nameof(Bio));
        ValidateOptionalText(ReligiousBackground, 250, nameof(ReligiousBackground));
        ValidateRequiredText(JewishHeritage, 250, nameof(JewishHeritage));
        var age = CalculateAge(currentDate);
        if (age > 130)
        {
            throw new InvalidOperationException("Date of birth cannot represent an age greater than 130 years.");
        }
    }

    private static void ValidateRequiredText(string value, int maximumLength, string propertyName)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException($"{propertyName} is required.", propertyName);
        }

        if (value.Trim().Length > maximumLength)
        {
            throw new ArgumentException($"{propertyName} cannot exceed {maximumLength:N0} characters.", propertyName);
        }
    }

    private static void ValidateOptionalText(string? value, int maximumLength, string propertyName)
    {
        if (value is not null && value.Trim().Length > maximumLength)
        {
            throw new ArgumentException($"{propertyName} cannot exceed {maximumLength:N0} characters.", propertyName);
        }
    }
}
