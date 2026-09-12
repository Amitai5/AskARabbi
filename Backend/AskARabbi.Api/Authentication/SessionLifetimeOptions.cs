namespace AskARabbi.Api.Authentication;

/// <summary>Limits persistent browser sign-in independently of the identity provider's session policy.</summary>
public sealed class SessionLifetimeOptions
{
    /// <summary>Gets the application-session configuration section name.</summary>
    public const string SectionName = "Session";

    /// <summary>Gets the maximum number of days since interactive sign-in, regardless of activity.</summary>
    public int MaximumLifetimeDays { get; init; } = 30;

    /// <summary>Gets the number of days without an authenticated API request before sign-in expires.</summary>
    public int InactivityTimeoutDays { get; init; } = 7;

    /// <summary>Validates finite positive session limits with an inactivity timeout no longer than the maximum lifetime.</summary>
    public void Validate()
    {
        if (MaximumLifetimeDays is < 1 or > 365)
        {
            throw new InvalidOperationException($"{SectionName}:MaximumLifetimeDays must be between 1 and 365.");
        }
        if (InactivityTimeoutDays < 1 || InactivityTimeoutDays > MaximumLifetimeDays)
        {
            throw new InvalidOperationException($"{SectionName}:InactivityTimeoutDays must be between 1 and {SectionName}:MaximumLifetimeDays.");
        }
    }
}
