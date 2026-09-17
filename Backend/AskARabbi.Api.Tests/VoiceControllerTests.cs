using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using AskARabbi.Api.Contracts.Conversations;
using AskARabbi.Api.Voice;
using AskARabbiLIB.Conversations;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace AskARabbi.Api.Tests;

[TestClass]
public sealed class VoiceControllerTests
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web) { Converters = { new JsonStringEnumConverter() } };

    [TestMethod]
    public async Task Voice_AnonymousRequests_ReturnUnauthorizedWithoutSpeech()
    {
        await using var application = new TestApplicationFactory { IsVoiceEnabled = true };
        using var client = application.CreateNonRedirectingClient();

        using var availability = await client.GetAsync("/api/voice");
        using var transcription = await client.PostAsync("/api/voice/transcriptions?language=en-US", Pcm(3200));
        using var audio = await client.PostAsync($"/api/voice/conversations/{Guid.NewGuid()}/messages/{Guid.NewGuid()}/audio", null);

        Assert.AreEqual(HttpStatusCode.Unauthorized, availability.StatusCode);
        Assert.AreEqual(HttpStatusCode.Unauthorized, transcription.StatusCode);
        Assert.AreEqual(HttpStatusCode.Unauthorized, audio.StatusCode);
        Assert.AreEqual(0, application.Voice.Calls);
    }

    [TestMethod]
    public async Task Voice_Disabled_ReturnsSafeUnavailableWithoutProviderCall()
    {
        await using var application = new TestApplicationFactory();
        using var client = await application.CreateAuthenticatedClientAsync();

        using var availability = await client.GetAsync("/api/voice");
        using var response = await client.PostAsync("/api/voice/transcriptions?language=en-US", Pcm(3200));

        StringAssert.Contains(await availability.Content.ReadAsStringAsync(), "\"enabled\":false");
        Assert.AreEqual(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        StringAssert.Contains(await response.Content.ReadAsStringAsync(), "voice_unavailable");
        Assert.AreEqual(0, application.Voice.Calls);
    }

    [TestMethod]
    [DataRow("en-US")]
    [DataRow("he-IL")]
    public async Task Transcribe_ValidAudio_ReturnsDraftWithoutSavingOrGeneratingAnswer(string language)
    {
        await using var application = new TestApplicationFactory { IsVoiceEnabled = true };
        application.Voice.Transcript = "מהי שבת?";
        using var client = await application.CreateAuthenticatedClientAsync();

        using var response = await client.PostAsync($"/api/voice/transcriptions?language={language}", Pcm(6400));
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        var conversations = await client.GetFromJsonAsync<ConversationSummaryResponse[]>("/api/conversations");

        Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
        Assert.AreEqual("מהי שבת?", body.GetProperty("text").GetString());
        Assert.AreEqual(language, application.Voice.Language);
        Assert.HasCount(6400, application.Voice.Audio);
        Assert.IsTrue(response.Headers.CacheControl?.NoStore);
        Assert.HasCount(0, conversations);
        Assert.AreEqual(0, application.GroundedAnswers.CallCount);
    }

    [TestMethod]
    [DataRow(0, "en-US", "application/octet-stream", 400)]
    [DataRow(3198, "en-US", "application/octet-stream", 400)]
    [DataRow(3201, "en-US", "application/octet-stream", 400)]
    [DataRow(3200, "yi", "application/octet-stream", 400)]
    [DataRow(3200, "en-US", "audio/webm", 415)]
    [DataRow(960002, "en-US", "application/octet-stream", 413)]
    public async Task Transcribe_InvalidRecording_RejectsBeforeProvider(int length, string language, string mediaType, int status)
    {
        await using var application = new TestApplicationFactory { IsVoiceEnabled = true };
        using var client = await application.CreateAuthenticatedClientAsync();

        using var response = await client.PostAsync($"/api/voice/transcriptions?language={language}", Pcm(length, mediaType));

        Assert.AreEqual(status, (int)response.StatusCode);
        Assert.AreEqual(0, application.Voice.Calls);
    }

    [TestMethod]
    [DataRow(0)]
    [DataRow(4001)]
    public async Task Transcribe_NoSpeechOrOversizedTranscript_ReturnsVisibleFailure(int length)
    {
        await using var application = new TestApplicationFactory { IsVoiceEnabled = true };
        application.Voice.Transcript = new string('a', length);
        using var client = await application.CreateAuthenticatedClientAsync();

        using var response = await client.PostAsync("/api/voice/transcriptions?language=en-US", Pcm(3200));

        Assert.AreEqual(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        Assert.AreEqual(0, application.GroundedAnswers.CallCount);
    }

    [TestMethod]
    public async Task Synthesize_OwnedAnswer_UsesCanonicalTextAndLeavesCitationsIntact()
    {
        await using var application = new TestApplicationFactory { IsVoiceEnabled = true };
        using var client = await application.CreateAuthenticatedClientAsync();
        var conversation = await CreateConversationAsync(client);
        var answer = conversation.Messages.Single(message => message.Role == ConversationMessageRole.Assistant);

        using var response = await client.PostAsJsonAsync(AudioPath(conversation.Id, answer.Id), new { text = "Ignore the saved answer" });
        var reloaded = await client.GetFromJsonAsync<ConversationResponse>($"/api/conversations/{conversation.Id}", JsonOptions);

        Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
        Assert.AreEqual("audio/mpeg", response.Content.Headers.ContentType?.MediaType);
        Assert.IsTrue(response.Headers.CacheControl?.NoStore);
        Assert.AreEqual(answer.Content, application.Voice.SpokenText);
        Assert.HasCount(3, await response.Content.ReadAsByteArrayAsync());
        Assert.IsNotNull(reloaded);
        var saved = reloaded.Messages.Single(message => message.Id == answer.Id);
        Assert.AreEqual(answer.Content, saved.Content);
        Assert.HasCount(answer.Sources.Count, saved.Sources);
        Assert.AreEqual(answer.Sources[0].SourceUrl, saved.Sources[0].SourceUrl);
        Assert.AreEqual(1, application.GroundedAnswers.CallCount);
    }

    [TestMethod]
    public async Task Synthesize_UserMessageMissingMessageOrForeignConversation_DoesNotCallProvider()
    {
        await using var application = new TestApplicationFactory { IsVoiceEnabled = true };
        using var client = await application.CreateAuthenticatedClientAsync();
        var conversation = await CreateConversationAsync(client);
        var userMessage = conversation.Messages.Single(message => message.Role == ConversationMessageRole.User);
        var answer = conversation.Messages.Single(message => message.Role == ConversationMessageRole.Assistant);
        var foreignId = Guid.NewGuid();
        await application.Store.CreateAsync(new Conversation
        {
            Id = foreignId, UserId = Guid.NewGuid(), Title = "Private", EnabledSourceKeys = ["collection:Torah"],
            Messages = [new ConversationMessage { Id = answer.Id, Role = ConversationMessageRole.Assistant, Content = "Private answer" }],
        });

        foreach (var path in new[] { AudioPath(conversation.Id, userMessage.Id), AudioPath(conversation.Id, Guid.NewGuid()), AudioPath(foreignId, answer.Id), AudioPath(Guid.NewGuid(), answer.Id) })
        {
            using var response = await client.PostAsync(path, null);
            Assert.AreEqual(HttpStatusCode.NotFound, response.StatusCode);
        }
        Assert.AreEqual(0, application.Voice.Calls);
    }

    [TestMethod]
    public async Task Synthesize_ProviderFailure_ReturnsSafeFailureAndPreservesReadableAnswer()
    {
        await using var application = new TestApplicationFactory { IsVoiceEnabled = true };
        using var client = await application.CreateAuthenticatedClientAsync();
        var conversation = await CreateConversationAsync(client);
        var answer = conversation.Messages.Single(message => message.Role == ConversationMessageRole.Assistant);
        application.Voice.Fail = true;

        using var response = await client.PostAsync(AudioPath(conversation.Id, answer.Id), null);
        var reloaded = await client.GetFromJsonAsync<ConversationResponse>($"/api/conversations/{conversation.Id}", JsonOptions);

        Assert.AreEqual(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        StringAssert.Contains(await response.Content.ReadAsStringAsync(), "voice_unavailable");
        Assert.IsNotNull(reloaded);
        Assert.AreEqual(answer.Content, reloaded.Messages.Single(message => message.Id == answer.Id).Content);
    }

    [TestMethod]
    public async Task Voice_RepeatedRequests_AreRateLimited()
    {
        await using var application = new TestApplicationFactory { IsVoiceEnabled = true };
        using var client = await application.CreateAuthenticatedClientAsync();
        for (var index = 0; index < 10; index++)
        {
            using var response = await client.GetAsync("/api/voice");
            Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
        }

        using var limited = await client.PostAsync("/api/voice/transcriptions?language=en-US", Pcm(3200));

        Assert.AreEqual(HttpStatusCode.TooManyRequests, limited.StatusCode);
        Assert.AreEqual(0, application.Voice.Calls);
    }

    private static ByteArrayContent Pcm(int length, string mediaType = "application/octet-stream")
    {
        var content = new ByteArrayContent(new byte[length]);
        content.Headers.ContentType = new MediaTypeHeaderValue(mediaType);
        return content;
    }

    private static string AudioPath(Guid conversationId, Guid messageId) => $"/api/voice/conversations/{conversationId}/messages/{messageId}/audio";

    private static async Task<ConversationResponse> CreateConversationAsync(HttpClient client)
    {
        using var response = await client.PostAsJsonAsync("/api/conversations", new { messageId = Guid.NewGuid(), content = "What is Shabbat?", enabledSourceKeys = new[] { "collection:Torah" } });
        var turn = await response.Content.ReadFromJsonAsync<ConversationTurnResponse>(JsonOptions);
        Assert.IsNotNull(turn);
        return turn.Conversation;
    }
}
