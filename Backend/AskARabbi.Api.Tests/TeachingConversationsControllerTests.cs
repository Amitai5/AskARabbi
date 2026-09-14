using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using AskARabbi.Api.Contracts.Conversations;
using AskARabbiLIB.DvarTorah;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace AskARabbi.Api.Tests;

[TestClass]
public sealed class TeachingConversationsControllerTests
{
    private static readonly DateTimeOffset Published = new(2026, 8, 24, 12, 0, 0, TimeSpan.Zero);
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web) { Converters = { new JsonStringEnumConverter() } };

    [TestMethod]
    [DataRow(null)]
    [DataRow("Choose life.")]
    [TestCategory("Integration")]
    public async Task Create_PublishedTeaching_RetainsWholeSnapshotForFollowUps(string? selection)
    {
        await using var application = new TestApplicationFactory();
        var article = CreateArticle();
        application.WeeklyDvarTorah.CurrentArticle = article;
        using var client = await application.CreateAuthenticatedClientAsync();

        using var response = await client.PostAsJsonAsync("/api/conversations?compact=true", new { messageId = Guid.Parse("51a75661-6248-413b-a96f-111111111111"), content = "What can I learn from this?", teaching = new { weekKey = article.Week.WeekKey, selectedText = selection, body = "Untrusted replacement that must not be used." } });
        var created = await response.Content.ReadFromJsonAsync<ConversationTurnDeltaResponse>(JsonOptions);

        Assert.AreEqual(HttpStatusCode.Created, response.StatusCode);
        Assert.IsNotNull(created);
        Assert.IsNotNull(created.TeachingContext);
        Assert.AreEqual(selection, created.TeachingContext.SelectedText);
        Assert.AreEqual(article.Title, created.TeachingContext.Title);
        Assert.AreEqual("What can I learn from this?", created.Messages[0].Content);
        Assert.IsNotNull(application.GroundedAnswers.LastQuestion?.TeachingContext);
        Assert.AreEqual(article.Body, application.GroundedAnswers.LastQuestion.TeachingContext.Body);
        var snapshot = application.GroundedAnswers.LastQuestion.TeachingContext;

        application.WeeklyDvarTorah.CurrentArticle = null;
        using var followUp = await client.PostAsJsonAsync($"/api/conversations/{created.Conversation.Id}/messages", new { messageId = Guid.Parse("51a75661-6248-413b-a96f-222222222222"), content = "And how can we practice that?" });
        Assert.AreEqual(HttpStatusCode.OK, followUp.StatusCode);
        Assert.AreEqual(snapshot, application.GroundedAnswers.LastQuestion.TeachingContext);
        using var loaded = await client.GetAsync($"/api/conversations/{created.Conversation.Id}");
        var saved = await loaded.Content.ReadFromJsonAsync<ConversationResponse>(JsonOptions);
        Assert.AreEqual(created.TeachingContext, saved?.TeachingContext);
        Assert.IsFalse((await loaded.Content.ReadAsStringAsync()).Contains(article.Body, StringComparison.Ordinal), "Full teaching snapshots must not inflate conversation API responses.");
    }

    [TestMethod]
    [DataRow("Not actually in the teaching")]
    [DataRow("")]
    [DataRow("   ")]
    [TestCategory("Integration")]
    public async Task Create_InvalidSelection_RejectsWithoutCreatingChat(string selection)
    {
        await using var application = new TestApplicationFactory();
        application.WeeklyDvarTorah.CurrentArticle = CreateArticle();
        using var client = await application.CreateAuthenticatedClientAsync();

        using var response = await client.PostAsJsonAsync("/api/conversations", new { messageId = Guid.Parse("51a75661-6248-413b-a96f-333333333333"), content = "Explain this.", teaching = new { weekKey = "diaspora:2026-08-29", selectedText = selection } });
        var chats = await client.GetFromJsonAsync<ConversationSummaryResponse[]>("/api/conversations");

        Assert.AreEqual(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.AreEqual(0, application.GroundedAnswers.CallCount);
        Assert.IsNotNull(chats);
        Assert.HasCount(0, chats);
    }

    [TestMethod]
    [DataRow("diaspora:2026-09-05")]
    [DataRow("israel:2026-08-29")]
    [DataRow("diaspora:2026-08-22")]
    [DataRow("invalid-key")]
    [TestCategory("Integration")]
    public async Task Create_UnavailableTeaching_RejectsWithoutCallingModel(string weekKey)
    {
        await using var application = new TestApplicationFactory();
        application.WeeklyDvarTorah.CurrentArticle = CreateArticle();
        using var client = await application.CreateAuthenticatedClientAsync();

        using var response = await client.PostAsJsonAsync("/api/conversations", new { messageId = Guid.Parse("51a75661-6248-413b-a96f-444444444444"), content = "Explain this.", teaching = new { weekKey } });

        Assert.AreEqual(HttpStatusCode.NotFound, response.StatusCode);
        Assert.AreEqual(0, application.GroundedAnswers.CallCount);
    }

    [TestMethod]
    [TestCategory("Integration")]
    public async Task Create_PastPublishedTeaching_AcceptsArchivedContext()
    {
        await using var application = new TestApplicationFactory();
        application.WeeklyDvarTorah.ArchivedArticles.Add(CreateArticle(new DateOnly(2026, 8, 22)));
        using var client = await application.CreateAuthenticatedClientAsync();

        using var response = await client.PostAsJsonAsync("/api/conversations", new { messageId = Guid.Parse("51a75661-6248-413b-a96f-555555555555"), content = "Explain this.", teaching = new { weekKey = "diaspora:2026-08-22" } });

        Assert.AreEqual(HttpStatusCode.Created, response.StatusCode);
        Assert.AreEqual("diaspora:2026-08-22", application.GroundedAnswers.LastQuestion?.TeachingContext?.WeekKey);
    }

    private static WeeklyDvarTorahArticle CreateArticle(DateOnly? date = null) => new(new WeeklyDvarTorahWeek(date ?? new DateOnly(2026, 8, 29), "16 Elul, 5786", "Ki Tavo", null, false), "Choosing responsibility", "Choose life.\n\n" + new string('a', 5_000) + "\nThe ending is part of the context too.", "test", Published, Published);
}
