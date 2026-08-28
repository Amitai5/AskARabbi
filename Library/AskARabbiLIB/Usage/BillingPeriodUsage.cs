namespace AskARabbiLIB.Usage;

/// <summary>Describes usage for one exact UTC billing period.</summary>
public sealed record BillingPeriodUsage(DateTimeOffset PeriodStartUtc, DateTimeOffset PeriodEndUtc, int AnswersUsed, int AnswerLimit)
{
    /// <summary>Gets the remaining included answers without returning a negative value.</summary>
    public int AnswersRemaining => Math.Max(0, AnswerLimit - AnswersUsed);
}
