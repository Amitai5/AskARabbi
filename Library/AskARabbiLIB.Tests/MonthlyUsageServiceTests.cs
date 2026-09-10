using AskARabbiLIB.Usage;
using AskARabbiLIB.Persistence.InMemory;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace AskARabbiLIB.Tests;

[TestClass]
public sealed class MonthlyUsageServiceTests
{
    private static readonly Guid UserId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid OtherUserId = Guid.Parse("22222222-2222-2222-2222-222222222222");

    [TestMethod]
    [DataRow(2026, 1, 31, 2026, 2)]
    [DataRow(2026, 12, 31, 2027, 1)]
    [DataRow(2028, 2, 29, 2028, 3)]
    [TestCategory("Unit")]
    public async Task GetCurrentAsync_AnyDayInMonth_ReturnsUtcCalendarMonth(int year, int month, int day, int endYear, int endMonth)
    {
        var clock = new TestClock(new(year, month, day, 23, 59, 0, TimeSpan.Zero));
        var service = new MonthlyUsageService(new InMemoryUsageStore(), 10_000_000, clock);
        var lease = await service.BeginChatAsync(UserId);
        await service.RecordTokensAsync(lease, 2_500_000);

        var result = await service.GetCurrentAsync(UserId);

        Assert.AreEqual(new DateTimeOffset(year, month, 1, 0, 0, 0, TimeSpan.Zero), result.PeriodStartUtc);
        Assert.AreEqual(new DateTimeOffset(endYear, endMonth, 1, 0, 0, 0, TimeSpan.Zero), result.PeriodEndUtc);
        Assert.AreEqual(2_500_000L, result.TokensUsed);
        Assert.AreEqual(7_500_000L, result.TokensRemaining);
        Assert.AreEqual(25m, result.UsedPercent);
        Assert.IsFalse(result.IsLimitReached);
    }

    [TestMethod]
    [TestCategory("Regression")]
    public async Task BeginChatAsync_AtLimit_BlocksAndReportsReset()
    {
        var service = CreateService();
        var lease = await service.BeginChatAsync(UserId);
        await service.RecordTokensAsync(lease, 10_000_000);
        await service.EndChatAsync(lease);

        var error = await Assert.ThrowsExactlyAsync<ChatUsageException>(() => service.BeginChatAsync(UserId));

        Assert.AreEqual("usage_limit_reached", error.Code);
        Assert.IsNotNull(error.Usage);
        Assert.IsTrue(error.Usage.IsLimitReached);
        Assert.AreEqual(100m, error.Usage.UsedPercent);
        Assert.AreEqual(0L, error.Usage.TokensRemaining);
        Assert.AreEqual(new DateTimeOffset(2026, 9, 1, 0, 0, 0, TimeSpan.Zero), error.Usage.PeriodEndUtc);
    }

    [TestMethod]
    [TestCategory("Regression")]
    public async Task EnsureAvailableAsync_LastProviderResponseCrossesLimit_DoesNotPermitAnotherCall()
    {
        var service = CreateService();
        var lease = await service.BeginChatAsync(UserId);
        await service.RecordTokensAsync(lease, 10_000_010);

        var error = await Assert.ThrowsExactlyAsync<ChatUsageException>(() => service.EnsureAvailableAsync(lease));

        Assert.AreEqual("usage_limit_reached", error.Code);
        Assert.AreEqual(10_000_010L, error.Usage?.TokensUsed);
        Assert.AreEqual(100m, error.Usage?.UsedPercent);
    }

    [TestMethod]
    [TestCategory("Regression")]
    public async Task BeginChatAsync_ConcurrentSameAccount_AllowsOnlyOneLease()
    {
        var service = CreateService();
        var results = await Task.WhenAll(Enumerable.Range(0, 12).Select(async _ =>
        {
            try
            {
                await service.BeginChatAsync(UserId);
                return true;
            }
            catch (ChatUsageException exception) when (exception.Code == "chat_in_progress")
            {
                return false;
            }
        }));

        Assert.AreEqual(1, results.Count(value => value));
        Assert.AreEqual(0L, (await service.GetCurrentAsync(UserId)).TokensUsed);
    }

    [TestMethod]
    [TestCategory("Regression")]
    public async Task BeginChatAsync_DifferentAccounts_HaveIndependentAllowances()
    {
        var service = CreateService();
        var first = await service.BeginChatAsync(UserId);
        await service.RecordTokensAsync(first, 10_000_000);

        var second = await service.BeginChatAsync(OtherUserId);
        await service.RecordTokensAsync(second, 123);

        Assert.AreEqual(123L, (await service.GetCurrentAsync(OtherUserId)).TokensUsed);
        Assert.IsTrue((await service.GetCurrentAsync(UserId)).IsLimitReached);
    }

    [TestMethod]
    [TestCategory("Regression")]
    public async Task RecordTokensAsync_DuplicateAndOutOfOrderCumulativeReports_ChargesOnce()
    {
        var service = CreateService();
        var lease = await service.BeginChatAsync(UserId);

        await Task.WhenAll(service.RecordTokensAsync(lease, 300), service.RecordTokensAsync(lease, 500), service.RecordTokensAsync(lease, 300));
        await service.EndChatAsync(lease);
        var next = await service.BeginChatAsync(UserId);
        await service.RecordTokensAsync(next, 50);

        Assert.AreEqual(550L, (await service.GetCurrentAsync(UserId)).TokensUsed);
    }

    [TestMethod]
    [TestCategory("Regression")]
    public async Task RecordTokensAsync_RequestCrossesMonth_ChargesStartingMonthAndResetsNewMonth()
    {
        var clock = new TestClock(new(2026, 8, 31, 23, 59, 59, TimeSpan.Zero));
        var store = new InMemoryUsageStore();
        var service = new MonthlyUsageService(store, 10_000_000, clock);
        var august = await service.BeginChatAsync(UserId);
        clock.Now = clock.Now.AddSeconds(2);

        await service.RecordTokensAsync(august, 999);
        var september = await service.GetCurrentAsync(UserId);
        var next = await service.BeginChatAsync(UserId);

        Assert.AreEqual(0L, september.TokensUsed);
        Assert.AreEqual(new DateTimeOffset(2026, 9, 1, 0, 0, 0, TimeSpan.Zero), next.PeriodStartUtc);
        Assert.AreEqual(999L, await store.GetTokenCountAsync(UserId, august.PeriodStartUtc, august.PeriodEndUtc));
    }

    [TestMethod]
    [TestCategory("Regression")]
    public async Task BeginChatAsync_ExpiredOwner_IsFencedFromRecordingAndReleasingReplacement()
    {
        var clock = new TestClock(new(2026, 8, 25, 12, 0, 0, TimeSpan.Zero));
        var service = new MonthlyUsageService(new InMemoryUsageStore(), 10_000_000, clock);
        var expired = await service.BeginChatAsync(UserId);
        clock.Now = clock.Now.AddMinutes(11);
        var replacement = await service.BeginChatAsync(UserId);

        await Assert.ThrowsExactlyAsync<ChatUsageException>(() => service.RecordTokensAsync(expired, 500));
        await Assert.ThrowsExactlyAsync<ChatUsageException>(() => service.EnsureAvailableAsync(expired));
        await service.EndChatAsync(expired);
        await service.RecordTokensAsync(replacement, 123);

        var busy = await Assert.ThrowsExactlyAsync<ChatUsageException>(() => service.BeginChatAsync(UserId));
        Assert.AreEqual("chat_in_progress", busy.Code);
        Assert.AreEqual(123L, (await service.GetCurrentAsync(UserId)).TokensUsed);
    }

    [TestMethod]
    [DataRow(0L)]
    [DataRow(-1L)]
    public void Constructor_NonPositiveLimit_Throws(long limit) => Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => new MonthlyUsageService(new InMemoryUsageStore(), limit));

    [TestMethod]
    public async Task GetCurrentAsync_EmptyUserId_Throws() => await Assert.ThrowsExactlyAsync<ArgumentException>(() => CreateService().GetCurrentAsync(Guid.Empty));

    [TestMethod]
    public async Task BeginChatAsync_EmptyUserId_Throws() => await Assert.ThrowsExactlyAsync<ArgumentException>(() => CreateService().BeginChatAsync(Guid.Empty));

    [TestMethod]
    public async Task RecordTokensAsync_NegativeCount_Throws()
    {
        var service = CreateService();
        var lease = await service.BeginChatAsync(UserId);

        await Assert.ThrowsExactlyAsync<ArgumentOutOfRangeException>(() => service.RecordTokensAsync(lease, -1));
    }

    private static MonthlyUsageService CreateService() => new(new InMemoryUsageStore(), 10_000_000, new TestClock(new(2026, 8, 25, 12, 0, 0, TimeSpan.Zero)));

    private sealed class TestClock(DateTimeOffset now) : TimeProvider
    {
        internal DateTimeOffset Now { get; set; } = now;
        public override DateTimeOffset GetUtcNow() => Now;
    }
}
