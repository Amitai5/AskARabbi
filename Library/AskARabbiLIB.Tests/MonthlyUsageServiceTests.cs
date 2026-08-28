using AskARabbiLIB.Usage;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace AskARabbiLIB.Tests;

[TestClass]
public sealed class MonthlyUsageServiceTests
{
    private static readonly Guid UserId = Guid.Parse("11111111-1111-1111-1111-111111111111");

    [TestMethod]
    [DataRow(2026, 1, 31, 2026, 1, 1, 2026, 2, 1)]
    [DataRow(2026, 12, 31, 2026, 12, 1, 2027, 1, 1)]
    [TestCategory("Unit")]
    public async Task GetCurrentAsync_AnyDayInMonth_ReturnsExactUtcCalendarMonth(int currentYear, int currentMonth, int currentDay, int startYear, int startMonth, int startDay, int endYear, int endMonth, int endDay)
    {
        var store = new FakeUsageStore { AnswerCount = 12 };
        var now = new DateTimeOffset(currentYear, currentMonth, currentDay, 23, 59, 0, TimeSpan.Zero);
        var service = new MonthlyUsageService(store, 50, new FixedTimeProvider(now));

        var result = await service.GetCurrentAsync(UserId);

        Assert.AreEqual(new DateTimeOffset(startYear, startMonth, startDay, 0, 0, 0, TimeSpan.Zero), result.PeriodStartUtc);
        Assert.AreEqual(new DateTimeOffset(endYear, endMonth, endDay, 0, 0, 0, TimeSpan.Zero), result.PeriodEndUtc);
        Assert.AreEqual(12, result.AnswersUsed);
        Assert.AreEqual(38, result.AnswersRemaining);
    }

    [TestMethod]
    [TestCategory("Unit")]
    public async Task RecordAnswerAsync_UsageAboveLimit_ReportsZeroRemaining()
    {
        var store = new FakeUsageStore { IncrementedAnswerCount = 51 };
        var service = new MonthlyUsageService(store, 50, new FixedTimeProvider(new DateTimeOffset(2026, 8, 25, 1, 0, 0, TimeSpan.Zero)));

        var result = await service.RecordAnswerAsync(UserId);

        Assert.AreEqual(51, result.AnswersUsed);
        Assert.AreEqual(0, result.AnswersRemaining);
        Assert.AreEqual(UserId, store.LastUserId);
    }

    [TestMethod]
    [TestCategory("Unit")]
    public void Constructor_NonPositiveLimit_Throws()
    {
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => new MonthlyUsageService(new FakeUsageStore(), 0));
    }

    [TestMethod]
    [TestCategory("Unit")]
    public async Task GetCurrentAsync_EmptyUserId_ThrowsWithoutReadingStore()
    {
        var store = new FakeUsageStore();
        var service = new MonthlyUsageService(store, 50);

        await Assert.ThrowsExactlyAsync<ArgumentException>(() => service.GetCurrentAsync(Guid.Empty));

        Assert.AreEqual(0, store.ReadCount);
    }

    private sealed class FixedTimeProvider : TimeProvider
    {
        private readonly DateTimeOffset utcNow;

        internal FixedTimeProvider(DateTimeOffset utcNow)
        {
            this.utcNow = utcNow;
        }

        public override DateTimeOffset GetUtcNow() => utcNow;
    }

    private sealed class FakeUsageStore : IUsageStore
    {
        internal int AnswerCount { get; init; }
        internal int IncrementedAnswerCount { get; init; }
        internal int ReadCount { get; private set; }
        internal Guid LastUserId { get; private set; }

        public Task<int> GetAnswerCountAsync(Guid userId, DateTimeOffset periodStartUtc, DateTimeOffset periodEndUtc, CancellationToken cancellationToken = default)
        {
            LastUserId = userId;
            ReadCount++;
            return Task.FromResult(AnswerCount);
        }

        public Task<int> IncrementAnswerCountAsync(Guid userId, DateTimeOffset periodStartUtc, DateTimeOffset periodEndUtc, CancellationToken cancellationToken = default)
        {
            LastUserId = userId;
            return Task.FromResult(IncrementedAnswerCount);
        }
    }
}
