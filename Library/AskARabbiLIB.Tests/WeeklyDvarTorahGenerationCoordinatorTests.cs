using AskARabbiLIB.Calendar;
using AskARabbiLIB.DvarTorah;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace AskARabbiLIB.Tests;

[TestClass]
public sealed class WeeklyDvarTorahGenerationCoordinatorTests
{
    private static readonly DateTimeOffset CurrentUtc = new(2026, 8, 31, 18, 0, 0, TimeSpan.Zero);
    private static readonly WeeklyDvarTorahWeek Week = new(new DateOnly(2026, 9, 5), "23 Elul, 5786", "Nitzavim-Vayeilech", null, false);

    [TestMethod]
    [TestCategory("Unit")]
    public async Task Run_ArticleAlreadyPublished_SkipsLeaseAndGenerator()
    {
        var article = CreateArticle();
        var store = new GenerationStore { Published = article };
        var generator = new RecordingGenerator();
        var coordinator = CreateCoordinator(store, generator);

        var result = await coordinator.RunAsync("invocation-1");

        Assert.AreEqual(WeeklyDvarTorahGenerationStatus.AlreadyPublished, result.Status);
        Assert.AreEqual(article, result.Article);
        Assert.AreEqual(0, store.AcquireCalls);
        Assert.AreEqual(0, generator.Calls);
    }

    [TestMethod]
    [TestCategory("Unit")]
    public async Task Run_AnotherInvocationOwnsLease_ReturnsInProgress()
    {
        var store = new GenerationStore { CanAcquire = false };
        var generator = new RecordingGenerator();
        var coordinator = CreateCoordinator(store, generator);

        var result = await coordinator.RunAsync("invocation-2");

        Assert.AreEqual(WeeklyDvarTorahGenerationStatus.GenerationInProgress, result.Status);
        Assert.IsNull(result.Article);
        Assert.AreEqual(0, generator.Calls);
    }

    [TestMethod]
    [TestCategory("Unit")]
    public async Task Run_LeaseAcquired_PublishesValidatedDraft()
    {
        var store = new GenerationStore();
        var generator = new RecordingGenerator();
        var coordinator = CreateCoordinator(store, generator);

        var result = await coordinator.RunAsync("invocation-3");

        Assert.AreEqual(WeeklyDvarTorahGenerationStatus.Published, result.Status);
        Assert.IsNotNull(result.Article);
        Assert.AreEqual("A weekly teaching", result.Article.Title);
        Assert.AreEqual("test-v1", result.Article.GeneratorVersion);
        Assert.AreEqual(1, store.PublishCalls);
        Assert.AreEqual(1, generator.Calls);
    }

    [TestMethod]
    [TestCategory("Unit")]
    public async Task Run_GeneratorFails_ReleasesLeaseWithSafeFailureCode()
    {
        var store = new GenerationStore();
        var generator = new RecordingGenerator { Exception = new InvalidOperationException("Sensitive provider detail") };
        var coordinator = CreateCoordinator(store, generator);

        await Assert.ThrowsExactlyAsync<InvalidOperationException>(() => coordinator.RunAsync("invocation-4"));

        Assert.AreEqual("InvalidOperationException", store.FailureCode);
        Assert.IsFalse(store.FailureCode?.Contains("Sensitive", StringComparison.Ordinal) ?? true);
    }

    [TestMethod]
    [TestCategory("Unit")]
    public async Task Run_GroundedGenerationFails_PersistsStableStageCode()
    {
        var store = new GenerationStore();
        var generator = new RecordingGenerator { Exception = new WeeklyDvarTorahGenerationException("ResearchSelectionInvalid", "Sensitive model detail") };
        var coordinator = CreateCoordinator(store, generator);

        await Assert.ThrowsExactlyAsync<WeeklyDvarTorahGenerationException>(() => coordinator.RunAsync("invocation-stage"));

        Assert.AreEqual("ResearchSelectionInvalid", store.FailureCode);
        Assert.IsFalse(store.FailureCode?.Contains("Sensitive", StringComparison.Ordinal) ?? false);
    }

    [TestMethod]
    [TestCategory("Unit")]
    public async Task Run_PublicationLosesLease_RecordsFailureAndThrows()
    {
        var store = new GenerationStore { CanPublish = false };
        var coordinator = CreateCoordinator(store, new RecordingGenerator());

        var exception = await Assert.ThrowsExactlyAsync<InvalidOperationException>(() => coordinator.RunAsync("invocation-5"));

        StringAssert.Contains(exception.Message, "lease expired");
        Assert.AreEqual("InvalidOperationException", store.FailureCode);
    }

    [TestMethod]
    [TestCategory("Unit")]
    public async Task Run_CallerCancels_DoesNotConvertCancellationIntoFailureState()
    {
        var store = new GenerationStore();
        var generator = new RecordingGenerator { Cancel = true };
        var coordinator = CreateCoordinator(store, generator);
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        await Assert.ThrowsExactlyAsync<OperationCanceledException>(() => coordinator.RunAsync("invocation-6", cancellation.Token));

        Assert.IsNull(store.FailureCode);
    }

    [TestMethod]
    [DataRow("")]
    [DataRow("   ")]
    [TestCategory("Unit")]
    public async Task Run_InvalidInvocationId_Throws(string invocationId)
    {
        var coordinator = CreateCoordinator(new GenerationStore(), new RecordingGenerator());

        await Assert.ThrowsExactlyAsync<ArgumentException>(() => coordinator.RunAsync(invocationId));
    }

    [TestMethod]
    [TestCategory("Unit")]
    public async Task Run_OverlongInvocationId_Throws()
    {
        var coordinator = CreateCoordinator(new GenerationStore(), new RecordingGenerator());

        await Assert.ThrowsExactlyAsync<ArgumentException>(() => coordinator.RunAsync(new string('x', 161)));
    }

    [TestMethod]
    [TestCategory("Unit")]
    public async Task Run_GenerationAndFailureRecordingBothFail_ThrowsAggregateWithBothErrors()
    {
        var store = new GenerationStore { FailureException = new InvalidOperationException("Persistence failed") };
        var generator = new RecordingGenerator { Exception = new ArgumentException("Generation failed") };
        var coordinator = CreateCoordinator(store, generator);

        var exception = await Assert.ThrowsExactlyAsync<AggregateException>(() => coordinator.RunAsync("invocation-7"));

        Assert.HasCount(2, exception.InnerExceptions);
    }

    [TestMethod]
    [TestCategory("Unit")]
    public void Constructor_MissingDependency_Throws()
    {
        var store = new GenerationStore();
        var generator = new RecordingGenerator();
        var options = new WeeklyDvarTorahOptions();
        var time = new FixedTimeProvider(CurrentUtc);
        var calendar = new StaticCalendar(new WeeklyParashahInfo(Week.ShabbatDate, Week.ShabbatDate, Week.Parashah, null, Week.HebrewDate, false, "Test"));
        var service = new WeeklyDvarTorahService(calendar, store, time, options);

        Assert.ThrowsExactly<ArgumentNullException>(() => new WeeklyDvarTorahGenerationCoordinator(null!, generator, service, time, options));
        Assert.ThrowsExactly<ArgumentNullException>(() => new WeeklyDvarTorahGenerationCoordinator(store, null!, service, time, options));
        Assert.ThrowsExactly<ArgumentNullException>(() => new WeeklyDvarTorahGenerationCoordinator(store, generator, null!, time, options));
        Assert.ThrowsExactly<ArgumentNullException>(() => new WeeklyDvarTorahGenerationCoordinator(store, generator, service, null!, options));
        Assert.ThrowsExactly<ArgumentNullException>(() => new WeeklyDvarTorahGenerationCoordinator(store, generator, service, time, null!));
    }

    private static WeeklyDvarTorahGenerationCoordinator CreateCoordinator(GenerationStore store, RecordingGenerator generator)
    {
        var options = new WeeklyDvarTorahOptions { GenerationLeaseMinutes = 30 };
        var timeProvider = new FixedTimeProvider(CurrentUtc);
        var calendar = new StaticCalendar(new WeeklyParashahInfo(Week.ShabbatDate, Week.ShabbatDate, Week.Parashah, Week.Holiday, Week.HebrewDate, Week.InIsrael, "Test"));
        var service = new WeeklyDvarTorahService(calendar, store, timeProvider, options);
        return new WeeklyDvarTorahGenerationCoordinator(store, generator, service, timeProvider, options);
    }

    private static WeeklyDvarTorahArticle CreateArticle() => new(Week, "A weekly teaching", "First paragraph.", "test-v1", CurrentUtc, CurrentUtc);

    private sealed class RecordingGenerator : IWeeklyDvarTorahGenerator
    {
        internal Exception? Exception { get; init; }

        internal bool Cancel { get; init; }

        internal int Calls { get; private set; }

        public Task<WeeklyDvarTorahDraft> GenerateAsync(WeeklyDvarTorahWeek week, CancellationToken cancellationToken = default)
        {
            Calls++;
            if (Cancel)
            {
                cancellationToken.ThrowIfCancellationRequested();
            }
            return Exception is null
                ? Task.FromResult(new WeeklyDvarTorahDraft("A weekly teaching", "First paragraph.", "test-v1"))
                : Task.FromException<WeeklyDvarTorahDraft>(Exception);
        }
    }

    private sealed class GenerationStore : IWeeklyDvarTorahGenerationStore
    {
        internal WeeklyDvarTorahArticle? Published { get; set; }

        internal bool CanAcquire { get; init; } = true;

        internal bool CanPublish { get; init; } = true;

        internal int AcquireCalls { get; private set; }

        internal int PublishCalls { get; private set; }

        internal string? FailureCode { get; private set; }

        internal Exception? FailureException { get; init; }

        public Task<WeeklyDvarTorahArticle?> GetPublishedAsync(WeeklyDvarTorahWeek week, CancellationToken cancellationToken = default) => Task.FromResult(Published);

        public Task<WeeklyDvarTorahArticle?> GetLatestPublishedAsync(bool inIsrael, DateOnly notAfter, CancellationToken cancellationToken = default) => Task.FromResult(Published);

        public Task<WeeklyDvarTorahArticle?> GetPublishedByWeekKeyAsync(string weekKey, CancellationToken cancellationToken = default) => Task.FromResult(Published?.Week.WeekKey == weekKey ? Published : null);

        public Task<WeeklyDvarTorahArchiveResult> SearchPublishedAsync(bool inIsrael, DateOnly before, string? search, int skip, int limit, CancellationToken cancellationToken = default) => Task.FromResult(new WeeklyDvarTorahArchiveResult([], 0));

        public Task<WeeklyDvarTorahGenerationLease?> TryAcquireGenerationLeaseAsync(WeeklyDvarTorahWeek week, string leaseId, DateTimeOffset acquiredAtUtc, DateTimeOffset expiresAtUtc, CancellationToken cancellationToken = default)
        {
            AcquireCalls++;
            return Task.FromResult(CanAcquire ? new WeeklyDvarTorahGenerationLease(week, leaseId, expiresAtUtc) : null);
        }

        public Task<bool> PublishAsync(WeeklyDvarTorahGenerationLease lease, WeeklyDvarTorahArticle article, CancellationToken cancellationToken = default)
        {
            PublishCalls++;
            if (CanPublish)
            {
                Published = article;
            }
            return Task.FromResult(CanPublish);
        }

        public Task RecordGenerationFailureAsync(WeeklyDvarTorahGenerationLease lease, string failureCode, DateTimeOffset failedAtUtc, CancellationToken cancellationToken = default)
        {
            if (FailureException is not null)
            {
                return Task.FromException(FailureException);
            }
            FailureCode = failureCode;
            return Task.CompletedTask;
        }
    }

    private sealed class StaticCalendar : IHebrewCalendarService
    {
        private readonly WeeklyParashahInfo result;

        internal StaticCalendar(WeeklyParashahInfo result)
        {
            this.result = result;
        }

        public HebrewDateInfo ConvertToHebrew(DateTime gregorianDateTime, bool occurredAfterSunset = false) => throw new NotSupportedException();

        public WeeklyParashahInfo FindParashahForWeek(DateTime dateTime, bool inIsrael = false) => result;

        public WeeklyParashahInfo FindHebrewAnniversaryParashah(DateTime birthDateTime, int anniversaryAge, bool inIsrael = false, bool occurredAfterSunset = false) => throw new NotSupportedException();
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
}
