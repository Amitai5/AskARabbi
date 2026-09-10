using AskARabbiLIB.DvarTorah;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace AskARabbi.DvarTorahJob.Tests;

[TestClass]
public sealed class DeferredWeeklyDvarTorahGeneratorTests
{
    [TestMethod]
    [TestCategory("Regression")]
    public async Task GenerateAsync_DeferredUntilInvoked_PassesExactWeekAndCancellation()
    {
        var factoryCalls = 0;
        var inner = new RecordingGenerator();
        var generator = new DeferredWeeklyDvarTorahGenerator(_ =>
        {
            factoryCalls++;
            return Task.FromResult<IWeeklyDvarTorahGenerator>(inner);
        });
        var week = new WeeklyDvarTorahWeek(new DateOnly(2026, 9, 5), "23 Elul, 5786", "Nitzavim", null, false);
        using var cancellation = new CancellationTokenSource();
        Assert.AreEqual(0, factoryCalls);

        var result = await generator.GenerateAsync(week, cancellation.Token);

        Assert.AreEqual(1, factoryCalls);
        Assert.AreSame(week, inner.Week);
        Assert.AreEqual(cancellation.Token, inner.CancellationToken);
        Assert.AreEqual("A teaching", result.Title);
    }

    [TestMethod]
    [TestCategory("Regression")]
    public async Task GenerateAsync_Canceled_DoesNotInitializeAi()
    {
        var generator = new DeferredWeeklyDvarTorahGenerator(_ => throw new AssertFailedException("AI must not be initialized."));
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        await Assert.ThrowsExactlyAsync<OperationCanceledException>(() => generator.GenerateAsync(new WeeklyDvarTorahWeek(new DateOnly(2026, 9, 5), "23 Elul, 5786", "Nitzavim", null, false), cancellation.Token));
    }

    private sealed class RecordingGenerator : IWeeklyDvarTorahGenerator
    {
        internal WeeklyDvarTorahWeek? Week { get; private set; }
        internal CancellationToken CancellationToken { get; private set; }

        public Task<WeeklyDvarTorahDraft> GenerateAsync(WeeklyDvarTorahWeek week, CancellationToken cancellationToken = default)
        {
            Week = week;
            CancellationToken = cancellationToken;
            return Task.FromResult(new WeeklyDvarTorahDraft("A teaching", "A complete speech.", "test"));
        }
    }
}
