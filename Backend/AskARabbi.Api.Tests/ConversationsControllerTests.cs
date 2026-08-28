using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using AskARabbi.Api.Contracts.Conversations;
using AskARabbiLIB.Conversations;
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
    public async Task CreateAndList_AuthenticatedUser_ReturnsNavigationSummary()
    {
        await using var application = new TestApplicationFactory();
        using var client = await application.CreateAuthenticatedClientAsync();

        using var createResponse = await client.PostAsJsonAsync("/api/conversations", new
        {
            title = "Shabbat automation",
            enabledSourceKeys = new[] { "collection:Torah", "collection:Talmud" },
        });
        var created = await createResponse.Content.ReadFromJsonAsync<ConversationResponse>();
        using var listResponse = await client.GetAsync("/api/conversations");
        var summaries = await listResponse.Content.ReadFromJsonAsync<ConversationSummaryResponse[]>();

        Assert.AreEqual(HttpStatusCode.Created, createResponse.StatusCode);
        Assert.IsNotNull(created);
        Assert.AreEqual("Shabbat automation", created.Title);
        Assert.IsNotNull(summaries);
        Assert.HasCount(1, summaries);
        Assert.AreEqual(created.Id, summaries[0].Id);
    }

    [TestMethod]
    [TestCategory("Integration")]
    public async Task AppendMessage_RepeatedClientMessageId_IsIdempotentAndReturnsCanonicalContext()
    {
        await using var application = new TestApplicationFactory();
        using var client = await application.CreateAuthenticatedClientAsync();
        var conversation = await CreateConversationAsync(client);
        var messageId = Guid.Parse("22222222-2222-2222-2222-222222222222");
        var request = new { messageId, content = "  Why do customs differ?  " };

        using var firstResponse = await client.PostAsJsonAsync($"/api/conversations/{conversation.Id}/messages", request);
        using var secondResponse = await client.PostAsJsonAsync($"/api/conversations/{conversation.Id}/messages", request);
        var result = await secondResponse.Content.ReadFromJsonAsync<ConversationTurnResponse>(JsonOptions);

        Assert.AreEqual(HttpStatusCode.OK, firstResponse.StatusCode);
        Assert.AreEqual(HttpStatusCode.OK, secondResponse.StatusCode);
        Assert.IsNotNull(result);
        Assert.AreEqual("stored", result.Status);
        Assert.HasCount(1, result.Conversation.Messages);
        Assert.AreEqual("Why do customs differ?", result.Conversation.Messages[0].Content);
        Assert.AreEqual(ConversationMessageRole.User, result.Conversation.Messages[0].Role);
    }

    [TestMethod]
    [TestCategory("Integration")]
    public async Task UpdateAndDelete_ExistingConversation_ChangesThenRemovesConversation()
    {
        await using var application = new TestApplicationFactory();
        using var client = await application.CreateAuthenticatedClientAsync();
        var conversation = await CreateConversationAsync(client);

        using var renameResponse = await client.PutAsJsonAsync($"/api/conversations/{conversation.Id}/title", new { title = "New title" });
        using var sourcesResponse = await client.PutAsJsonAsync($"/api/conversations/{conversation.Id}/sources", new { enabledSourceKeys = new[] { "collection:Mishnah" } });
        using var getResponse = await client.GetAsync($"/api/conversations/{conversation.Id}");
        var updated = await getResponse.Content.ReadFromJsonAsync<ConversationResponse>();
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

    private static async Task<ConversationResponse> CreateConversationAsync(HttpClient client)
    {
        using var response = await client.PostAsJsonAsync("/api/conversations", new { title = "Study" });
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<ConversationResponse>())!;
    }
}
