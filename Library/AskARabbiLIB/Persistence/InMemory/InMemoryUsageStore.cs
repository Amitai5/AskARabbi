using AskARabbiLIB.Usage;

namespace AskARabbiLIB.Persistence.InMemory;

/// <summary>Provides isolated, thread-safe monthly accounting for local development and deterministic tests.</summary>
public sealed class InMemoryUsageStore : IUsageStore
{
    private readonly object synchronization = new();
    private readonly Dictionary<(Guid UserId, DateTimeOffset Start), UsageEntry> entries = [];

    /// <inheritdoc/>
    public Task<long> GetTokenCountAsync(Guid userId, DateTimeOffset periodStartUtc, DateTimeOffset periodEndUtc, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        lock (synchronization)
        {
            entries.TryGetValue((userId, periodStartUtc), out var entry);
            return Task.FromResult(entry?.Tokens ?? 0);
        }
    }

    /// <inheritdoc/>
    public Task<bool> TryAcquireChatAsync(ChatUsageLease lease, DateTimeOffset now, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(lease);
        cancellationToken.ThrowIfCancellationRequested();
        lock (synchronization)
        {
            var key = (lease.UserId, lease.PeriodStartUtc);
            if (!entries.TryGetValue(key, out var entry))
            {
                entries[key] = entry = new UsageEntry();
            }
            if (entry.Tokens >= lease.TokenLimit || entry.Lease?.ExpiresAtUtc > now)
            {
                return Task.FromResult(false);
            }
            entry.Lease = lease;
            entry.LeaseTokens = 0;
            return Task.FromResult(true);
        }
    }

    /// <inheritdoc/>
    public Task<bool> RecordTokensAsync(ChatUsageLease lease, long cumulativeTokens, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(lease);
        ArgumentOutOfRangeException.ThrowIfNegative(cumulativeTokens);
        cancellationToken.ThrowIfCancellationRequested();
        lock (synchronization)
        {
            if (!entries.TryGetValue((lease.UserId, lease.PeriodStartUtc), out var entry) || entry.Lease?.Id != lease.Id)
            {
                return Task.FromResult(false);
            }
            if (cumulativeTokens > entry.LeaseTokens)
            {
                entry.Tokens = checked(entry.Tokens + cumulativeTokens - entry.LeaseTokens);
                entry.LeaseTokens = cumulativeTokens;
            }
            return Task.FromResult(true);
        }
    }

    /// <inheritdoc/>
    public Task ReleaseChatAsync(ChatUsageLease lease, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        lock (synchronization)
        {
            if (entries.TryGetValue((lease.UserId, lease.PeriodStartUtc), out var entry) && entry.Lease?.Id == lease.Id)
            {
                entry.Lease = null;
            }
        }
        return Task.CompletedTask;
    }

    /// <summary>Erases usage only when the entire account is deleted, never when chats are cleared.</summary>
    /// <param name="userId">Account being erased.</param>
    public void DeleteAccount(Guid userId)
    {
        lock (synchronization)
        {
            foreach (var key in entries.Keys.Where(key => key.UserId == userId).ToArray())
            {
                entries.Remove(key);
            }
        }
    }

    private sealed class UsageEntry
    {
        internal long Tokens { get; set; }
        internal long LeaseTokens { get; set; }
        internal ChatUsageLease? Lease { get; set; }
    }
}
