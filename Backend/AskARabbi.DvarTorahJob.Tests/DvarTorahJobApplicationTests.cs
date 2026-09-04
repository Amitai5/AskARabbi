using AskARabbiLIB.DvarTorah;
using AskARabbiLIB.DvarTorah.Audio;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace AskARabbi.DvarTorahJob.Tests;

[TestClass]
public sealed class DvarTorahJobApplicationTests
{
    [TestMethod]
    [TestCategory("Unit")]
    public async Task RunAsync_WhenGenerationIsDisabled_DoesNotConstructExternalServices()
    {
        var execution = new TestExecution { IsGenerationEnabled = false };

        var result = await execution.CreateApplication().RunAsync();

        Assert.IsNull(result);
        Assert.IsEmpty(execution.Operations);
    }

    [TestMethod]
    [TestCategory("Unit")]
    public async Task RunAsync_WhenGenerationFails_DoesNotAttemptAudio()
    {
        var expected = new InvalidOperationException("Expected failure");
        var execution = new TestExecution { GenerationFailure = expected };

        var actual = await Assert.ThrowsExactlyAsync<InvalidOperationException>(() => execution.CreateApplication().RunAsync());

        Assert.AreSame(expected, actual);
        CollectionAssert.AreEqual(new[] { "publish-text" }, execution.Operations);
    }

    [TestMethod]
    [TestCategory("Unit")]
    public async Task RunAsync_WhenCanceledBeforeStart_DoesNotReadConfiguration()
    {
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        var execution = new TestExecution();

        await Assert.ThrowsExactlyAsync<OperationCanceledException>(() => execution.CreateApplication().RunAsync(cancellation.Token));

        Assert.IsFalse(execution.WasConfigurationRead);
        Assert.IsEmpty(execution.Operations);
    }

    [TestMethod]
    [DataRow(WeeklyDvarTorahGenerationStatus.Published)]
    [DataRow(WeeklyDvarTorahGenerationStatus.AlreadyPublished)]
    [TestCategory("Unit")]
    public async Task RunAsync_PublishedArticle_GeneratesAudioOnlyAfterPublication(WeeklyDvarTorahGenerationStatus status)
    {
        using var cancellation = new CancellationTokenSource();
        var execution = new TestExecution { GenerationStatus = status };

        var result = await execution.CreateApplication().RunAsync(cancellation.Token);

        Assert.IsNotNull(result);
        Assert.AreEqual(status, result.Generation.Status);
        Assert.AreEqual(WeeklyDvarTorahAudioStatus.Generated, result.Audio?.Status);
        Assert.AreSame(execution.Article, execution.NarratedArticle);
        Assert.IsNull(result.AudioFailureCode);
        Assert.AreEqual("test-invocation", execution.AudioInvocationId);
        Assert.AreEqual(cancellation.Token, execution.AudioCancellationToken);
        CollectionAssert.AreEqual(new[] { "publish-text", "narrate-published-text" }, execution.Operations);
    }

    [TestMethod]
    [TestCategory("Unit")]
    public async Task RunAsync_TextOwnedByAnotherInvocation_DoesNotSynthesize()
    {
        var execution = new TestExecution { GenerationStatus = WeeklyDvarTorahGenerationStatus.GenerationInProgress, ReturnPublication = false };

        var result = await execution.CreateApplication().RunAsync();

        Assert.IsNotNull(result);
        Assert.IsNull(result.Audio);
        CollectionAssert.AreEqual(new[] { "publish-text" }, execution.Operations);
    }

    [TestMethod]
    [TestCategory("Unit")]
    public async Task RunAsync_BackfillWithTextDisabled_LoadsOnlyRequestedPublication()
    {
        var execution = new TestExecution { IsGenerationEnabled = false, BackfillWeekKey = "diaspora:2026-09-05" };

        var result = await execution.CreateApplication().RunAsync();

        Assert.IsNotNull(result);
        Assert.AreEqual(WeeklyDvarTorahGenerationStatus.AlreadyPublished, result.Generation.Status);
        Assert.AreEqual(execution.BackfillWeekKey, execution.LoadedWeekKey);
        Assert.AreSame(execution.Article, execution.NarratedArticle);
        CollectionAssert.AreEqual(new[] { "load-published-text", "narrate-published-text" }, execution.Operations);
    }

    [TestMethod]
    [TestCategory("Unit")]
    public async Task RunAsync_BackfillUnknownWeek_DoesNotGenerateReplacementText()
    {
        var execution = new TestExecution { BackfillWeekKey = "diaspora:2026-09-05", ReturnPublication = false };

        await Assert.ThrowsExactlyAsync<DvarTorahJobConfigurationException>(() => execution.CreateApplication().RunAsync());

        CollectionAssert.AreEqual(new[] { "load-published-text" }, execution.Operations);
    }

    [TestMethod]
    [DataRow("")]
    [DataRow(" ")]
    [DataRow("2026-09-05")]
    [DataRow("all")]
    [DataRow("diaspora:2026-09-04")]
    [DataRow("diaspora:2026-13-05")]
    [TestCategory("Unit")]
    public async Task RunAsync_InvalidBackfillKey_FailsBeforeExternalServices(string key)
    {
        var execution = new TestExecution { BackfillWeekKey = key };

        await Assert.ThrowsExactlyAsync<DvarTorahJobConfigurationException>(() => execution.CreateApplication().RunAsync());

        Assert.IsEmpty(execution.Operations);
    }

    [TestMethod]
    [TestCategory("Unit")]
    public async Task RunAsync_AudioFailure_PreservesPublishedArticleAndRequestsRetry()
    {
        var execution = new TestExecution { AudioFailure = new IOException("Expected unavailable encoder") };

        var result = await execution.CreateApplication().RunAsync();

        Assert.IsNotNull(result);
        Assert.AreEqual(WeeklyDvarTorahGenerationStatus.Published, result.Generation.Status);
        Assert.AreSame(execution.Article, result.Generation.Article);
        Assert.IsNull(result.Audio);
        Assert.AreEqual(nameof(IOException), result.AudioFailureCode);
        CollectionAssert.AreEqual(new[] { "publish-text", "narrate-published-text" }, execution.Operations);
    }

    [TestMethod]
    [TestCategory("Unit")]
    public async Task RunAsync_AudioDisabled_PreservesPublishedArticleWithoutFailure()
    {
        var execution = new TestExecution { AudioStatus = WeeklyDvarTorahAudioStatus.Disabled };

        var result = await execution.CreateApplication().RunAsync();

        Assert.IsNotNull(result);
        Assert.AreEqual(WeeklyDvarTorahAudioStatus.Disabled, result.Audio?.Status);
        Assert.IsNull(result.AudioFailureCode);
    }

    [TestMethod]
    [TestCategory("Unit")]
    public async Task RunAsync_BackfillWithAudioDisabled_ReportsConfigurationFailure()
    {
        var execution = new TestExecution { BackfillWeekKey = "diaspora:2026-09-05", AudioStatus = WeeklyDvarTorahAudioStatus.Disabled };

        var result = await execution.CreateApplication().RunAsync();

        Assert.IsNotNull(result);
        Assert.AreEqual(nameof(DvarTorahJobConfigurationException), result.AudioFailureCode);
        Assert.AreSame(execution.Article, result.Generation.Article);
        CollectionAssert.AreEqual(new[] { "load-published-text", "narrate-published-text" }, execution.Operations);
    }

    [TestMethod]
    [TestCategory("Unit")]
    public async Task RunAsync_AudioLeaseLost_RequestsRetryWithoutRepublishingText()
    {
        var execution = new TestExecution { AudioStatus = WeeklyDvarTorahAudioStatus.LostLease };

        var result = await execution.CreateApplication().RunAsync();

        Assert.IsNotNull(result);
        Assert.AreEqual("AudioLeaseLost", result.AudioFailureCode);
        Assert.AreSame(execution.Article, result.Generation.Article);
    }

    [TestMethod]
    [TestCategory("Unit")]
    public async Task RunAsync_CanceledDuringAudio_PropagatesCancellation()
    {
        using var cancellation = new CancellationTokenSource();
        var execution = new TestExecution { BeforeAudio = () => cancellation.Cancel() };

        await Assert.ThrowsExactlyAsync<OperationCanceledException>(() => execution.CreateApplication().RunAsync(cancellation.Token));

        CollectionAssert.AreEqual(new[] { "publish-text", "narrate-published-text" }, execution.Operations);
    }

    private sealed class TestExecution
    {
        internal bool IsGenerationEnabled { get; init; } = true;
        internal bool ReturnPublication { get; init; } = true;
        internal string? BackfillWeekKey { get; init; }
        internal WeeklyDvarTorahGenerationStatus GenerationStatus { get; init; } = WeeklyDvarTorahGenerationStatus.Published;
        internal WeeklyDvarTorahAudioStatus AudioStatus { get; init; } = WeeklyDvarTorahAudioStatus.Generated;
        internal Exception? GenerationFailure { get; init; }
        internal Exception? AudioFailure { get; init; }
        internal Action? BeforeAudio { get; init; }
        internal List<string> Operations { get; } = [];
        internal bool WasConfigurationRead { get; private set; }
        internal string? LoadedWeekKey { get; private set; }
        internal string? AudioInvocationId { get; private set; }
        internal CancellationToken AudioCancellationToken { get; private set; }
        internal WeeklyDvarTorahArticle? NarratedArticle { get; private set; }
        internal WeeklyDvarTorahArticle Article { get; } = new(new WeeklyDvarTorahWeek(new DateOnly(2026, 9, 5), "23 Elul", "Nitzavim", null, false), "Choose responsibility", "A complete published teaching.", "test-v1", new DateTimeOffset(2026, 9, 1, 12, 0, 0, TimeSpan.Zero), new DateTimeOffset(2026, 9, 1, 12, 0, 0, TimeSpan.Zero));

        internal DvarTorahJobApplication CreateApplication() => new(
            () =>
            {
                WasConfigurationRead = true;
                return IsGenerationEnabled;
            },
            (invocation, token) =>
            {
                token.ThrowIfCancellationRequested();
                Operations.Add("publish-text");
                return GenerationFailure is null
                    ? Task.FromResult(new WeeklyDvarTorahGenerationResult(GenerationStatus, Article.Week, ReturnPublication ? Article : null))
                    : Task.FromException<WeeklyDvarTorahGenerationResult>(GenerationFailure);
            },
            () => "test-invocation",
            () =>
            {
                WasConfigurationRead = true;
                return BackfillWeekKey;
            },
            (weekKey, token) =>
            {
                token.ThrowIfCancellationRequested();
                Operations.Add("load-published-text");
                LoadedWeekKey = weekKey;
                return Task.FromResult(ReturnPublication ? Article : null);
            },
            (article, invocationId, token) =>
            {
                Operations.Add("narrate-published-text");
                BeforeAudio?.Invoke();
                token.ThrowIfCancellationRequested();
                NarratedArticle = article;
                AudioInvocationId = invocationId;
                AudioCancellationToken = token;
                return AudioFailure is null
                    ? Task.FromResult(new WeeklyDvarTorahAudioResult(AudioStatus, null))
                    : Task.FromException<WeeklyDvarTorahAudioResult>(AudioFailure);
            });
    }
}
