namespace AskARabbi.Api.Voice;

/// <summary>Isolates transient speech processing from saved conversation generation.</summary>
public interface IVoiceService
{
    /// <summary>Transcribes a bounded mono 16 kHz, signed 16-bit little-endian PCM utterance.</summary>
    /// <param name="pcm">Raw audio, without a WAV header.</param>
    /// <param name="language">Supported recognition locale.</param>
    /// <param name="cancellationToken">Request cancellation.</param>
    /// <returns>Recognized text, or empty text when no speech was recognized.</returns>
    Task<string> TranscribeAsync(byte[] pcm, string language, CancellationToken cancellationToken);

    /// <summary>Synthesizes the canonical saved assistant answer without changing its text.</summary>
    /// <param name="text">Validated answer text.</param>
    /// <param name="cancellationToken">Request cancellation.</param>
    /// <returns>MP3 audio.</returns>
    Task<byte[]> SynthesizeAsync(string text, CancellationToken cancellationToken);
}
