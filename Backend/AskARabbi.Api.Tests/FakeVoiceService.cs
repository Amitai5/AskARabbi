using AskARabbi.Api.Voice;

namespace AskARabbi.Api.Tests;

internal sealed class FakeVoiceService : IVoiceService
{
    internal string Transcript { get; set; } = "What does Shabbat mean?";
    internal string? SpokenText { get; private set; }
    internal string? Language { get; private set; }
    internal byte[]? Audio { get; private set; }
    internal bool Fail { get; set; }
    internal int Calls { get; private set; }

    public Task<string> TranscribeAsync(byte[] pcm, string language, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        Calls++;
        Audio = pcm;
        Language = language;
        return Fail ? Task.FromException<string>(new VoiceUnavailableException()) : Task.FromResult(Transcript);
    }

    public Task<byte[]> SynthesizeAsync(string text, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        Calls++;
        SpokenText = text;
        return Fail ? Task.FromException<byte[]>(new VoiceUnavailableException()) : Task.FromResult(new byte[] { 1, 2, 3 });
    }
}
