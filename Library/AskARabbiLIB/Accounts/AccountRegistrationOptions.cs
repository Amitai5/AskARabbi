namespace AskARabbiLIB.Accounts;

/// <summary>Controls public registration without restricting existing accounts.</summary>
public sealed class AccountRegistrationOptions
{
    /// <summary>Gets the application configuration section.</summary>
    public const string SectionName = "Registration";

    /// <summary>Gets the maximum number of accounts; zero means unlimited.</summary>
    public int AccountLimit { get; init; } = 100;

    /// <summary>Validates the configured account ceiling.</summary>
    public void Validate()
    {
        if (AccountLimit < 0)
        {
            throw new InvalidOperationException("Registration:AccountLimit must be zero (unlimited) or a positive number.");
        }
    }
}
