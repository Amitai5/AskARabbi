namespace AskARabbiLIB.DvarTorah;

/// <summary>Coordinates exclusive generation and atomic publication for weekly articles.</summary>
public interface IWeeklyDvarTorahGenerationStore : IWeeklyDvarTorahStore
{
    /// <summary>Attempts to acquire exclusive generation ownership for a week.</summary>
    /// <param name="week">Reading week to generate.</param>
    /// <param name="leaseId">Unique invocation identifier.</param>
    /// <param name="acquiredAtUtc">UTC acquisition time.</param>
    /// <param name="expiresAtUtc">UTC recovery deadline.</param>
    /// <param name="cancellationToken">Token that can cancel the operation.</param>
    /// <returns>The acquired lease, or <see langword="null"/> when another attempt owns or published the week.</returns>
    Task<WeeklyDvarTorahGenerationLease?> TryAcquireGenerationLeaseAsync(WeeklyDvarTorahWeek week, string leaseId, DateTimeOffset acquiredAtUtc, DateTimeOffset expiresAtUtc, CancellationToken cancellationToken = default);

    /// <summary>Atomically publishes an article when the caller still owns its lease.</summary>
    /// <param name="lease">Owned generation lease.</param>
    /// <param name="article">Validated article to publish.</param>
    /// <param name="cancellationToken">Token that can cancel the operation.</param>
    /// <returns><see langword="true"/> when publication succeeded.</returns>
    Task<bool> PublishAsync(WeeklyDvarTorahGenerationLease lease, WeeklyDvarTorahArticle article, CancellationToken cancellationToken = default);

    /// <summary>Records a failed attempt and releases its lease for a later retry.</summary>
    /// <param name="lease">Owned generation lease.</param>
    /// <param name="failureCode">Safe diagnostic code that contains no generated content or secrets.</param>
    /// <param name="failedAtUtc">UTC failure time.</param>
    /// <param name="cancellationToken">Token that can cancel the operation.</param>
    /// <returns>A task representing the operation.</returns>
    Task RecordGenerationFailureAsync(WeeklyDvarTorahGenerationLease lease, string failureCode, DateTimeOffset failedAtUtc, CancellationToken cancellationToken = default);
}
