namespace AskARabbiLIB.Usage;

/// <summary>Calculates calendar-month billing periods and reports answer usage.</summary>
public sealed class MonthlyUsageService
{
    private readonly IUsageStore store;
    private readonly TimeProvider timeProvider;

    /// <summary>Initializes a monthly usage service.</summary>
    /// <param name="store">Usage persistence boundary.</param>
    /// <param name="answerLimit">Included answers per calendar month.</param>
    /// <param name="timeProvider">Optional source of UTC time.</param>
    public MonthlyUsageService(IUsageStore store, int answerLimit, TimeProvider? timeProvider = null)
    {
        this.store = store ?? throw new ArgumentNullException(nameof(store));
        if (answerLimit < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(answerLimit), "Answer limit must be positive.");
        }

        AnswerLimit = answerLimit;
        this.timeProvider = timeProvider ?? TimeProvider.System;
    }

    /// <summary>Gets the configured monthly answer limit.</summary>
    public int AnswerLimit { get; }

    /// <summary>Gets usage for the current calendar-month billing period.</summary>
    /// <param name="userId">Owning user ID.</param>
    /// <param name="cancellationToken">Token that can cancel the operation.</param>
    /// <returns>Usage with exact inclusive start and exclusive end timestamps.</returns>
    public async Task<BillingPeriodUsage> GetCurrentAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        ValidateUserId(userId);
        var (start, end) = GetCurrentPeriod();
        var used = await store.GetAnswerCountAsync(userId, start, end, cancellationToken).ConfigureAwait(false);
        return new BillingPeriodUsage(start, end, used, AnswerLimit);
    }

    /// <summary>Records one completed answer in the current calendar-month billing period.</summary>
    /// <param name="userId">Owning user ID.</param>
    /// <param name="cancellationToken">Token that can cancel the operation.</param>
    /// <returns>Updated usage with exact period timestamps.</returns>
    public async Task<BillingPeriodUsage> RecordAnswerAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        ValidateUserId(userId);
        var (start, end) = GetCurrentPeriod();
        var used = await store.IncrementAnswerCountAsync(userId, start, end, cancellationToken).ConfigureAwait(false);
        return new BillingPeriodUsage(start, end, used, AnswerLimit);
    }

    private (DateTimeOffset Start, DateTimeOffset End) GetCurrentPeriod()
    {
        var now = timeProvider.GetUtcNow();
        var start = new DateTimeOffset(now.Year, now.Month, 1, 0, 0, 0, TimeSpan.Zero);
        return (start, start.AddMonths(1));
    }

    private static void ValidateUserId(Guid userId)
    {
        if (userId == Guid.Empty)
        {
            throw new ArgumentException("User ID is required.", nameof(userId));
        }
    }
}
