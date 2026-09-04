namespace AskARabbiLIB.DvarTorah.Audio;

/// <summary>Atomically attaches narration to existing publications without modifying article status or content.</summary>
public interface IWeeklyDvarTorahAudioStore
{
    /// <summary>Acquires narration ownership if the same article is still published and the version is missing.</summary>
    /// <param name="article">Exact expected published content.</param>
    /// <param name="version">Content-and-voice version.</param>
    /// <param name="leaseId">Unique invocation identifier.</param>
    /// <param name="acquiredAtUtc">UTC lease start.</param>
    /// <param name="expiresAtUtc">UTC recovery deadline.</param>
    /// <param name="cancellationToken">Operation cancellation.</param>
    /// <returns>Exclusive lease or null if another invocation owns or completed the version.</returns>
    Task<WeeklyDvarTorahAudioLease?> TryAcquireAudioLeaseAsync(WeeklyDvarTorahArticle article, string version, string leaseId, DateTimeOffset acquiredAtUtc, DateTimeOffset expiresAtUtc, CancellationToken cancellationToken = default);

    /// <summary>Attaches metadata only if the exact original article and active lease still match.</summary>
    /// <param name="lease">Owned narration lease.</param>
    /// <param name="article">Exact original published article.</param>
    /// <param name="audio">Completed private recording.</param>
    /// <param name="publishedAtUtc">UTC publication time.</param>
    /// <param name="cancellationToken">Operation cancellation.</param>
    /// <returns>True when the audio was attached.</returns>
    Task<bool> PublishAudioAsync(WeeklyDvarTorahAudioLease lease, WeeklyDvarTorahArticle article, WeeklyDvarTorahAudioMetadata audio, DateTimeOffset publishedAtUtc, CancellationToken cancellationToken = default);

    /// <summary>Releases only the owned audio lease, preserving the published article.</summary>
    /// <param name="lease">Owned narration lease.</param>
    /// <param name="failureCode">Safe failure code without content or credentials.</param>
    /// <param name="failedAtUtc">UTC failure time.</param>
    /// <param name="cancellationToken">Operation cancellation.</param>
    /// <returns>A task representing the operation.</returns>
    Task RecordAudioFailureAsync(WeeklyDvarTorahAudioLease lease, string failureCode, DateTimeOffset failedAtUtc, CancellationToken cancellationToken = default);
}
