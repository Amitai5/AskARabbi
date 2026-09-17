using AskARabbi.Api.Voice;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace AskARabbi.Api.Tests;

[TestClass]
public sealed class VoiceOptionsTests
{
    private const string ResourceId = "/subscriptions/11111111-1111-1111-1111-111111111111/resourceGroups/test/providers/Microsoft.CognitiveServices/accounts/speech";

    [TestMethod]
    public void Validate_Disabled_DoesNotRequireResource()
    {
        new VoiceOptions().Validate();
    }

    [TestMethod]
    [DataRow(null)]
    [DataRow("https://speech.cognitiveservices.azure.com/")]
    public void Validate_EnabledValidResource_AcceptsRegionalAndCustomHosts(string? endpoint)
    {
        new VoiceOptions { Enabled = true, SpeechResourceId = ResourceId, SpeechServiceUri = endpoint }.Validate();
    }

    [TestMethod]
    [DataRow("http://speech.cognitiveservices.azure.com/")]
    [DataRow("https://speech.cognitiveservices.azure.com.evil.test/")]
    [DataRow("https://user:secret@speech.cognitiveservices.azure.com/")]
    [DataRow("https://speech.cognitiveservices.azure.com/path")]
    [DataRow("https://speech.cognitiveservices.azure.com/?key=secret")]
    [DataRow("https://speech.cognitiveservices.azure.com:444/")]
    public void Validate_UnsafeEndpoint_RejectsConfiguration(string endpoint)
    {
        Assert.ThrowsExactly<InvalidOperationException>(() => new VoiceOptions { Enabled = true, SpeechResourceId = ResourceId, SpeechServiceUri = endpoint }.Validate());
    }

    [TestMethod]
    public void Validate_MissingResource_RejectsEnabledFeature()
    {
        Assert.ThrowsExactly<InvalidOperationException>(() => new VoiceOptions { Enabled = true }.Validate());
    }
}
