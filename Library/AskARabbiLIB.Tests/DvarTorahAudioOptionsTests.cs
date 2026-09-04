using AskARabbiLIB.DvarTorah.Audio;
using Azure.Core;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace AskARabbiLIB.Tests;

[TestClass]
public sealed class DvarTorahAudioOptionsTests
{
    [TestMethod]
    [DataRow("")]
    [DataRow("http://test.blob.core.windows.net/")]
    [DataRow("https://attacker.example/")]
    [DataRow("https://test.blob.core.windows.net/path")]
    [DataRow("https://test.blob.core.windows.net/?sig=secret")]
    [DataRow("https://test.blob.core.windows.net/#fragment")]
    [DataRow("https://user:password@test.blob.core.windows.net/")]
    [DataRow("https://test.blob.core.windows.net:8443/")]
    [TestCategory("Unit")]
    public void ValidateStorage_UntrustedServiceUri_FailsFast(string uri)
    {
        Assert.ThrowsExactly<InvalidOperationException>(() => new DvarTorahAudioOptions { StorageServiceUri = uri }.ValidateStorage());
    }

    [TestMethod]
    [DataRow("ab")]
    [DataRow("HasUpperCase")]
    [DataRow("two--dashes")]
    [DataRow("../other")]
    [DataRow("-container")]
    [TestCategory("Unit")]
    public void ValidateStorage_InvalidContainer_FailsFast(string container)
    {
        Assert.ThrowsExactly<InvalidOperationException>(() => new DvarTorahAudioOptions { StorageServiceUri = "https://test.blob.core.windows.net/", ContainerName = container }.ValidateStorage());
    }

    [TestMethod]
    [DataRow("bad region", "valid", "valid", 30)]
    [DataRow("eastus2", "missing", "valid", 30)]
    [DataRow("eastus2", "valid", "<script>", 30)]
    [DataRow("eastus2", "valid", "valid", 4)]
    [DataRow("eastus2", "valid", "valid", 121)]
    [TestCategory("Unit")]
    public void ValidateGeneration_InvalidSetting_FailsFast(string region, string resource, string voice, int minutes)
    {
        var options = new DvarTorahAudioOptions
        {
            StorageServiceUri = "https://test.blob.core.windows.net/", SpeechRegion = region,
            SpeechResourceId = resource == "valid" ? DvarTorahAudioTestData.Options().SpeechResourceId : "",
            Voice = voice, LeaseDuration = TimeSpan.FromMinutes(minutes),
        };

        Assert.ThrowsExactly<InvalidOperationException>(options.ValidateGeneration);
    }

    [TestMethod]
    [TestCategory("Unit")]
    public void Defaults_ValidCloudSettings_UsesApprovedMaleMultilingualVoice()
    {
        var options = DvarTorahAudioTestData.Options();

        options.ValidateGeneration();

        Assert.AreEqual("en-US-AndrewMultilingualNeural", options.Voice);
        Assert.AreEqual("dvar-torah-audio", options.ContainerName);
        Assert.AreEqual("ffmpeg", options.FfmpegPath);
        Assert.IsNotNull(new AzureBlobDvarTorahAudioStorage(options, new NeverCalledCredential()));
    }

    [TestMethod]
    [TestCategory("Unit")]
    public void AudioException_CodesAreSafe_PreservesOriginalExceptionWithoutRawMessage()
    {
        var original = new IOException("Sensitive details");

        var failure = new DvarTorahAudioException("EncoderExitFailure", "encoding", original);

        Assert.AreSame(original, failure.InnerException);
        Assert.AreEqual("encoding", failure.Stage);
        Assert.AreEqual("EncoderExitFailure", failure.FailureCode);
        Assert.IsFalse(failure.Message.Contains("Sensitive details", StringComparison.Ordinal));
        Assert.ThrowsExactly<ArgumentException>(() => new DvarTorahAudioException("secret?token=abc", "encoding"));
        Assert.ThrowsExactly<ArgumentException>(() => new DvarTorahAudioException("Code", "<script>"));
        Assert.ThrowsExactly<ArgumentException>(() => new DvarTorahAudioException(" ", "encoding"));
        Assert.ThrowsExactly<ArgumentException>(() => new DvarTorahAudioException("Code", " "));
    }

    [TestMethod]
    [TestCategory("Unit")]
    public async Task Encoder_InvalidOrCanceledInput_FailsBeforeStartingNativeProcess()
    {
        var encoder = new FfmpegDvarTorahMp3Encoder(DvarTorahAudioTestData.Options());
        using var empty = new MemoryStream();
        using var odd = new MemoryStream(new byte[3]);
        using var valid = new MemoryStream(new byte[4]);
        using var tooLarge = new OversizedPcmStream();

        await Assert.ThrowsExactlyAsync<ArgumentNullException>(() => encoder.EncodeAsync(null!));
        await Assert.ThrowsExactlyAsync<ArgumentException>(() => encoder.EncodeAsync(empty));
        await Assert.ThrowsExactlyAsync<ArgumentException>(() => encoder.EncodeAsync(odd));
        await Assert.ThrowsExactlyAsync<ArgumentException>(() => encoder.EncodeAsync(tooLarge));
        await Assert.ThrowsExactlyAsync<OperationCanceledException>(() => encoder.EncodeAsync(valid, new CancellationToken(true)));
        Assert.ThrowsExactly<ArgumentNullException>(() => new FfmpegDvarTorahMp3Encoder(null!));
        Assert.ThrowsExactly<ArgumentException>(() => new FfmpegDvarTorahMp3Encoder(new DvarTorahAudioOptions { FfmpegPath = " " }));
    }

    private sealed class OversizedPcmStream : MemoryStream
    {
        public override long Length => 180 * 1024 * 1024 + 2;
    }

    private sealed class NeverCalledCredential : TokenCredential
    {
        public override AccessToken GetToken(TokenRequestContext requestContext, CancellationToken cancellationToken) => throw new AssertFailedException("No real credential call is allowed.");
        public override ValueTask<AccessToken> GetTokenAsync(TokenRequestContext requestContext, CancellationToken cancellationToken) => throw new AssertFailedException("No real credential call is allowed.");
    }
}
