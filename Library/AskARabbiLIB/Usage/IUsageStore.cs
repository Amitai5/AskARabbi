namespace AskARabbiLIB.Usage;

/// <summary>Persists per-user answer counts for exact billing periods.</summary>
public interface IUsageStore
{
    /// <summary>Gets the answer count for a user and billing period.</summary>
    /// <param name="userId">Owning user ID.</param>
    /// <param name="periodStartUtc">Inclusive UTC period start.</param>
    /// <param name="periodEndUtc">Exclusive UTC period end.</param>
    /// <param name="cancellationToken">Token that can cancel the operation.</param>
    /// <returns>The number of answers used in the period.</returns>
    Task<int> GetAnswerCountAsync(Guid userId, DateTimeOffset periodStartUtc, DateTimeOffset periodEndUtc, CancellationToken cancellationToken = default);

    /// <summary>Atomically records a completed answer for a user and billing period.</summary>
    /// <param name="userId">Owning user ID.</param>
    /// <param name="periodStartUtc">Inclusive UTC period start.</param>
    /// <param name="periodEndUtc">Exclusive UTC period end.</param>
    /// <param name="cancellationToken">Token that can cancel the operation.</param>
    /// <returns>The resulting answer count.</returns>
    Task<int> IncrementAnswerCountAsync(Guid userId, DateTimeOffset periodStartUtc, DateTimeOffset periodEndUtc, CancellationToken cancellationToken = default);
}
