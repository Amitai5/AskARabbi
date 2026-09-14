using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using AskARabbi.Api.Contracts.Conversations;
using AskARabbi.Api.Contracts.ConversationSettings;
using AskARabbiLIB.AI;
using AskARabbiLIB.Grounding;
using AskARabbiLIB.Usage;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace AskARabbi.Api.Tests;

[TestClass]
public sealed class TokenUsageIntegrationTests
{
    private static readonly DateTimeOffset Start = new(2026, 8, 1, 0, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset Now = new(2026, 8, 25, 12, 30, 0, TimeSpan.Zero);
    private static readonly Guid FirstMessageId = Guid.Parse("22222222-2222-2222-2222-222222222222");
    private static readonly Guid NextMessageId = Guid.Parse("33333333-3333-3333-3333-333333333333");
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web) { Converters = { new JsonStringEnumConverter() } };

    [TestMethod]
    [TestCategory("Integration")]
    public async Task Create_AtMonthlyTokenLimit_RejectsBeforeSavingOrCallingAi()
    {
        await using var app = new TestApplicationFactory();
        using var client = await app.CreateAuthenticatedClientAsync();
        await SeedUsageAsync(app, 5_000_000);

        using var response = await client.PostAsJsonAsync("/api/conversations?compact=true", new { messageId = FirstMessageId, content = "Explain this week's parashah." });
        using var problem = JsonDocument.Parse(await response.Content.ReadAsStringAsync());

        Assert.AreEqual(HttpStatusCode.TooManyRequests, response.StatusCode);
        Assert.AreEqual("usage_limit_reached", problem.RootElement.GetProperty("code").GetString());
        Assert.IsTrue(problem.RootElement.GetProperty("usage").GetProperty("isLimitReached").GetBoolean());
        Assert.AreEqual(100m, problem.RootElement.GetProperty("usage").GetProperty("usedPercent").GetDecimal());
        Assert.IsNotNull(response.Headers.RetryAfter);
        Assert.AreEqual(0, app.GroundedAnswers.CallCount);
        Assert.HasCount(0, await app.Store.ListAsync(app.Store.UserId, 100));
    }

    [TestMethod]
    [TestCategory("Integration")]
    public async Task AppendMessage_ExhaustedAllowance_BlocksOldChatsButAllowsReadsAndDvarTorah()
    {
        await using var app = new TestApplicationFactory();
        using var client = await app.CreateAuthenticatedClientAsync();
        using var firstResponse = await client.PostAsJsonAsync("/api/conversations", new { messageId = FirstMessageId, content = "Explain Shabbat." });
        var first = await firstResponse.Content.ReadFromJsonAsync<ConversationTurnResponse>(JsonOptions);
        Assert.IsNotNull(first);
        await SeedUsageAsync(app, 5_000_000 - 30);

        using var blocked = await client.PostAsJsonAsync($"/api/conversations/{first.Conversation.Id}/messages?compact=true", new { messageId = NextMessageId, content = "Why is that?" });
        using var read = await client.GetAsync($"/api/conversations/{first.Conversation.Id}");
        using var dvar = await client.GetAsync("/api/dvar-torah");
        var stored = await app.Store.GetAsync(app.Store.UserId, first.Conversation.Id);

        Assert.AreEqual(HttpStatusCode.TooManyRequests, blocked.StatusCode);
        Assert.AreEqual(HttpStatusCode.OK, read.StatusCode);
        Assert.AreEqual(HttpStatusCode.OK, dvar.StatusCode);
        Assert.IsNotNull(stored);
        Assert.HasCount(2, stored.Messages);
        Assert.AreEqual(1, app.GroundedAnswers.CallCount);
    }

    [TestMethod]
    [TestCategory("Integration")]
    public async Task CreateAndAppend_ProviderUsage_IncrementsAcrossChatsAndReturnsPercentage()
    {
        await using var app = new TestApplicationFactory();
        using var client = await app.CreateAuthenticatedClientAsync();
        using var firstResponse = await client.PostAsJsonAsync("/api/conversations?compact=true", new { messageId = FirstMessageId, content = "Explain Shabbat." });
        var first = await firstResponse.Content.ReadFromJsonAsync<ConversationTurnDeltaResponse>(JsonOptions);
        Assert.IsNotNull(first);

        using var nextResponse = await client.PostAsJsonAsync($"/api/conversations/{first.Conversation.Id}/messages?compact=true", new { messageId = NextMessageId, content = "Why is that?" });
        var next = await nextResponse.Content.ReadFromJsonAsync<ConversationTurnDeltaResponse>(JsonOptions);
        using var otherResponse = await client.PostAsJsonAsync("/api/conversations?compact=true", new { messageId = NextMessageId, content = "Summarize Vayigash." });
        var other = await otherResponse.Content.ReadFromJsonAsync<ConversationTurnDeltaResponse>(JsonOptions);

        Assert.AreEqual(30L, first.Usage?.TokensUsed);
        Assert.AreEqual(60L, next?.Usage?.TokensUsed);
        Assert.AreEqual(90L, other?.Usage?.TokensUsed);
        Assert.AreEqual(0.0018m, other?.Usage?.UsedPercent);
        Assert.AreEqual(5_000_000L, other?.Usage?.TokenLimit);
        Assert.AreEqual(3, app.GroundedAnswers.CallCount);
    }

    [TestMethod]
    [TestCategory("Integration")]
    public async Task AppendMessage_AnsweredRetryAtLimit_DoesNotRegenerateOrChargeAgain()
    {
        await using var app = new TestApplicationFactory();
        using var client = await app.CreateAuthenticatedClientAsync();
        using var created = await client.PostAsJsonAsync("/api/conversations", new { messageId = FirstMessageId, content = "Explain Shabbat." });
        var first = await created.Content.ReadFromJsonAsync<ConversationTurnResponse>(JsonOptions);
        Assert.IsNotNull(first);
        await SeedUsageAsync(app, 5_000_000 - 30);

        using var repeated = await client.PostAsJsonAsync($"/api/conversations/{first.Conversation.Id}/messages", new { messageId = FirstMessageId, content = "Explain Shabbat." });
        var retry = await repeated.Content.ReadFromJsonAsync<ConversationTurnResponse>(JsonOptions);

        Assert.AreEqual(HttpStatusCode.OK, repeated.StatusCode);
        Assert.AreEqual("answered", retry?.Status);
        Assert.AreEqual(5_000_000L, retry?.Usage?.TokensUsed);
        Assert.AreEqual(1, app.GroundedAnswers.CallCount);
    }

    [TestMethod]
    [TestCategory("Integration")]
    public async Task Create_OneTokenBelowMonthlyLimit_AllowsAnswerThenBlocksNextQuestion()
    {
        await using var app = new TestApplicationFactory();
        using var client = await app.CreateAuthenticatedClientAsync();
        await SeedUsageAsync(app, 4_999_999);

        using var response = await client.PostAsJsonAsync("/api/conversations?compact=true", new { messageId = FirstMessageId, content = "Explain Shabbat." });
        var turn = await response.Content.ReadFromJsonAsync<ConversationTurnDeltaResponse>(JsonOptions);
        using var next = await client.PostAsJsonAsync("/api/conversations", new { messageId = NextMessageId, content = "Explain another custom." });

        Assert.AreEqual(HttpStatusCode.Created, response.StatusCode);
        Assert.AreEqual("answered", turn?.Status);
        Assert.AreEqual(5_000_029L, turn?.Usage?.TokensUsed);
        Assert.AreEqual(0L, turn?.Usage?.TokensRemaining);
        Assert.AreEqual(HttpStatusCode.TooManyRequests, next.StatusCode);
        Assert.AreEqual(1, app.GroundedAnswers.CallCount);
    }

    [TestMethod]
    [DataRow(2_500_000L, 2_500_000L, 50, false)]
    [DataRow(5_000_000L, 0L, 100, true)]
    [DataRow(6_000_000L, 0L, 100, true)]
    [TestCategory("Integration")]
    public async Task GetUsage_PreviousAllowanceUsage_PreservesCountersWithReducedLimit(long tokensUsed, long tokensRemaining, int usedPercent, bool isLimitReached)
    {
        await using var app = new TestApplicationFactory();
        using var client = await app.CreateAuthenticatedClientAsync();
        var previousLease = new ChatUsageLease(app.Store.UserId, Guid.Parse("55555555-5555-5555-5555-555555555555"), Start, Start.AddMonths(1), Now.AddMinutes(10), 10_000_000);
        Assert.IsTrue(await app.Store.TryAcquireChatAsync(previousLease, Now));
        Assert.IsTrue(await app.Store.RecordTokensAsync(previousLease, tokensUsed));
        await app.Store.ReleaseChatAsync(previousLease);

        var usage = await client.GetFromJsonAsync<UsageResponse>("/api/conversation-settings/usage");

        Assert.IsNotNull(usage);
        Assert.AreEqual(5_000_000L, usage.TokenLimit);
        Assert.AreEqual(tokensUsed, usage.TokensUsed);
        Assert.AreEqual(tokensRemaining, usage.TokensRemaining);
        Assert.AreEqual((decimal)usedPercent, usage.UsedPercent);
        Assert.AreEqual(isLimitReached, usage.IsLimitReached);
        Assert.AreEqual(tokensUsed, await app.Store.GetTokenCountAsync(app.Store.UserId, Start, Start.AddMonths(1)));
    }

    [TestMethod]
    [DataRow(GroundedAnswerStatus.ValidationFailed)]
    [DataRow(GroundedAnswerStatus.AIUnavailable)]
    [TestCategory("Integration")]
    public async Task Create_FailedGeneration_StillChargesReportedTokens(GroundedAnswerStatus status)
    {
        await using var app = new TestApplicationFactory();
        using var client = await app.CreateAuthenticatedClientAsync();
        app.GroundedAnswers.NextResult = new GroundedAnswerResult
        {
            Status = status,
            ErrorMessage = "Internal diagnostic.",
            Trace = new GroundedAnswerTrace(TimeSpan.Zero, TimeSpan.Zero, 2, 1, 100, new AIUsage(400, 600, 1_000), GroundedValidationStatus.Failed, true, "failed-response", "test-model"),
        };

        using var response = await client.PostAsJsonAsync("/api/conversations?compact=true", new { messageId = FirstMessageId, content = "Explain Shabbat." });
        var turn = await response.Content.ReadFromJsonAsync<ConversationTurnDeltaResponse>(JsonOptions);

        Assert.IsNotNull(turn);
        Assert.AreEqual(1_000L, turn.Usage?.TokensUsed);
        Assert.HasCount(1, turn.Messages);
        using var next = await client.PostAsJsonAsync("/api/conversations", new { messageId = NextMessageId, content = "Try another question." });
        Assert.AreEqual(HttpStatusCode.Created, next.StatusCode);
    }

    [TestMethod]
    [TestCategory("Integration")]
    public async Task Create_OtherReplicaOwnsChat_ReturnsConflictWithoutSaving()
    {
        await using var app = new TestApplicationFactory();
        using var client = await app.CreateAuthenticatedClientAsync();
        var lease = CreateLease(app.Store.UserId);
        Assert.IsTrue(await app.Store.TryAcquireChatAsync(lease, Now));

        using var response = await client.PostAsJsonAsync("/api/conversations", new { messageId = FirstMessageId, content = "Explain Shabbat." });
        using var problem = JsonDocument.Parse(await response.Content.ReadAsStringAsync());

        Assert.AreEqual(HttpStatusCode.Conflict, response.StatusCode);
        Assert.AreEqual("chat_in_progress", problem.RootElement.GetProperty("code").GetString());
        Assert.HasCount(0, await app.Store.ListAsync(app.Store.UserId, 100));
        Assert.AreEqual(0, app.GroundedAnswers.CallCount);
    }

    private static async Task SeedUsageAsync(TestApplicationFactory app, long tokens)
    {
        var lease = CreateLease(app.Store.UserId);
        Assert.IsTrue(await app.Store.TryAcquireChatAsync(lease, Now));
        Assert.IsTrue(await app.Store.RecordTokensAsync(lease, tokens));
        await app.Store.ReleaseChatAsync(lease);
    }

    private static ChatUsageLease CreateLease(Guid userId) => new(userId, Guid.Parse("44444444-4444-4444-4444-444444444444"), Start, Start.AddMonths(1), Now.AddMinutes(10), 5_000_000);
}
