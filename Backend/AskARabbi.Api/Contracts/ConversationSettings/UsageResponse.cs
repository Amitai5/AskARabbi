namespace AskARabbi.Api.Contracts.ConversationSettings;

/// <summary>Reports the monthly token allowance and its percentage used.</summary>
public sealed record UsageResponse(DateTimeOffset PeriodStartUtc, DateTimeOffset PeriodEndUtc, long TokensUsed, long TokenLimit, long TokensRemaining, decimal UsedPercent, bool IsLimitReached)
{
    /// <summary>Maps durable monthly accounting to the public allowance contract.</summary>
    /// <param name="usage">Current monthly accounting.</param>
    /// <returns>The token allowance exposed to clients.</returns>
    public static UsageResponse FromUsage(AskARabbiLIB.Usage.BillingPeriodUsage usage) => new(usage.PeriodStartUtc, usage.PeriodEndUtc, usage.TokensUsed, usage.TokenLimit, usage.TokensRemaining, usage.UsedPercent, usage.IsLimitReached);
}
