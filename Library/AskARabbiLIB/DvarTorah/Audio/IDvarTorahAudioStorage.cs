namespace AskARabbiLIB.DvarTorah.Audio;

/// <summary>Stores completed narration and supports recovery after an upload succeeded but database publication failed.</summary>
public interface IDvarTorahAudioStorage : IDvarTorahAudioReader
{
    /// <summary>Finds a complete previously uploaded version for safe retry without another synthesis.</summary>
    /// <param name="weekKey">Published reading week.</param>
    /// <param name="version">Expected content-and-voice hash.</param>
    /// <param name="cancellationToken">Operation cancellation.</param>
    /// <returns>Completed metadata, or null when both artifacts are not present.</returns>
    Task<WeeklyDvarTorahAudioMetadata?> FindStoredAsync(string weekKey, string version, CancellationToken cancellationToken = default);

    /// <summary>Uploads the MP3 followed by its timing manifest using the Hot tier.</summary>
    /// <param name="weekKey">Published reading week.</param>
    /// <param name="narration">Encoded and aligned narration.</param>
    /// <param name="createdAtUtc">UTC completion time.</param>
    /// <param name="cancellationToken">Operation cancellation.</param>
    /// <returns>Stable private recording metadata for MongoDB.</returns>
    Task<WeeklyDvarTorahAudioMetadata> UploadAsync(string weekKey, DvarTorahNarration narration, DateTimeOffset createdAtUtc, CancellationToken cancellationToken = default);
}
