namespace AskARabbiLIB.DvarTorah.Audio;

/// <summary>Reads narration only from configured private storage, never arbitrary URLs.</summary>
public interface IDvarTorahAudioReader
{
    /// <summary>Reads audio properties without its body.</summary>
    /// <param name="audio">Trusted stored narration metadata.</param>
    /// <param name="cancellationToken">Operation cancellation.</param>
    /// <returns>Properties or null when the blob no longer exists.</returns>
    Task<DvarTorahAudioBlobInfo?> GetInfoAsync(WeeklyDvarTorahAudioMetadata audio, CancellationToken cancellationToken = default);

    /// <summary>Opens a bounded network stream; the caller owns disposal.</summary>
    /// <param name="audio">Trusted stored narration metadata.</param>
    /// <param name="offset">First byte to read.</param>
    /// <param name="length">Maximum bytes to read, or null for the remainder.</param>
    /// <param name="cancellationToken">Operation cancellation.</param>
    /// <returns>A network-backed stream without buffering the recording.</returns>
    Task<Stream> OpenReadAsync(WeeklyDvarTorahAudioMetadata audio, long offset, long? length, CancellationToken cancellationToken = default);

    /// <summary>Reads and validates a narration's bounded timing manifest.</summary>
    /// <param name="audio">Trusted stored narration metadata.</param>
    /// <param name="cancellationToken">Operation cancellation.</param>
    /// <returns>Validated timings, or null if unavailable.</returns>
    Task<DvarTorahAudioTimings?> GetTimingsAsync(WeeklyDvarTorahAudioMetadata audio, CancellationToken cancellationToken = default);
}
