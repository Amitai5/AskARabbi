using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using AskARabbi.Api.Contracts.Conversations;
using AskARabbiLIB.Conversations;
using AskARabbiLIB.Grounding;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace AskARabbi.Api.Tests;

[TestClass]
public sealed class ConversationsControllerTests
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() },
    };

    [TestMethod]
    [TestCategory("Integration")]
    public async Task List_AnonymousRequest_ReturnsUnauthorized()
    {
        await using var application = new TestApplicationFactory();
        using var client = application.CreateNonRedirectingClient();

        using var response = await client.GetAsync("/api/conversations");

        Assert.AreEqual(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [TestMethod]
    [TestCategory("Integration")]
    public async Task CreateAndList_FirstMessage_ReturnsAiTitledNavigationSummary()
    {
        await using var application = new TestApplicationFactory();
        using var client = await application.CreateAuthenticatedClientAsync();

        using var createResponse = await client.PostAsJsonAsync("/api/conversations", new
        {
            messageId = Guid.Parse("22222222-2222-2222-2222-222222222222"),
            content = "How does Shabbat automation work?",
            enabledSourceKeys = new[] { "collection:Torah", "collection:Talmud" },
        });
        var created = await createResponse.Content.ReadFromJsonAsync<ConversationTurnResponse>(JsonOptions);
        using var listResponse = await client.GetAsync("/api/conversations");
        var summaries = await listResponse.Content.ReadFromJsonAsync<ConversationSummaryResponse[]>();

        Assert.AreEqual(HttpStatusCode.Created, createResponse.StatusCode);
        Assert.IsNotNull(created);
        Assert.AreEqual("answered", created.Status);
        Assert.AreEqual("Jewish Customs and Practice", created.Conversation.Title);
        Assert.HasCount(2, created.Conversation.Messages);
        Assert.HasCount(1, created.Conversation.Messages[1].Sources);
        var source = created.Conversation.Messages[1].Sources[0];
        Assert.AreEqual("The tested source text.", source.Quotations[0]);
        StringAssert.Contains(source.Context, "surrounding tested source context");
        Assert.AreEqual("https://www.sefaria.org/Test_1.1", source.SourceUrl);
        Assert.AreEqual("https://example.test/source", source.AttributionUrl);
        Assert.IsNotNull(summaries);
        Assert.HasCount(1, summaries);
        Assert.AreEqual(created.Conversation.Id, summaries[0].Id);
        Assert.AreEqual("Jewish Customs and Practice", summaries[0].Title);
        Assert.IsNotNull(application.GroundedAnswers.LastQuestion);
        Assert.IsTrue(application.GroundedAnswers.LastQuestion.ShouldGenerateConversationTitle);
    }

    [TestMethod]
    [TestCategory("Integration")]
    public async Task Create_MissingFirstMessage_RejectsRequestWithoutSavingConversation()
    {
        await using var application = new TestApplicationFactory();
        using var client = await application.CreateAuthenticatedClientAsync();

        using var createResponse = await client.PostAsJsonAsync("/api/conversations", new { enabledSourceKeys = new[] { "collection:Torah" } });
        using var listResponse = await client.GetAsync("/api/conversations");
        var summaries = await listResponse.Content.ReadFromJsonAsync<ConversationSummaryResponse[]>();

        Assert.AreEqual(HttpStatusCode.BadRequest, createResponse.StatusCode);
        Assert.IsNotNull(summaries);
        Assert.HasCount(0, summaries);
        Assert.AreEqual(0, application.GroundedAnswers.CallCount);
    }

    [TestMethod]
    [TestCategory("Regression")]
    public async Task Create_GroundingValidationFails_ReturnsSafeMessageWithoutInternalDetails()
    {
        // Arrange
        await using var application = new TestApplicationFactory();
        application.GroundedAnswers.NextResult = new GroundedAnswerResult
        {
            Status = GroundedAnswerStatus.ValidationFailed,
            ErrorMessage = "Direct quotation for evidence ID 'E5' does not match the source.",
            Trace = new GroundedAnswerTrace(TimeSpan.Zero, TimeSpan.FromMilliseconds(20), 6, 6, 4_648, null, GroundedValidationStatus.Failed, true, "test-response", "test-model"),
        };
        using var client = await application.CreateAuthenticatedClientAsync();

        // Act
        using var response = await client.PostAsJsonAsync("/api/conversations", new
        {
            messageId = Guid.Parse("44444444-4444-4444-4444-444444444444"),
            content = "Why do Jewish customs differ?",
            enabledSourceKeys = new[] { "collection:Torah" },
        });
        var result = await response.Content.ReadFromJsonAsync<ConversationTurnResponse>(JsonOptions);

        // Assert
        Assert.AreEqual(HttpStatusCode.Created, response.StatusCode);
        Assert.IsNotNull(result);
        Assert.AreEqual("validation_failed", result.Status);
        Assert.AreEqual("AskARabbi could not verify every quotation against its source, so it did not show the answer. Please try again.", result.Message);
        Assert.IsNotNull(result.Message);
        Assert.IsFalse(result.Message.Contains("E5", StringComparison.Ordinal));
        Assert.AreEqual(Conversation.DefaultTitle, result.Conversation.Title);
        Assert.HasCount(1, result.Conversation.Messages);
    }

    [TestMethod]
    [TestCategory("Integration")]
    public async Task AppendMessage_RepeatedClientMessageId_IsIdempotentAndReturnsCanonicalContext()
    {
        await using var application = new TestApplicationFactory();
        using var client = await application.CreateAuthenticatedClientAsync();
        var conversation = (await CreateConversationAsync(client)).Conversation;
        var messageId = Guid.Parse("22222222-2222-2222-2222-222222222222");
        var request = new { messageId, content = "  Why do customs differ?  " };

        using var firstResponse = await client.PostAsJsonAsync($"/api/conversations/{conversation.Id}/messages", request);
        using var secondResponse = await client.PostAsJsonAsync($"/api/conversations/{conversation.Id}/messages", request);
        var result = await secondResponse.Content.ReadFromJsonAsync<ConversationTurnResponse>(JsonOptions);

        Assert.AreEqual(HttpStatusCode.OK, firstResponse.StatusCode);
        Assert.AreEqual(HttpStatusCode.OK, secondResponse.StatusCode);
        Assert.IsNotNull(result);
        Assert.AreEqual("answered", result.Status);
        Assert.HasCount(4, result.Conversation.Messages);
        Assert.AreEqual("Why do customs differ?", result.Conversation.Messages[2].Content);
        Assert.AreEqual(ConversationMessageRole.User, result.Conversation.Messages[0].Role);
        Assert.AreEqual(ConversationMessageRole.Assistant, result.Conversation.Messages[1].Role);
        Assert.AreEqual("Jewish Customs and Practice", result.Conversation.Title);
        Assert.AreEqual(2, application.GroundedAnswers.CallCount);
        Assert.IsNotNull(application.GroundedAnswers.LastQuestion);
        Assert.IsFalse(application.GroundedAnswers.LastQuestion.ShouldGenerateConversationTitle);
    }

    [TestMethod]
    [TestCategory("Integration")]
    public async Task AppendMessage_CompactResponse_ReturnsOnlyCurrentTurnAndServerTiming()
    {
        // Arrange
        await using var application = new TestApplicationFactory();
        using var client = await application.CreateAuthenticatedClientAsync();
        var conversation = (await CreateConversationAsync(client)).Conversation;
        var messageId = Guid.Parse("33333333-3333-3333-3333-333333333333");

        // Act
        using var response = await client.PostAsJsonAsync($"/api/conversations/{conversation.Id}/messages?compact=true", new { messageId, content = "Why do customs differ?" });
        var result = await response.Content.ReadFromJsonAsync<ConversationTurnDeltaResponse>(JsonOptions);

        // Assert
        Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
        Assert.IsNotNull(result);
        Assert.AreEqual("answered", result.Status);
        Assert.AreEqual(conversation.Id, result.Conversation.Id);
        Assert.AreEqual("Jewish Customs and Practice", result.Conversation.Title);
        Assert.HasCount(2, result.Messages);
        Assert.AreEqual(messageId, result.Messages[0].Id);
        Assert.AreEqual(ConversationMessageRole.User, result.Messages[0].Role);
        Assert.AreEqual(ConversationMessageRole.Assistant, result.Messages[1].Role);
        var serverTiming = response.Headers.GetValues("Server-Timing").Single();
        StringAssert.Contains(serverTiming, "turn;dur=");
        StringAssert.Contains(serverTiming, "retrieval;dur=");
        StringAssert.Contains(serverTiming, "model;dur=");
    }

    [TestMethod]
    [TestCategory("Integration")]
    public async Task UpdateAndDelete_ExistingConversation_ChangesThenRemovesConversation()
    {
        await using var application = new TestApplicationFactory();
        using var client = await application.CreateAuthenticatedClientAsync();
        var conversation = (await CreateConversationAsync(client)).Conversation;

        using var renameResponse = await client.PutAsJsonAsync($"/api/conversations/{conversation.Id}/title", new { title = "New title" });
        using var sourcesResponse = await client.PutAsJsonAsync($"/api/conversations/{conversation.Id}/sources", new { enabledSourceKeys = new[] { "collection:Mishnah" } });
        using var getResponse = await client.GetAsync($"/api/conversations/{conversation.Id}");
        var updated = await getResponse.Content.ReadFromJsonAsync<ConversationResponse>(JsonOptions);
        using var deleteResponse = await client.DeleteAsync($"/api/conversations/{conversation.Id}");
        using var missingResponse = await client.GetAsync($"/api/conversations/{conversation.Id}");

        Assert.AreEqual(HttpStatusCode.NoContent, renameResponse.StatusCode);
        Assert.AreEqual(HttpStatusCode.NoContent, sourcesResponse.StatusCode);
        Assert.IsNotNull(updated);
        Assert.AreEqual("New title", updated.Title);
        CollectionAssert.AreEqual(new[] { "collection:Mishnah" }, updated.EnabledSourceKeys.ToArray());
        Assert.AreEqual(HttpStatusCode.NoContent, deleteResponse.StatusCode);
        Assert.AreEqual(HttpStatusCode.NotFound, missingResponse.StatusCode);
    }

    private static async Task<ConversationTurnResponse> CreateConversationAsync(HttpClient client)
    {
        using var response = await client.PostAsJsonAsync("/api/conversations", new
        {
            messageId = Guid.NewGuid(),
            content = "What should we study?",
            enabledSourceKeys = new[] { "collection:Torah" },
        });
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<ConversationTurnResponse>(JsonOptions))!;
    }
}
