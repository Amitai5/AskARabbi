namespace AskARabbiLIB.Usage;

/// <summary>Enforces monthly token admission and durable per-chat accounting.</summary>
public sealed class MonthlyUsageService
{
    private readonly IUsageStore store;
    private readonly TimeProvider timeProvider;

    /// <summary>Initializes monthly token accounting.</summary>
    /// <param name="store">Durable token and lease store.</param>
    /// <param name="tokenLimit">Included tokens per UTC calendar month.</param>
    /// <param name="timeProvider">UTC clock, defaulting to system time.</param>
    public MonthlyUsageService(IUsageStore store, long tokenLimit, TimeProvider? timeProvider = null)
    {
        ArgumentNullException.ThrowIfNull(store);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(tokenLimit);
        this.store = store;
        this.timeProvider = timeProvider ?? TimeProvider.System;
        TokenLimit = tokenLimit;
    }

    /// <summary>Gets the configured monthly token allowance.</summary>
    public long TokenLimit { get; }

    /// <summary>Reads usage for the current UTC calendar month.</summary>
    /// <param name="userId">Account owner.</param>
    /// <param name="cancellationToken">Operation cancellation.</param>
    /// <returns>The current monthly allowance.</returns>
    public async Task<BillingPeriodUsage> GetCurrentAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        ValidateUserId(userId);
        var start = GetMonthStart(timeProvider.GetUtcNow());
        return await GetPeriodAsync(userId, start, start.AddMonths(1), cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Admits a chat before message persistence or paid AI work.</summary>
    /// <param name="userId">Account owner.</param>
    /// <param name="cancellationToken">Operation cancellation.</param>
    /// <returns>Accounting ownership for the request's starting month.</returns>
    public async Task<ChatUsageLease> BeginChatAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        ValidateUserId(userId);
        var now = timeProvider.GetUtcNow();
        var start = GetMonthStart(now);
        // API mutations time out after five minutes. Ownership lasts longer to fence off
        // a stalled replica, while still recovering automatically after a process crash.
        var lease = new ChatUsageLease(userId, Guid.NewGuid(), start, start.AddMonths(1), now.AddMinutes(10), TokenLimit);
        if (await store.TryAcquireChatAsync(lease, now, cancellationToken).ConfigureAwait(false))
        {
            return lease;
        }

        var usage = await GetPeriodAsync(userId, lease.PeriodStartUtc, lease.PeriodEndUtc, cancellationToken).ConfigureAwait(false);
        ThrowIfLimitReached(usage);
        throw new ChatUsageException("chat_in_progress", "Another answer is still being prepared for your account. Please wait for it to finish, then try again.", usage);
    }

    /// <summary>Rechecks quota before every paid provider request, including repairs and retrieval.</summary>
    /// <param name="lease">Current accounting ownership.</param>
    /// <param name="cancellationToken">Operation cancellation.</param>
    /// <returns>The completion task.</returns>
    public async Task EnsureAvailableAsync(ChatUsageLease lease, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(lease);
        if (timeProvider.GetUtcNow() >= lease.ExpiresAtUtc)
        {
            throw new ChatUsageException("usage_unavailable", "The answer took too long. Please try again.");
        }
        var usage = await GetPeriodAsync(lease.UserId, lease.PeriodStartUtc, lease.PeriodEndUtc, cancellationToken).ConfigureAwait(false);
        ThrowIfLimitReached(usage);
    }

    /// <summary>Persists cumulative tokens without double-charging repeated accounting writes.</summary>
    /// <param name="lease">Current accounting ownership.</param>
    /// <param name="cumulativeTokens">All known tokens consumed by this chat attempt.</param>
    /// <param name="cancellationToken">Operation cancellation.</param>
    /// <returns>The completion task.</returns>
    public async Task RecordTokensAsync(ChatUsageLease lease, long cumulativeTokens, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(lease);
        ArgumentOutOfRangeException.ThrowIfNegative(cumulativeTokens);
        if (!await store.RecordTokensAsync(lease, cumulativeTokens, cancellationToken).ConfigureAwait(false))
        {
            throw new ChatUsageException("usage_unavailable", "Chat usage could not be saved. Please try again shortly.");
        }
    }

    /// <summary>Releases admission ownership while retaining all consumed tokens.</summary>
    /// <param name="lease">Current accounting ownership.</param>
    /// <param name="cancellationToken">Operation cancellation.</param>
    /// <returns>The completion task.</returns>
    public Task EndChatAsync(ChatUsageLease lease, CancellationToken cancellationToken = default) => store.ReleaseChatAsync(lease, cancellationToken);

    private async Task<BillingPeriodUsage> GetPeriodAsync(Guid userId, DateTimeOffset start, DateTimeOffset end, CancellationToken cancellationToken)
    {
        var tokens = await store.GetTokenCountAsync(userId, start, end, cancellationToken).ConfigureAwait(false);
        return new BillingPeriodUsage(start, end, tokens, TokenLimit);
    }

    private static void ThrowIfLimitReached(BillingPeriodUsage usage)
    {
        if (usage.IsLimitReached)
        {
            throw new ChatUsageException("usage_limit_reached", "You have used your monthly chat allowance. Chat resets at the start of next month (UTC). You can still read and listen to Dvar Torah.", usage);
        }
    }

    private static DateTimeOffset GetMonthStart(DateTimeOffset now) => new(now.UtcDateTime.Year, now.UtcDateTime.Month, 1, 0, 0, 0, TimeSpan.Zero);

    private static void ValidateUserId(Guid userId)
    {
        if (userId == Guid.Empty)
        {
            throw new ArgumentException("A user ID is required.", nameof(userId));
        }
    }
}
