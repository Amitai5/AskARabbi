using AskARabbiLIB.DvarTorah.Audio;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Text.RegularExpressions;
using System.Xml.Linq;

namespace AskARabbiLIB.Tests;

[TestClass]
public sealed class AzureSpeechDvarTorahNarratorTests
{
    [TestMethod]
    [TestCategory("Unit")]
    public async Task GenerateAsync_MultipleChunks_ConcatenatesSamplesAndOffsetsWithoutTruncation()
    {
        var article = DvarTorahAudioTestData.Article(string.Concat(Enumerable.Repeat("Hello שַׁבָּת שָׁלוֹם [T1]. ", 160)));
        var encoder = new RecordingEncoder();
        var calls = new List<string>();
        Task<DvarTorahSpeechAudio> Synthesize(string ssml, CancellationToken cancellationToken)
        {
            calls.Add(ssml);
            return Task.FromResult(SpeechResult(ssml));
        }
        var narrator = new AzureSpeechDvarTorahNarrator(DvarTorahAudioTestData.Options(), encoder, Synthesize);

        var result = await narrator.GenerateAsync(article, DvarTorahAudioText.GetVersion(article, DvarTorahAudioTestData.Voice));

        Assert.IsGreaterThan(2, calls.Count);
        Assert.IsTrue(calls.All(ssml => !ssml.Contains("[T1]", StringComparison.Ordinal)));
        Assert.AreEqual(calls.Count * 48_000, encoder.PcmLength);
        Assert.AreEqual(calls.Count * 1000d, result.Timings.DurationMs);
        Assert.AreEqual(article.Body, result.Timings.Body);
        Assert.HasCount(1 + 160 * 3, result.Timings.Words);
        Assert.AreEqual("title", result.Timings.Words[0].Section);
        Assert.AreEqual("body", result.Timings.Words[1].Section);
        Assert.AreEqual(1000d, result.Timings.Words[1].AudioOffsetMs);
        Assert.AreEqual(article.Body.LastIndexOf("שָׁלוֹם", StringComparison.Ordinal), result.Timings.Words[^1].TextOffset);
        DvarTorahAudioValidation.ValidateTimings(result.Timings);
    }

    [TestMethod]
    [DataRow(0)]
    [DataRow(1)]
    [TestCategory("Unit")]
    public async Task GenerateAsync_EmptyOrOddPcm_FailsBeforeEncoding(int length)
    {
        var encoder = new RecordingEncoder();
        var narrator = new AzureSpeechDvarTorahNarrator(DvarTorahAudioTestData.Options(), encoder, (_, _) => Task.FromResult(new DvarTorahSpeechAudio(new byte[length], [])));

        var failure = await Assert.ThrowsExactlyAsync<DvarTorahAudioException>(() => narrator.GenerateAsync(DvarTorahAudioTestData.Article(), DvarTorahAudioTestData.Timings().Version));

        Assert.AreEqual("AudioDataInvalid", failure.FailureCode);
        Assert.AreEqual(0, encoder.Calls);
    }

    [TestMethod]
    [TestCategory("Unit")]
    public async Task GenerateAsync_MissingBoundary_FailsBeforeEncoding()
    {
        var encoder = new RecordingEncoder();
        var narrator = new AzureSpeechDvarTorahNarrator(DvarTorahAudioTestData.Options(), encoder, (_, _) => Task.FromResult(new DvarTorahSpeechAudio(new byte[48_000], [])));

        var failure = await Assert.ThrowsExactlyAsync<DvarTorahAudioException>(() => narrator.GenerateAsync(DvarTorahAudioTestData.Article(), DvarTorahAudioTestData.Timings().Version));

        Assert.AreEqual("WordBoundariesMissing", failure.FailureCode);
        Assert.AreEqual(0, encoder.Calls);
    }

    [TestMethod]
    [DataRow(0u, "Title")]
    [DataRow(uint.MaxValue, "Title")]
    [DataRow(1u, "Wrong")]
    [TestCategory("Unit")]
    public async Task GenerateAsync_InvalidBoundaryOffset_RejectsWrongWordHighlight(uint offset, string text)
    {
        var encoder = new RecordingEncoder();
        var narrator = new AzureSpeechDvarTorahNarrator(DvarTorahAudioTestData.Options(), encoder, (_, _) => Task.FromResult(new DvarTorahSpeechAudio(new byte[48_000], [new(text, offset, 0, 500)])));

        var failure = await Assert.ThrowsExactlyAsync<DvarTorahAudioException>(() => narrator.GenerateAsync(DvarTorahAudioTestData.Article(), DvarTorahAudioTestData.Timings().Version));

        Assert.AreEqual("WordAlignmentFailed", failure.FailureCode);
        Assert.AreEqual("alignment", failure.Stage);
        Assert.AreEqual(0, encoder.Calls);
    }

    [TestMethod]
    [TestCategory("Unit")]
    public async Task GenerateAsync_InvalidArticleOrVersion_RejectsBeforeCallingSpeech()
    {
        var narrator = new AzureSpeechDvarTorahNarrator(DvarTorahAudioTestData.Options(), new RecordingEncoder(), (_, _) => throw new AssertFailedException("Speech must not be called."));

        await Assert.ThrowsExactlyAsync<ArgumentNullException>(() => narrator.GenerateAsync(null!, "version"));
        await Assert.ThrowsExactlyAsync<ArgumentException>(() => narrator.GenerateAsync(DvarTorahAudioTestData.Article(), "wrong"));
        await Assert.ThrowsExactlyAsync<OperationCanceledException>(() => narrator.GenerateAsync(DvarTorahAudioTestData.Article(), DvarTorahAudioTestData.Timings().Version, new CancellationToken(true)));
    }

    [TestMethod]
    [TestCategory("Unit")]
    public async Task GenerateAsync_EncoderFailure_PropagatesWithoutReturningPartialRecording()
    {
        var encoder = new RecordingEncoder { Failure = new DvarTorahAudioException("EncoderExitFailure", "encoding") };
        var narrator = new AzureSpeechDvarTorahNarrator(DvarTorahAudioTestData.Options(), encoder, (ssml, _) => Task.FromResult(SpeechResult(ssml)));

        var failure = await Assert.ThrowsExactlyAsync<DvarTorahAudioException>(() => narrator.GenerateAsync(DvarTorahAudioTestData.Article(), DvarTorahAudioTestData.Timings().Version));

        Assert.AreSame(encoder.Failure, failure);
        Assert.AreEqual(1, encoder.Calls);
    }

    [TestMethod]
    [TestCategory("Unit")]
    public void Constructor_MissingDependencies_FailsFastWithoutNativeCalls()
    {
        var options = DvarTorahAudioTestData.Options();
        var encoder = new RecordingEncoder();
        Assert.ThrowsExactly<ArgumentNullException>(() => new AzureSpeechDvarTorahNarrator(null!, encoder, (_, _) => throw new NotSupportedException()));
        Assert.ThrowsExactly<ArgumentNullException>(() => new AzureSpeechDvarTorahNarrator(options, null!, (_, _) => throw new NotSupportedException()));
        Assert.ThrowsExactly<ArgumentNullException>(() => new AzureSpeechDvarTorahNarrator(options, encoder, null!));
        Assert.ThrowsExactly<ArgumentNullException>(() => new AzureSpeechDvarTorahNarrator(options, null!, encoder));
    }

    private static DvarTorahSpeechAudio SpeechResult(string ssml)
    {
        var text = XDocument.Parse(ssml).Root?.Value ?? throw new AssertFailedException("Invalid SSML.");
        var matches = Regex.Matches(text, @"[\p{L}\p{M}]+", RegexOptions.CultureInvariant);
        var cursor = ssml.IndexOf(">", ssml.IndexOf("<lang", StringComparison.Ordinal), StringComparison.Ordinal) + 1;
        var words = new List<DvarTorahSpeechWord>();
        for (var index = 0; index < matches.Count; index++)
        {
            var word = matches[index].Value;
            var position = ssml.IndexOf(word, cursor, StringComparison.Ordinal);
            Assert.IsGreaterThanOrEqualTo(0, position);
            words.Add(new(word, (uint)position, index * 1000d / matches.Count, 500d / matches.Count));
            cursor = position + word.Length;
        }
        return new DvarTorahSpeechAudio(new byte[48_000], words);
    }

    private sealed class RecordingEncoder : IDvarTorahMp3Encoder
    {
        public int Calls { get; private set; }
        public long PcmLength { get; private set; }
        public Exception? Failure { get; init; }
        public Task<ReadOnlyMemory<byte>> EncodeAsync(Stream pcm, CancellationToken cancellationToken = default)
        {
            Calls++;
            PcmLength = pcm.Length;
            return Failure is null ? Task.FromResult<ReadOnlyMemory<byte>>(new byte[] { 0xff, 0xfb, 0, 0 }) : Task.FromException<ReadOnlyMemory<byte>>(Failure);
        }
    }
}
