using AskARabbiLIB.AI;
using AskARabbiLIB.Persistence.InMemory;
using AskARabbiLIB.Usage;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace AskARabbiLIB.Tests;

[TestClass]
public sealed class ChatUsageContextTests
{
    private static readonly Guid UserId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly DateTimeOffset Now = new(2026, 9, 10, 12, 0, 0, TimeSpan.Zero);

    [TestMethod]
    [TestCategory("Regression")]
    public async Task RecordAsync_MultipleStagesAndDuplicateResponse_CountsInputAndOutputOnce()
    {
        var service = new MonthlyUsageService(new InMemoryUsageStore(), 10_000_000, new FixedClock());
        var context = new ChatUsageContext();
        var lease = await service.BeginChatAsync(UserId);
        using var scope = context.Begin(lease, service);

        await context.RecordAsync("retrieval", new AIUsage(100, 10, 110));
        await context.RecordAsync("draft", new AIUsage(300, 500, 800));
        await context.RecordAsync("repair", new AIUsage(400, 200, 600));
        await context.RecordAsync("repair", new AIUsage(400, 200, 600));
        await context.RecordAsync("validation", new AIUsage(100, 100, 200));

        Assert.AreEqual(1_710L, (await service.GetCurrentAsync(UserId)).TokensUsed);
        Assert.IsTrue(context.HasReportedUsage);
        Assert.AreEqual(1_710L, context.TokensRecorded);
        Assert.AreEqual(4, context.ProviderResponsesRecorded);
    }

    [TestMethod]
    [TestCategory("Regression")]
    public async Task RecordAsync_ParallelUsers_ChargesTheirOwnScopes()
    {
        var service = new MonthlyUsageService(new InMemoryUsageStore(), 10_000_000, new FixedClock());
        var context = new ChatUsageContext();
        var otherId = Guid.Parse("22222222-2222-2222-2222-222222222222");
        var bothStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var started = 0;

        await Task.WhenAll(RunAsync(UserId, 123), RunAsync(otherId, 456));

        Assert.AreEqual(123L, (await service.GetCurrentAsync(UserId)).TokensUsed);
        Assert.AreEqual(456L, (await service.GetCurrentAsync(otherId)).TokensUsed);
        Assert.IsFalse(context.HasReportedUsage);

        async Task RunAsync(Guid userId, int tokens)
        {
            var lease = await service.BeginChatAsync(userId);
            using var scope = context.Begin(lease, service);
            if (Interlocked.Increment(ref started) == 2) { bothStarted.SetResult(); }
            await bothStarted.Task;
            await context.RecordAsync("same-response-id-in-separate-accounts", new AIUsage(tokens, 0, tokens));
        }
    }

    [TestMethod]
    [TestCategory("Regression")]
    public async Task RecordAsync_ParallelStages_NoLostIncrements()
    {
        var service = new MonthlyUsageService(new InMemoryUsageStore(), 10_000_000, new FixedClock());
        var context = new ChatUsageContext();
        using var scope = context.Begin(await service.BeginChatAsync(UserId), service);

        await Task.WhenAll(Enumerable.Range(0, 20).Select(index => context.RecordAsync($"response-{index}", new AIUsage(100, 20, 120))));

        Assert.AreEqual(2_400L, (await service.GetCurrentAsync(UserId)).TokensUsed);
    }

    [TestMethod]
    [TestCategory("Regression")]
    public async Task BeforeRequestAsync_AllowanceConsumedByDraft_StopsValidationAndRepair()
    {
        var service = new MonthlyUsageService(new InMemoryUsageStore(), 100, new FixedClock());
        var context = new ChatUsageContext();
        using var scope = context.Begin(await service.BeginChatAsync(UserId), service);
        await context.BeforeRequestAsync();
        await context.RecordAsync("draft", new AIUsage(80, 20, 100));

        var error = await Assert.ThrowsExactlyAsync<ChatUsageException>(() => context.BeforeRequestAsync());

        Assert.AreEqual("usage_limit_reached", error.Code);
    }

    [TestMethod]
    [TestCategory("Regression")]
    public async Task RecordAsync_PersistenceFailure_StopsFurtherPaidCalls()
    {
        var service = new MonthlyUsageService(new RejectedAccountingStore(), 100, new FixedClock());
        var context = new ChatUsageContext();
        using var scope = context.Begin(await service.BeginChatAsync(UserId), service);

        var recording = await Assert.ThrowsExactlyAsync<ChatUsageException>(() => context.RecordAsync("draft", new AIUsage(1, 1, 2)));
        var nextCall = await Assert.ThrowsExactlyAsync<ChatUsageException>(() => context.BeforeRequestAsync());

        Assert.AreEqual("usage_unavailable", recording.Code);
        Assert.AreSame(recording, nextCall);
        Assert.AreEqual(0L, context.TokensRecorded);
        Assert.AreEqual(0, context.ProviderResponsesRecorded);
    }

    [TestMethod]
    [TestCategory("Regression")]
    public async Task RecordAsync_NoChatScope_DoesNotChargeWeeklyDvarTorahWork()
    {
        var context = new ChatUsageContext();

        await context.BeforeRequestAsync();
        await context.RecordAsync("weekly-generation", new AIUsage(100, 100, 200));

        Assert.IsFalse(context.HasReportedUsage);
        Assert.AreEqual(0L, context.TokensRecorded);
        Assert.AreEqual(0, context.ProviderResponsesRecorded);
    }

    [TestMethod]
    public async Task Begin_NestedScope_RejectsAccidentalAccountAttribution()
    {
        var service = new MonthlyUsageService(new InMemoryUsageStore(), 100, new FixedClock());
        var context = new ChatUsageContext();
        var lease = await service.BeginChatAsync(UserId);
        using var scope = context.Begin(lease, service);

        Assert.ThrowsExactly<InvalidOperationException>(() => context.Begin(lease, service));
    }

    private sealed class FixedClock : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => Now;
    }

    private sealed class RejectedAccountingStore : IUsageStore
    {
        public Task<long> GetTokenCountAsync(Guid userId, DateTimeOffset periodStartUtc, DateTimeOffset periodEndUtc, CancellationToken cancellationToken = default) => Task.FromResult(0L);
        public Task<bool> TryAcquireChatAsync(ChatUsageLease lease, DateTimeOffset now, CancellationToken cancellationToken = default) => Task.FromResult(true);
        public Task<bool> RecordTokensAsync(ChatUsageLease lease, long cumulativeTokens, CancellationToken cancellationToken = default) => Task.FromException<bool>(new InvalidOperationException("Database unavailable"));
        public Task ReleaseChatAsync(ChatUsageLease lease, CancellationToken cancellationToken = default) => Task.CompletedTask;
    }
}
