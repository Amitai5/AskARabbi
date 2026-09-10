namespace AskARabbiLIB.Usage;

/// <summary>Reports provider-reported chat tokens consumed in a UTC calendar month.</summary>
public sealed record BillingPeriodUsage(DateTimeOffset PeriodStartUtc, DateTimeOffset PeriodEndUtc, long TokensUsed, long TokenLimit)
{
    /// <summary>Gets the remaining token allowance, clamped at zero.</summary>
    public long TokensRemaining => Math.Max(0, TokenLimit - TokensUsed);

    /// <summary>Gets the percentage consumed, clamped to zero through one hundred.</summary>
    public decimal UsedPercent => Math.Clamp(TokenLimit > 0 ? 100m * TokensUsed / TokenLimit : 100m, 0m, 100m);

    /// <summary>Gets whether another paid chat request is prohibited.</summary>
    public bool IsLimitReached => TokensRemaining == 0;
}
