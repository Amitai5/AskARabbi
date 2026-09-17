using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Xml.Linq;
using AskARabbi.Api.Voice;
using Azure.Core;
using Azure.Identity;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace AskARabbi.Api.Tests;

[TestClass]
public sealed class AzureVoiceServiceTests
{
    private const string ResourceId = "/subscriptions/11111111-1111-1111-1111-111111111111/resourceGroups/test/providers/Microsoft.CognitiveServices/accounts/speech";
    private static readonly VoiceOptions Options = new() { Enabled = true, SpeechResourceId = ResourceId, SpeechServiceUri = "https://speech.cognitiveservices.azure.com/" };

    [TestMethod]
    public async Task Transcribe_PcmInput_UsesManagedIdentityAndValidWaveEnvelope()
    {
        var credential = new FakeCredential();
        using var handler = new RecordingHandler(_ => new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("{\"RecognitionStatus\":\"Success\",\"DisplayText\":\"מהי שבת?\"}") });
        using var client = new HttpClient(handler);
        var service = new AzureVoiceService(Options, credential, client);
        var pcm = new byte[3200];
        pcm[0] = 123;

        var result = await service.TranscribeAsync(pcm, "he-IL", CancellationToken.None);

        Assert.AreEqual("מהי שבת?", result);
        Assert.AreEqual("https://speech.cognitiveservices.azure.com/stt/speech/recognition/conversation/cognitiveservices/v1?language=he-IL&format=simple", handler.Uri?.AbsoluteUri);
        Assert.AreEqual($"Bearer aad#{ResourceId}#fake-token", handler.Authorization);
        CollectionAssert.AreEqual(new[] { "https://cognitiveservices.azure.com/.default" }, credential.Scopes);
        Assert.IsNotNull(handler.Body);
        Assert.HasCount(3244, handler.Body);
        Assert.AreEqual("RIFF", Encoding.ASCII.GetString(handler.Body, 0, 4));
        Assert.AreEqual("WAVEfmt ", Encoding.ASCII.GetString(handler.Body, 8, 8));
        Assert.AreEqual(16_000, BitConverter.ToInt32(handler.Body, 24));
        Assert.AreEqual((short)1, BitConverter.ToInt16(handler.Body, 22));
        Assert.AreEqual((short)16, BitConverter.ToInt16(handler.Body, 34));
        Assert.AreEqual(pcm.Length, BitConverter.ToInt32(handler.Body, 40));
        Assert.AreEqual((byte)123, handler.Body[44]);
        StringAssert.Contains(handler.ContentType ?? "", "audio/wav");
    }

    [TestMethod]
    [DataRow("NoMatch")]
    [DataRow("InitialSilenceTimeout")]
    [DataRow("BabbleTimeout")]
    public async Task Transcribe_ExpectedNoSpeech_ReturnsEmptyDraft(string status)
    {
        using var handler = new RecordingHandler(_ => new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent($"{{\"RecognitionStatus\":\"{status}\"}}") });
        using var client = new HttpClient(handler);

        var result = await new AzureVoiceService(Options, new FakeCredential(), client).TranscribeAsync(new byte[3200], "en-US", CancellationToken.None);

        Assert.AreEqual(string.Empty, result);
    }

    [TestMethod]
    [DataRow("not-json")]
    [DataRow("{}")]
    [DataRow("{\"RecognitionStatus\":\"Error\"}")]
    [DataRow("{\"RecognitionStatus\":\"Success\",\"DisplayText\":123}")]
    public async Task Transcribe_InvalidProviderPayload_ReturnsSafeFailure(string body)
    {
        using var handler = new RecordingHandler(_ => new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(body) });
        using var client = new HttpClient(handler);

        await Assert.ThrowsExactlyAsync<VoiceUnavailableException>(() => new AzureVoiceService(Options, new FakeCredential(), client).TranscribeAsync(new byte[3200], "en-US", CancellationToken.None));
    }

    [TestMethod]
    public async Task Synthesize_QuotationContainingMarkup_EscapesTextAndRequestsMp3()
    {
        using var handler = new RecordingHandler(_ => new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent([1, 2, 3]) });
        using var client = new HttpClient(handler);
        var text = "A quote: <voice name='evil'>שלום & learning</voice> [1]";

        var result = await new AzureVoiceService(Options, new FakeCredential(), client).SynthesizeAsync(text, CancellationToken.None);

        Assert.AreEqual("https://speech.cognitiveservices.azure.com/tts/cognitiveservices/v1", handler.Uri?.AbsoluteUri);
        Assert.AreEqual("audio-24khz-48kbitrate-mono-mp3", handler.OutputFormat);
        Assert.IsNotNull(handler.Body);
        var xml = XElement.Parse(Encoding.UTF8.GetString(handler.Body));
        var voice = xml.Elements().Single();
        Assert.AreEqual(text, voice.Value);
        Assert.IsFalse(voice.HasElements);
        Assert.AreEqual(Options.Voice, voice.Attribute("name")?.Value);
        Assert.AreEqual("application/ssml+xml", MediaTypeHeaderValue.Parse(handler.ContentType ?? "").MediaType);
        CollectionAssert.AreEqual(new byte[] { 1, 2, 3 }, result);
    }

    [TestMethod]
    public async Task Synthesize_RegionalConfiguration_UsesConfiguredRegion()
    {
        using var handler = new RecordingHandler(_ => new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent([1]) });
        using var client = new HttpClient(handler);
        var options = new VoiceOptions { Enabled = true, SpeechResourceId = ResourceId, SpeechRegion = "eastus2" };

        await new AzureVoiceService(options, new FakeCredential(), client).SynthesizeAsync("Answer [1]", CancellationToken.None);

        Assert.AreEqual("eastus2.tts.speech.microsoft.com", handler.Uri?.Host);
    }

    [TestMethod]
    [DataRow(401)]
    [DataRow(429)]
    [DataRow(500)]
    [DataRow(302)]
    public async Task Synthesize_ProviderFailure_DoesNotExposeProviderBody(int status)
    {
        using var handler = new RecordingHandler(_ => new HttpResponseMessage((HttpStatusCode)status) { Content = new StringContent("private diagnostics with secret-token") });
        using var client = new HttpClient(handler);

        var error = await Assert.ThrowsExactlyAsync<VoiceUnavailableException>(() => new AzureVoiceService(Options, new FakeCredential(), client).SynthesizeAsync("Private answer", CancellationToken.None));

        Assert.IsFalse(error.Message.Contains("secret-token", StringComparison.Ordinal));
        Assert.IsNull(error.InnerException);
        Assert.AreEqual(1, handler.Calls);
    }

    [TestMethod]
    [DataRow(0)]
    [DataRow(4000001)]
    public async Task Synthesize_EmptyOrOversizedAudio_RejectsProviderResponse(int bytes)
    {
        using var handler = new RecordingHandler(_ => new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent(new byte[bytes]) });
        using var client = new HttpClient(handler);

        await Assert.ThrowsExactlyAsync<VoiceUnavailableException>(() => new AzureVoiceService(Options, new FakeCredential(), client).SynthesizeAsync("Answer", CancellationToken.None));
    }

    [TestMethod]
    public async Task Synthesize_CallerCancellation_PropagatesWithoutProviderRequest()
    {
        using var handler = new RecordingHandler(_ => new HttpResponseMessage(HttpStatusCode.OK));
        using var client = new HttpClient(handler);
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        await Assert.ThrowsExactlyAsync<OperationCanceledException>(() => new AzureVoiceService(Options, new FakeCredential(), client).SynthesizeAsync("Answer", cancellation.Token));

        Assert.AreEqual(0, handler.Calls);
    }

    [TestMethod]
    public async Task Synthesize_IdentityFailure_DoesNotExposeCredentialDiagnostics()
    {
        using var handler = new RecordingHandler(_ => new HttpResponseMessage(HttpStatusCode.OK));
        using var client = new HttpClient(handler);
        var credential = new FakeCredential { Fail = true };

        var error = await Assert.ThrowsExactlyAsync<VoiceUnavailableException>(() => new AzureVoiceService(Options, credential, client).SynthesizeAsync("Answer", CancellationToken.None));

        Assert.AreEqual(0, handler.Calls);
        Assert.IsFalse(error.Message.Contains("private identity diagnostics", StringComparison.Ordinal));
    }

    private sealed class FakeCredential : TokenCredential
    {
        internal string[] Scopes { get; private set; } = [];
        internal bool Fail { get; init; }

        public override AccessToken GetToken(TokenRequestContext requestContext, CancellationToken cancellationToken) => throw new NotSupportedException();

        public override ValueTask<AccessToken> GetTokenAsync(TokenRequestContext requestContext, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Scopes = requestContext.Scopes;
            return Fail ? ValueTask.FromException<AccessToken>(new AuthenticationFailedException("private identity diagnostics")) : ValueTask.FromResult(new AccessToken("fake-token", DateTimeOffset.MaxValue));
        }
    }

    private sealed class RecordingHandler(Func<HttpRequestMessage, HttpResponseMessage> respond) : HttpMessageHandler
    {
        internal Uri? Uri { get; private set; }
        internal string? Authorization { get; private set; }
        internal string? ContentType { get; private set; }
        internal string? OutputFormat { get; private set; }
        internal byte[]? Body { get; private set; }
        internal int Calls { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Calls++;
            Uri = request.RequestUri;
            Authorization = request.Headers.Authorization?.ToString();
            OutputFormat = request.Headers.TryGetValues("X-Microsoft-OutputFormat", out var values) ? values.Single() : null;
            ContentType = request.Content?.Headers.ContentType?.ToString();
            Body = request.Content is null ? null : await request.Content.ReadAsByteArrayAsync(cancellationToken);
            return respond(request);
        }
    }
}
