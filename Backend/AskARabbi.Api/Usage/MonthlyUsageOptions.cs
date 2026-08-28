namespace AskARabbi.Api.Usage;

/// <summary>Configures included answer usage for the current account tier.</summary>
public sealed record MonthlyUsageOptions
{
    /// <summary>Gets the configuration section name.</summary>
    public const string SectionName = "Usage";

    /// <summary>Gets the included answers in each UTC calendar month.</summary>
    public int MonthlyAnswerLimit { get; init; } = 50;

    /// <summary>Validates usage configuration.</summary>
    public void Validate()
    {
        if (MonthlyAnswerLimit < 1)
        {
            throw new InvalidOperationException($"{SectionName}:{nameof(MonthlyAnswerLimit)} must be positive.");
        }
    }
}
