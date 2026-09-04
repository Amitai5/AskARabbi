using AskARabbiLIB.DvarTorah;
using AskARabbiLIB.DvarTorah.Audio;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace AskARabbiLIB.Tests;

[TestClass]
public sealed class WeeklyDvarTorahAudioCoordinatorTests
{
    [TestMethod]
    [TestCategory("Unit")]
    public async Task RunAsync_NewVersion_GeneratesUploadsAndAttachesOnce()
    {
        var scenario = new Scenario();

        var result = await scenario.Coordinator.RunAsync(DvarTorahAudioTestData.Article(), "invocation");

        Assert.AreEqual(WeeklyDvarTorahAudioStatus.Generated, result.Status);
        Assert.AreEqual(1, scenario.NarrationCalls);
        Assert.AreEqual(1, scenario.UploadCalls);
        Assert.AreEqual(1, scenario.PublicationCalls);
        Assert.IsNull(scenario.FailureCode);
        Assert.AreEqual(DvarTorahAudioTestData.Now.AddMinutes(30), scenario.Lease?.ExpiresAtUtc);
    }

    [TestMethod]
    [TestCategory("Unit")]
    public async Task RunAsync_AlreadyGenerated_DoesNoIo()
    {
        var scenario = new Scenario();
        var article = DvarTorahAudioTestData.Article() with { Audio = DvarTorahAudioTestData.Metadata() };

        var result = await scenario.Coordinator.RunAsync(article, "invocation");

        Assert.AreEqual(WeeklyDvarTorahAudioStatus.AlreadyGenerated, result.Status);
        Assert.AreEqual(0, scenario.AcquireCalls);
        Assert.AreEqual(0, scenario.NarrationCalls);
    }

    [TestMethod]
    [TestCategory("Unit")]
    public async Task RunAsync_Disabled_DoesNotValidateCloudSettingsOrPerformIo()
    {
        var scenario = new Scenario(new DvarTorahAudioOptions());

        var result = await scenario.Coordinator.RunAsync(DvarTorahAudioTestData.Article(), "invocation");

        Assert.AreEqual(WeeklyDvarTorahAudioStatus.Disabled, result.Status);
        Assert.AreEqual(0, scenario.AcquireCalls);
    }

    [TestMethod]
    [TestCategory("Unit")]
    public async Task RunAsync_AnotherWorkerOwnsLease_DoesNotPayForSpeech()
    {
        var scenario = new Scenario { CanAcquire = false };

        var result = await scenario.Coordinator.RunAsync(DvarTorahAudioTestData.Article(), "invocation");

        Assert.AreEqual(WeeklyDvarTorahAudioStatus.GenerationInProgress, result.Status);
        Assert.AreEqual(0, scenario.NarrationCalls);
        Assert.AreEqual(0, scenario.UploadCalls);
    }

    [TestMethod]
    [TestCategory("Unit")]
    public async Task RunAsync_PreviousUploadSucceeded_RecoversWithoutSynthesizingAgain()
    {
        var scenario = new Scenario { Stored = DvarTorahAudioTestData.Metadata() };

        var result = await scenario.Coordinator.RunAsync(DvarTorahAudioTestData.Article(), "invocation");

        Assert.AreEqual(WeeklyDvarTorahAudioStatus.Generated, result.Status);
        Assert.AreEqual(0, scenario.NarrationCalls);
        Assert.AreEqual(0, scenario.UploadCalls);
        Assert.AreEqual(1, scenario.PublicationCalls);
    }

    [TestMethod]
    [TestCategory("Unit")]
    public async Task RunAsync_RecoveredWrongContent_RejectsAudioWithoutChangingText()
    {
        var scenario = new Scenario { Stored = DvarTorahAudioTestData.Metadata(), Timings = DvarTorahAudioTestData.Timings() with { Body = "Wrong body" } };

        await Assert.ThrowsExactlyAsync<InvalidDataException>(() => scenario.Coordinator.RunAsync(DvarTorahAudioTestData.Article(), "invocation"));

        Assert.AreEqual("InvalidDataException", scenario.FailureCode);
        Assert.AreEqual(0, scenario.PublicationCalls);
        Assert.AreEqual(0, scenario.NarrationCalls);
    }

    [TestMethod]
    [TestCategory("Unit")]
    public async Task RunAsync_SpeechFails_ReleasesOnlyAudioLeaseWithSafeCode()
    {
        var scenario = new Scenario { NarrationFailure = new InvalidOperationException("Secret provider details") };

        await Assert.ThrowsExactlyAsync<InvalidOperationException>(() => scenario.Coordinator.RunAsync(DvarTorahAudioTestData.Article(), "invocation"));

        Assert.AreEqual("InvalidOperationException", scenario.FailureCode);
        Assert.AreEqual(0, scenario.PublicationCalls);
        Assert.AreEqual(0, scenario.UploadCalls);
    }

    [TestMethod]
    [TestCategory("Unit")]
    public async Task RunAsync_CanceledDuringSpeech_ReleasesLeaseUsingIndependentCleanupToken()
    {
        var scenario = new Scenario { NarrationFailure = new OperationCanceledException() };

        await Assert.ThrowsExactlyAsync<OperationCanceledException>(() => scenario.Coordinator.RunAsync(DvarTorahAudioTestData.Article(), "invocation"));

        Assert.AreEqual("Canceled", scenario.FailureCode);
        Assert.IsFalse(scenario.CleanupWasCanceled);
        Assert.AreEqual(0, scenario.PublicationCalls);
    }

    [TestMethod]
    [TestCategory("Unit")]
    public async Task RunAsync_FailureAndCleanupFailure_PreservesBothExceptions()
    {
        var scenario = new Scenario { NarrationFailure = new InvalidOperationException("speech"), CleanupFails = true };

        var exception = await Assert.ThrowsExactlyAsync<AggregateException>(() => scenario.Coordinator.RunAsync(DvarTorahAudioTestData.Article(), "invocation"));

        Assert.HasCount(2, exception.InnerExceptions);
        Assert.AreEqual("speech", exception.InnerExceptions[0].Message);
    }

    [TestMethod]
    [TestCategory("Unit")]
    public async Task RunAsync_LeaseLostBeforePublication_DoesNotClaimAudioAttached()
    {
        var scenario = new Scenario { CanPublish = false };

        var result = await scenario.Coordinator.RunAsync(DvarTorahAudioTestData.Article(), "invocation");

        Assert.AreEqual(WeeklyDvarTorahAudioStatus.LostLease, result.Status);
        Assert.IsNull(result.Audio);
    }

    [TestMethod]
    [TestCategory("Unit")]
    public async Task RunAsync_WrongGeneratedVersion_DoesNotUploadOrPublish()
    {
        var scenario = new Scenario { Timings = DvarTorahAudioTestData.Timings() with { Version = new string('a', 64) } };

        await Assert.ThrowsExactlyAsync<InvalidDataException>(() => scenario.Coordinator.RunAsync(DvarTorahAudioTestData.Article(), "invocation"));

        Assert.AreEqual(0, scenario.UploadCalls);
        Assert.AreEqual(0, scenario.PublicationCalls);
    }

    [TestMethod]
    [TestCategory("Unit")]
    public async Task RunAsync_InvalidInputs_RejectsBeforeIo()
    {
        var scenario = new Scenario();

        await Assert.ThrowsExactlyAsync<ArgumentNullException>(() => scenario.Coordinator.RunAsync(null!, "invocation"));
        await Assert.ThrowsExactlyAsync<ArgumentException>(() => scenario.Coordinator.RunAsync(DvarTorahAudioTestData.Article(), " "));
        await Assert.ThrowsExactlyAsync<ArgumentException>(() => scenario.Coordinator.RunAsync(DvarTorahAudioTestData.Article(), new string('x', 161)));
        await Assert.ThrowsExactlyAsync<OperationCanceledException>(() => scenario.Coordinator.RunAsync(DvarTorahAudioTestData.Article(), "invocation", new CancellationToken(true)));

        Assert.AreEqual(0, scenario.AcquireCalls);
    }

    [TestMethod]
    [TestCategory("Unit")]
    public void Constructor_MissingCollaborator_RejectsBeforeIo()
    {
        var scenario = new Scenario();
        var options = DvarTorahAudioTestData.Options();
        var clock = new FixedTimeProvider();

        Assert.ThrowsExactly<ArgumentNullException>(() => new WeeklyDvarTorahAudioCoordinator(null!, scenario, scenario, clock, options));
        Assert.ThrowsExactly<ArgumentNullException>(() => new WeeklyDvarTorahAudioCoordinator(scenario, null!, scenario, clock, options));
        Assert.ThrowsExactly<ArgumentNullException>(() => new WeeklyDvarTorahAudioCoordinator(scenario, scenario, null!, clock, options));
        Assert.ThrowsExactly<ArgumentNullException>(() => new WeeklyDvarTorahAudioCoordinator(scenario, scenario, scenario, null!, options));
        Assert.ThrowsExactly<ArgumentNullException>(() => new WeeklyDvarTorahAudioCoordinator(scenario, scenario, scenario, clock, null!));
    }

    [TestMethod]
    [TestCategory("Unit")]
    public async Task RunAsync_TypedSpeechFailure_PersistsOnlyStableFailureCode()
    {
        var scenario = new Scenario { NarrationFailure = new DvarTorahAudioException("SpeechAuthenticationFailure", "synthesis") };

        await Assert.ThrowsExactlyAsync<DvarTorahAudioException>(() => scenario.Coordinator.RunAsync(DvarTorahAudioTestData.Article(), "invocation"));

        Assert.AreEqual("SpeechAuthenticationFailure", scenario.FailureCode);
    }

    private sealed class Scenario : IWeeklyDvarTorahAudioStore, IDvarTorahNarrator, IDvarTorahAudioStorage
    {
        public WeeklyDvarTorahAudioCoordinator Coordinator { get; }
        public bool CanAcquire { get; init; } = true;
        public bool CanPublish { get; init; } = true;
        public bool CleanupFails { get; init; }
        public bool CleanupWasCanceled { get; private set; }
        public Exception? NarrationFailure { get; init; }
        public WeeklyDvarTorahAudioMetadata? Stored { get; init; }
        public DvarTorahAudioTimings Timings { get; init; } = DvarTorahAudioTestData.Timings();
        public int AcquireCalls { get; private set; }
        public int NarrationCalls { get; private set; }
        public int UploadCalls { get; private set; }
        public int PublicationCalls { get; private set; }
        public string? FailureCode { get; private set; }
        public WeeklyDvarTorahAudioLease? Lease { get; private set; }
        public Scenario(DvarTorahAudioOptions? options = null) => Coordinator = new(this, this, this, new FixedTimeProvider(), options ?? DvarTorahAudioTestData.Options());
        public Task<WeeklyDvarTorahAudioLease?> TryAcquireAudioLeaseAsync(WeeklyDvarTorahArticle article, string version, string leaseId, DateTimeOffset acquiredAtUtc, DateTimeOffset expiresAtUtc, CancellationToken cancellationToken = default)
        {
            AcquireCalls++;
            Lease = CanAcquire ? new(article.Week.WeekKey, version, leaseId, expiresAtUtc) : null;
            return Task.FromResult(Lease);
        }
        public Task<bool> PublishAudioAsync(WeeklyDvarTorahAudioLease lease, WeeklyDvarTorahArticle article, WeeklyDvarTorahAudioMetadata audio, DateTimeOffset publishedAtUtc, CancellationToken cancellationToken = default)
        {
            PublicationCalls++;
            return Task.FromResult(CanPublish);
        }
        public Task RecordAudioFailureAsync(WeeklyDvarTorahAudioLease lease, string failureCode, DateTimeOffset failedAtUtc, CancellationToken cancellationToken = default)
        {
            FailureCode = failureCode;
            CleanupWasCanceled = cancellationToken.IsCancellationRequested;
            return CleanupFails ? Task.FromException(new IOException("database")) : Task.CompletedTask;
        }
        public Task<DvarTorahNarration> GenerateAsync(WeeklyDvarTorahArticle article, string version, CancellationToken cancellationToken = default)
        {
            NarrationCalls++;
            return NarrationFailure is null ? Task.FromResult(new DvarTorahNarration(new byte[] { 0xff, 0xfb, 0, 0 }, Timings)) : Task.FromException<DvarTorahNarration>(NarrationFailure);
        }
        public Task<WeeklyDvarTorahAudioMetadata?> FindStoredAsync(string weekKey, string version, CancellationToken cancellationToken = default) => Task.FromResult(Stored);
        public Task<WeeklyDvarTorahAudioMetadata> UploadAsync(string weekKey, DvarTorahNarration narration, DateTimeOffset createdAtUtc, CancellationToken cancellationToken = default)
        {
            UploadCalls++;
            return Task.FromResult(DvarTorahAudioTestData.Metadata());
        }
        public Task<DvarTorahAudioTimings?> GetTimingsAsync(WeeklyDvarTorahAudioMetadata audio, CancellationToken cancellationToken = default) => Task.FromResult<DvarTorahAudioTimings?>(Timings);
        public Task<DvarTorahAudioBlobInfo?> GetInfoAsync(WeeklyDvarTorahAudioMetadata audio, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<Stream> OpenReadAsync(WeeklyDvarTorahAudioMetadata audio, long offset, long? length, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }

    private sealed class FixedTimeProvider : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => DvarTorahAudioTestData.Now;
    }
}
