namespace AskARabbi.Api.Usage;

/// <summary>Configures the monthly chat token allowance for each account.</summary>
public sealed record MonthlyUsageOptions
{
    /// <summary>Gets the configuration section name.</summary>
    public const string SectionName = "Usage";

    /// <summary>Gets the included input and output tokens in each UTC calendar month.</summary>
    public long MonthlyTokenLimit { get; init; } = 5_000_000;

    /// <summary>Validates usage configuration.</summary>
    public void Validate()
    {
        if (MonthlyTokenLimit < 1)
        {
            throw new InvalidOperationException($"{SectionName}:{nameof(MonthlyTokenLimit)} must be positive.");
        }
    }
}
