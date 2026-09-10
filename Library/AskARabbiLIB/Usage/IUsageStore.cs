namespace AskARabbiLIB.Usage;

/// <summary>Persists account-wide token counters independently of deletable chat history.</summary>
public interface IUsageStore
{
    /// <summary>Reads the token count for one account and billing month.</summary>
    /// <param name="userId">Account owner.</param>
    /// <param name="periodStartUtc">Inclusive UTC month start.</param>
    /// <param name="periodEndUtc">Exclusive UTC month end.</param>
    /// <param name="cancellationToken">Operation cancellation.</param>
    /// <returns>The durable token count, or zero for a new month.</returns>
    Task<long> GetTokenCountAsync(Guid userId, DateTimeOffset periodStartUtc, DateTimeOffset periodEndUtc, CancellationToken cancellationToken = default);

    /// <summary>Atomically admits one chat only below quota and without another active chat lease.</summary>
    /// <param name="lease">Requested accounting ownership.</param>
    /// <param name="now">Current UTC instant for crash recovery.</param>
    /// <param name="cancellationToken">Operation cancellation.</param>
    /// <returns>Whether ownership was acquired.</returns>
    Task<bool> TryAcquireChatAsync(ChatUsageLease lease, DateTimeOffset now, CancellationToken cancellationToken = default);

    /// <summary>Idempotently advances an owned chat's cumulative tokens and monthly total.</summary>
    /// <param name="lease">Accounting ownership.</param>
    /// <param name="cumulativeTokens">Total provider-reported tokens for this lease, including failures.</param>
    /// <param name="cancellationToken">Operation cancellation.</param>
    /// <returns>Whether the accounting lease is still owned.</returns>
    Task<bool> RecordTokensAsync(ChatUsageLease lease, long cumulativeTokens, CancellationToken cancellationToken = default);

    /// <summary>Releases ownership without changing the durable monthly count.</summary>
    /// <param name="lease">Accounting ownership to release.</param>
    /// <param name="cancellationToken">Operation cancellation.</param>
    /// <returns>The completion task.</returns>
    Task ReleaseChatAsync(ChatUsageLease lease, CancellationToken cancellationToken = default);
}
