using AskARabbiLIB.DvarTorah.Audio;

namespace AskARabbi.Api.DvarTorahAudio;

internal sealed class UnavailableDvarTorahAudioReader : IDvarTorahAudioReader
{
    /// <inheritdoc/>
    public Task<DvarTorahAudioBlobInfo?> GetInfoAsync(WeeklyDvarTorahAudioMetadata audio, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult<DvarTorahAudioBlobInfo?>(null);
    }

    /// <inheritdoc/>
    public Task<Stream> OpenReadAsync(WeeklyDvarTorahAudioMetadata audio, long offset, long? length, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        throw new InvalidOperationException("Dvar Torah audio storage is disabled.");
    }

    /// <inheritdoc/>
    public Task<DvarTorahAudioTimings?> GetTimingsAsync(WeeklyDvarTorahAudioMetadata audio, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult<DvarTorahAudioTimings?>(null);
    }
}
