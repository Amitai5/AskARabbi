namespace AskARabbiLIB.DvarTorah.Audio;

/// <summary>Encodes concatenated 24 kHz, mono, 16-bit PCM into one seekable MP3.</summary>
public interface IDvarTorahMp3Encoder
{
    /// <summary>Encodes audio without changing its sample rate or playback duration.</summary>
    /// <param name="pcm">Complete raw PCM; the caller owns the seekable input stream.</param>
    /// <param name="cancellationToken">Operation cancellation.</param>
    /// <returns>A complete MP3 with seek and encoder-padding metadata.</returns>
    Task<ReadOnlyMemory<byte>> EncodeAsync(Stream pcm, CancellationToken cancellationToken = default);
}
