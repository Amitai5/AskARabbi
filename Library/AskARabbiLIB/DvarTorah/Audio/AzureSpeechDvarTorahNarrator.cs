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
            var alignmentCursor = 0;
            foreach (var boundary in result.Words)
            {
                var relativeOffset = ssml.GetDisplayOffset(boundary.SsmlOffset) - chunk.DisplayOffset;
                if (!IsExactBoundary(chunk.Text, boundary.Text, relativeOffset, alignmentCursor, chunk.Text.Length))
                {
                    // Search the spoken copy, not silent reference labels that may repeat the same word.
                    relativeOffset = FindExactBoundary(chunk.Text, boundary.Text, alignmentCursor, chunk.Text.Length);
                }
                if (relativeOffset < 0)
                {
                    throw new DvarTorahAudioException("WordAlignmentFailed", "alignment");
                }
                words.Add(new(chunk.Section, boundary.Text, chunk.DisplayOffset + relativeOffset, boundary.Text.Length, offsetMs + boundary.AudioOffsetMs, boundary.DurationMs));
                alignmentCursor = relativeOffset + boundary.Text.Length;
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

    private static bool IsExactBoundary(string displayText, string word, int offset, int minimumOffset, int maximumOffset)
    {
        return offset >= minimumOffset && offset <= maximumOffset - word.Length && displayText.AsSpan(offset, word.Length).SequenceEqual(word);
    }

    private static int FindExactBoundary(string displayText, string word, int minimumOffset, int maximumOffset)
    {
        if (word.Length == 0 || minimumOffset < 0 || maximumOffset > displayText.Length || minimumOffset > maximumOffset - word.Length)
        {
            return -1;
        }

        return displayText.IndexOf(word, minimumOffset, maximumOffset - minimumOffset, StringComparison.Ordinal);
    }

    private static async Task<DvarTorahSpeechAudio> SynthesizeAsync(string ssml, DvarTorahAudioOptions options, TokenCredential credential, CancellationToken cancellationToken)
    {
        var accessToken = await credential.GetTokenAsync(new TokenRequestContext(["https://cognitiveservices.azure.com/.default"]), cancellationToken).ConfigureAwait(false);
        // Service-endpoint firewalls require the resource's custom host, not the regional Speech host.
        var authorizationToken = $"aad#{options.SpeechResourceId}#{accessToken.Token}";
        var endpoint = options.GetSpeechEndpoint();
        var configuration = endpoint is null ? SpeechConfig.FromAuthorizationToken(authorizationToken, options.SpeechRegion) : SpeechConfig.FromEndpoint(endpoint);
        configuration.AuthorizationToken = authorizationToken;
        configuration.SetSpeechSynthesisOutputFormat(SpeechSynthesisOutputFormat.Raw24Khz16BitMonoPcm);
        ConfigureWordBoundaryEvents(configuration.SetProperty);
        var boundaries = new ConcurrentQueue<DvarTorahSpeechWord>();
        var metadataCompleted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var synthesizer = new SpeechSynthesizer(configuration, audioConfig: null);
        synthesizer.WordBoundary += (_, boundary) =>
        {
            if (boundary.BoundaryType == SpeechSynthesisBoundaryType.Word)
            {
                boundaries.Enqueue(new(boundary.Text, boundary.TextOffset, boundary.AudioOffset / (double)TimeSpan.TicksPerMillisecond, boundary.Duration.TotalMilliseconds));
            }
        };
        synthesizer.SynthesisCompleted += (_, _) => metadataCompleted.TrySetResult();
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        deadline.CancelAfter(TimeSpan.FromMinutes(3));
        try
        {
            using var result = await synthesizer.SpeakSsmlAsync(ssml).WaitAsync(deadline.Token).ConfigureAwait(false);
            if (result.Reason != ResultReason.SynthesizingAudioCompleted)
            {
                var failure = SpeechSynthesisCancellationDetails.FromResult(result);
                // SDK diagnostics can include resource URLs and text; expose only the stable error code.
                throw new DvarTorahAudioException($"Speech{failure.ErrorCode}", "synthesis");
            }
            return await CompleteSynthesisAsync(result.AudioData, boundaries, metadataCompleted.Task, deadline.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            await synthesizer.StopSpeakingAsync().ConfigureAwait(false);
            throw;
        }
    }

    internal static async Task<DvarTorahSpeechAudio> CompleteSynthesisAsync(byte[] pcm, ConcurrentQueue<DvarTorahSpeechWord> boundaries, Task metadataCompleted, CancellationToken cancellationToken)
    {
        // The audio-result task and native event callbacks complete independently, especially for short chunks.
        await metadataCompleted.WaitAsync(cancellationToken).ConfigureAwait(false);
        return new DvarTorahSpeechAudio(pcm, boundaries.ToArray());
    }

    internal static void ConfigureWordBoundaryEvents(Action<PropertyId, string> setProperty)
    {
        setProperty(PropertyId.SpeechServiceResponse_RequestWordBoundary, "true");
        // This worker saves audio instead of playing it. Receive metadata immediately, before disposing the synthesizer.
        setProperty(PropertyId.SpeechServiceResponse_SynthesisEventsSyncToAudio, "false");
    }
}
