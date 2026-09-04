using Azure.Core;
using Microsoft.CognitiveServices.Speech;
using System.Collections.Concurrent;

namespace AskARabbiLIB.DvarTorah.Audio;

/// <summary>Synthesizes English/Hebrew narration in bounded chunks and produces exact word highlighting positions.</summary>
public sealed class AzureSpeechDvarTorahNarrator : IDvarTorahNarrator
{
    private const int PcmBytesPerSecond = 24_000 * 2;
    private readonly DvarTorahAudioOptions options;
    private readonly IDvarTorahMp3Encoder encoder;
    private readonly Func<string, CancellationToken, Task<DvarTorahSpeechAudio>> synthesize;

    /// <summary>Initializes the managed-identity Speech narrator.</summary>
    /// <param name="options">Speech resource, voice, and storage settings.</param>
    /// <param name="credential">Explicit managed identity in production.</param>
    /// <param name="encoder">Server-side single-file MP3 encoder.</param>
    public AzureSpeechDvarTorahNarrator(DvarTorahAudioOptions options, TokenCredential credential, IDvarTorahMp3Encoder encoder) : this(options, encoder, (ssml, cancellationToken) => SynthesizeAsync(ssml, options, credential, cancellationToken))
    {
        ArgumentNullException.ThrowIfNull(credential);
    }

    internal AzureSpeechDvarTorahNarrator(DvarTorahAudioOptions options, IDvarTorahMp3Encoder encoder, Func<string, CancellationToken, Task<DvarTorahSpeechAudio>> synthesize)
    {
        this.options = options ?? throw new ArgumentNullException(nameof(options));
        this.encoder = encoder ?? throw new ArgumentNullException(nameof(encoder));
        this.synthesize = synthesize ?? throw new ArgumentNullException(nameof(synthesize));
        options.ValidateGeneration();
    }

    /// <inheritdoc/>
    public async Task<DvarTorahNarration> GenerateAsync(WeeklyDvarTorahArticle article, string version, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(article);
        if (DvarTorahAudioText.GetVersion(article, options.Voice) != version)
        {
            throw new ArgumentException("The narration version does not match the article and voice.", nameof(version));
        }
        var title = DvarTorahAudioText.Normalize(article.Title);
        var body = DvarTorahAudioText.Normalize(article.Body);
        var chunks = DvarTorahAudioText.GetChunks("title", title).Concat(DvarTorahAudioText.GetChunks("body", body));
        var words = new List<DvarTorahAudioWord>();
        using var pcm = new MemoryStream();
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        deadline.CancelAfter(options.LeaseDuration - TimeSpan.FromMinutes(1));
        foreach (var chunk in chunks)
        {
            deadline.Token.ThrowIfCancellationRequested();
            var ssml = new DvarTorahSsml(chunk, options.Voice);
            var result = await synthesize(ssml.Text, deadline.Token).ConfigureAwait(false);
            if (result.Pcm.Length == 0 || result.Pcm.Length % 2 != 0 || pcm.Length + result.Pcm.Length > DvarTorahAudioValidation.MaximumPcmBytes)
            {
                throw new DvarTorahAudioException("AudioDataInvalid", "synthesis");
            }
            var offsetMs = pcm.Length * 1000d / PcmBytesPerSecond;
            var displayText = chunk.Section == "title" ? title : body;
            foreach (var boundary in result.Words)
            {
                var displayOffset = ssml.GetDisplayOffset(boundary.SsmlOffset);
                if (displayOffset < 0 || displayOffset > displayText.Length - boundary.Text.Length || !displayText.AsSpan(displayOffset, boundary.Text.Length).SequenceEqual(boundary.Text))
                {
                    throw new DvarTorahAudioException("WordAlignmentFailed", "alignment");
                }
                words.Add(new(chunk.Section, boundary.Text, displayOffset, boundary.Text.Length, offsetMs + boundary.AudioOffsetMs, boundary.DurationMs));
            }
            if (result.Words.Count == 0)
            {
                throw new DvarTorahAudioException("WordBoundariesMissing", "alignment");
            }
            await pcm.WriteAsync(result.Pcm, deadline.Token).ConfigureAwait(false);
        }
        var timings = new DvarTorahAudioTimings { Version = version, Voice = options.Voice, Title = title, Body = body, DurationMs = pcm.Length * 1000d / PcmBytesPerSecond, Words = words };
        DvarTorahAudioValidation.ValidateTimings(timings);
        var mp3 = await encoder.EncodeAsync(pcm, deadline.Token).ConfigureAwait(false);
        return new DvarTorahNarration(mp3, timings);
    }

    private static async Task<DvarTorahSpeechAudio> SynthesizeAsync(string ssml, DvarTorahAudioOptions options, TokenCredential credential, CancellationToken cancellationToken)
    {
        var accessToken = await credential.GetTokenAsync(new TokenRequestContext(["https://cognitiveservices.azure.com/.default"]), cancellationToken).ConfigureAwait(false);
        var configuration = SpeechConfig.FromAuthorizationToken($"aad#{options.SpeechResourceId}#{accessToken.Token}", options.SpeechRegion);
        configuration.SetSpeechSynthesisOutputFormat(SpeechSynthesisOutputFormat.Raw24Khz16BitMonoPcm);
        configuration.SetProperty(PropertyId.SpeechServiceResponse_RequestWordBoundary, "true");
        var boundaries = new ConcurrentQueue<DvarTorahSpeechWord>();
        using var synthesizer = new SpeechSynthesizer(configuration, audioConfig: null);
        synthesizer.WordBoundary += (_, boundary) =>
        {
            if (boundary.BoundaryType == SpeechSynthesisBoundaryType.Word)
            {
                boundaries.Enqueue(new(boundary.Text, boundary.TextOffset, boundary.AudioOffset / (double)TimeSpan.TicksPerMillisecond, boundary.Duration.TotalMilliseconds));
            }
        };
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        deadline.CancelAfter(TimeSpan.FromMinutes(3));
        SpeechSynthesisResult result;
        try
        {
            result = await synthesizer.SpeakSsmlAsync(ssml).WaitAsync(deadline.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            await synthesizer.StopSpeakingAsync().ConfigureAwait(false);
            throw;
        }
        using (result)
        {
            if (result.Reason != ResultReason.SynthesizingAudioCompleted)
            {
                var failure = SpeechSynthesisCancellationDetails.FromResult(result);
                // SDK diagnostics can include resource URLs and text; expose only the stable error code.
                throw new DvarTorahAudioException($"Speech{failure.ErrorCode}", "synthesis");
            }
            return new DvarTorahSpeechAudio(result.AudioData, boundaries.ToArray());
        }
    }
}
