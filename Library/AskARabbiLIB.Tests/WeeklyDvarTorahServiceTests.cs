using AskARabbiLIB.Calendar;
using AskARabbiLIB.DvarTorah;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace AskARabbiLIB.Tests;

[TestClass]
public sealed class WeeklyDvarTorahServiceTests
{
    private static readonly DateTimeOffset CurrentUtc = new(2026, 8, 31, 18, 0, 0, TimeSpan.Zero);
    private static readonly WeeklyDvarTorahWeek CurrentWeek = new(new DateOnly(2026, 9, 5), "23 Elul, 5786", "Nitzavim-Vayeilech", null, false);

    [TestMethod]
    [TestCategory("Unit")]
    public async Task GetCurrentOrLatest_CurrentPublicationExists_ReturnsCurrentWithoutFallbackQuery()
    {
        var article = CreateArticle(CurrentWeek);
        var store = new RecordingStore { Current = article, Latest = CreateArticle(CreatePreviousWeek()) };
        var service = CreateService(store);

        var result = await service.GetCurrentOrLatestAsync();

        Assert.IsTrue(result.IsCurrentWeek);
        Assert.AreEqual(article, result.Article);
        Assert.AreEqual(1, store.CurrentCalls);
        Assert.AreEqual(0, store.LatestCalls);
    }

    [TestMethod]
    [TestCategory("Unit")]
    public async Task GetCurrentOrLatest_CurrentPublicationMissing_ReturnsLatestEarlierPublication()
    {
        var previous = CreateArticle(CreatePreviousWeek());
        var store = new RecordingStore { Latest = previous };
        var service = CreateService(store);

        var result = await service.GetCurrentOrLatestAsync();

        Assert.IsFalse(result.IsCurrentWeek);
        Assert.AreEqual(previous, result.Article);
        Assert.AreEqual(CurrentWeek.ShabbatDate, store.LatestNotAfter);
    }

    [TestMethod]
    [TestCategory("Unit")]
    public async Task GetCurrentOrLatest_NoPublications_ReturnsCurrentWeekWithEmptyArticle()
    {
        var service = CreateService(new RecordingStore());

        var result = await service.GetCurrentOrLatestAsync();

        Assert.AreEqual(CurrentWeek, result.CurrentWeek);
        Assert.IsNull(result.Article);
        Assert.IsFalse(result.IsCurrentWeek);
    }

    [TestMethod]
    [TestCategory("Unit")]
    public async Task SearchArchive_ValidQuery_NormalizesAndForwardsPastWeekBounds()
    {
        var archive = new WeeklyDvarTorahArchiveResult([], 12);
        var store = new RecordingStore { Archive = archive };
        var service = CreateService(store);

        var result = await service.SearchArchiveAsync("  responsibility  ", 2, 10);

        Assert.AreEqual(archive, result);
        Assert.IsFalse(store.ArchiveInIsrael);
        Assert.AreEqual(CurrentWeek.ShabbatDate, store.ArchiveBefore);
        Assert.AreEqual("responsibility", store.ArchiveSearch);
        Assert.AreEqual(10, store.ArchiveSkip);
        Assert.AreEqual(10, store.ArchiveLimit);
    }

    [TestMethod]
    [TestCategory("Unit")]
    public void SearchArchive_InvalidQuery_ThrowsBeforeStoreCall()
    {
        var store = new RecordingStore();
        var service = CreateService(store);

        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => service.SearchArchiveAsync(null, 0, 10));
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => service.SearchArchiveAsync(null, 1, 51));
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => service.SearchArchiveAsync(null, int.MaxValue, 10));
        Assert.ThrowsExactly<ArgumentException>(() => service.SearchArchiveAsync(new string('x', WeeklyDvarTorahService.MaximumArchiveSearchCharacters + 1)));
        Assert.AreEqual(0, store.ArchiveCalls);
    }

    [TestMethod]
    [TestCategory("Unit")]
    public async Task GetArchived_PastConfiguredCycle_LoadsArticleWhileInvalidKeysDoNotQueryStore()
    {
        var previous = CreateArticle(CreatePreviousWeek());
        var store = new RecordingStore { Archived = previous };
        var service = CreateService(store);

        var result = await service.GetArchivedAsync(previous.Week.WeekKey);
        var currentResult = await service.GetArchivedAsync(CurrentWeek.WeekKey);
        var otherCycleResult = await service.GetArchivedAsync("israel:2026-08-29");

        Assert.AreEqual(previous, result);
        Assert.IsNull(currentResult);
        Assert.IsNull(otherCycleResult);
        Assert.AreEqual(1, store.ArchivedCalls);
        Assert.AreEqual(previous.Week.WeekKey, store.ArchivedWeekKey);
    }

    [TestMethod]
    [TestCategory("Unit")]
    public void GetCurrentWeek_ConfiguredIsraelCycle_UsesUtcCivilDateAndIsraelReading()
    {
        var calendar = new RecordingCalendar(new WeeklyParashahInfo(CurrentWeek.ShabbatDate, CurrentWeek.ShabbatDate, "Nitzavim", null, CurrentWeek.HebrewDate, true, "Test"));
        var service = new WeeklyDvarTorahService(calendar, new RecordingStore(), new FixedTimeProvider(CurrentUtc), new WeeklyDvarTorahOptions { InIsrael = true });

        var result = service.GetCurrentWeek();

        Assert.AreEqual(CurrentUtc.UtcDateTime, calendar.RequestedDateTime);
        Assert.IsTrue(calendar.RequestedInIsrael);
        Assert.IsTrue(result.InIsrael);
    }

    [TestMethod]
    [DataRow(0)]
    [DataRow(121)]
    [TestCategory("Unit")]
    public void Validate_InvalidGenerationLease_Throws(int minutes)
    {
        var options = new WeeklyDvarTorahOptions { GenerationLeaseMinutes = minutes };

        Assert.ThrowsExactly<InvalidOperationException>(options.Validate);
    }

    [TestMethod]
    [TestCategory("Unit")]
    public void WeeklyDvarTorahDraft_OverlongBody_Throws()
    {
        var body = new string('x', WeeklyDvarTorahDraft.MaximumBodyCharacters + 1);

        Assert.ThrowsExactly<ArgumentException>(() => new WeeklyDvarTorahDraft("Title", body, "test-v1"));
    }

    [TestMethod]
    [TestCategory("Unit")]
    public void WeeklyDvarTorahWeek_InvalidCalendarIdentity_Throws()
    {
        Assert.ThrowsExactly<ArgumentException>(() => new WeeklyDvarTorahWeek(new DateOnly(2026, 9, 4), "23 Elul, 5786", "Nitzavim", null, false));
        Assert.ThrowsExactly<ArgumentException>(() => new WeeklyDvarTorahWeek(CurrentWeek.ShabbatDate, " ", "Nitzavim", null, false));
        Assert.ThrowsExactly<ArgumentException>(() => WeeklyDvarTorahWeek.CreateWeekKey(new DateOnly(2026, 9, 4), false));
        Assert.ThrowsExactly<ArgumentNullException>(() => WeeklyDvarTorahWeek.FromParashah(null!));
    }

    [TestMethod]
    [TestCategory("Unit")]
    public void WeeklyDvarTorahDraft_InvalidRequiredFields_Throws()
    {
        Assert.ThrowsExactly<ArgumentException>(() => new WeeklyDvarTorahDraft(" ", "Body", "test-v1"));
        Assert.ThrowsExactly<ArgumentException>(() => new WeeklyDvarTorahDraft(new string('x', WeeklyDvarTorahDraft.MaximumTitleCharacters + 1), "Body", "test-v1"));
        Assert.ThrowsExactly<ArgumentException>(() => new WeeklyDvarTorahDraft("Title", "Body", new string('x', 121)));
    }

    [TestMethod]
    [TestCategory("Unit")]
    public void WeeklyDvarTorahArticle_InvalidPublicationIdentity_Throws()
    {
        Assert.ThrowsExactly<ArgumentNullException>(() => new WeeklyDvarTorahArticle(null!, "Title", "Body", "test-v1", CurrentUtc, CurrentUtc));
        Assert.ThrowsExactly<ArgumentException>(() => new WeeklyDvarTorahArticle(CurrentWeek, "Title", "Body", "test-v1", CurrentUtc, CurrentUtc.AddMinutes(-1)));
    }

    [TestMethod]
    [TestCategory("Unit")]
    public void Constructor_MissingDependency_Throws()
    {
        var calendar = new RecordingCalendar(new WeeklyParashahInfo(CurrentWeek.ShabbatDate, CurrentWeek.ShabbatDate, CurrentWeek.Parashah, null, CurrentWeek.HebrewDate, false, "Test"));
        var store = new RecordingStore();
        var time = new FixedTimeProvider(CurrentUtc);
        var options = new WeeklyDvarTorahOptions();

        Assert.ThrowsExactly<ArgumentNullException>(() => new WeeklyDvarTorahService(null!, store, time, options));
        Assert.ThrowsExactly<ArgumentNullException>(() => new WeeklyDvarTorahService(calendar, null!, time, options));
        Assert.ThrowsExactly<ArgumentNullException>(() => new WeeklyDvarTorahService(calendar, store, null!, options));
        Assert.ThrowsExactly<ArgumentNullException>(() => new WeeklyDvarTorahService(calendar, store, time, null!));
    }

    private static WeeklyDvarTorahService CreateService(RecordingStore store)
    {
        var calendarResult = new WeeklyParashahInfo(CurrentWeek.ShabbatDate, CurrentWeek.ShabbatDate, CurrentWeek.Parashah, CurrentWeek.Holiday, CurrentWeek.HebrewDate, CurrentWeek.InIsrael, "Test");
        return new WeeklyDvarTorahService(new RecordingCalendar(calendarResult), store, new FixedTimeProvider(CurrentUtc), new WeeklyDvarTorahOptions());
    }

    private static WeeklyDvarTorahArticle CreateArticle(WeeklyDvarTorahWeek week) => new(week, "A weekly teaching", "First paragraph.\n\nSecond paragraph.", "test-v1", CurrentUtc, CurrentUtc);

    private static WeeklyDvarTorahWeek CreatePreviousWeek() => new(new DateOnly(2026, 8, 29), "16 Elul, 5786", "Ki Teitzei", null, false);

    private sealed class RecordingCalendar : IHebrewCalendarService
    {
        private readonly WeeklyParashahInfo result;

        internal RecordingCalendar(WeeklyParashahInfo result)
        {
            this.result = result;
        }

        internal DateTime RequestedDateTime { get; private set; }

        internal bool RequestedInIsrael { get; private set; }

        public HebrewDateInfo ConvertToHebrew(DateTime gregorianDateTime, bool occurredAfterSunset = false) => throw new NotSupportedException();

        public WeeklyParashahInfo FindParashahForWeek(DateTime dateTime, bool inIsrael = false)
        {
            RequestedDateTime = dateTime;
            RequestedInIsrael = inIsrael;
            return result;
        }

        public WeeklyParashahInfo FindHebrewAnniversaryParashah(DateTime birthDateTime, int anniversaryAge, bool inIsrael = false, bool occurredAfterSunset = false) => throw new NotSupportedException();
    }

    private sealed class RecordingStore : IWeeklyDvarTorahStore
    {
        internal WeeklyDvarTorahArticle? Current { get; init; }

        internal WeeklyDvarTorahArticle? Latest { get; init; }

        internal WeeklyDvarTorahArticle? Archived { get; init; }

        internal WeeklyDvarTorahArchiveResult Archive { get; init; } = new([], 0);

        internal int CurrentCalls { get; private set; }

        internal int LatestCalls { get; private set; }

        internal DateOnly LatestNotAfter { get; private set; }

        internal int ArchivedCalls { get; private set; }

        internal string? ArchivedWeekKey { get; private set; }

        internal int ArchiveCalls { get; private set; }

        internal bool ArchiveInIsrael { get; private set; }

        internal DateOnly ArchiveBefore { get; private set; }

        internal string? ArchiveSearch { get; private set; }

        internal int ArchiveSkip { get; private set; }

        internal int ArchiveLimit { get; private set; }

        public Task<WeeklyDvarTorahArticle?> GetPublishedAsync(WeeklyDvarTorahWeek week, CancellationToken cancellationToken = default)
        {
            CurrentCalls++;
            return Task.FromResult(Current?.Week.WeekKey == week.WeekKey ? Current : null);
        }

        public Task<WeeklyDvarTorahArticle?> GetLatestPublishedAsync(bool inIsrael, DateOnly notAfter, CancellationToken cancellationToken = default)
        {
            LatestCalls++;
            LatestNotAfter = notAfter;
            return Task.FromResult(Latest);
        }

        public Task<WeeklyDvarTorahArticle?> GetPublishedByWeekKeyAsync(string weekKey, CancellationToken cancellationToken = default)
        {
            ArchivedCalls++;
            ArchivedWeekKey = weekKey;
            return Task.FromResult(Archived?.Week.WeekKey == weekKey ? Archived : null);
        }

        public Task<WeeklyDvarTorahArchiveResult> SearchPublishedAsync(bool inIsrael, DateOnly before, string? search, int skip, int limit, CancellationToken cancellationToken = default)
        {
            ArchiveCalls++;
            ArchiveInIsrael = inIsrael;
            ArchiveBefore = before;
            ArchiveSearch = search;
            ArchiveSkip = skip;
            ArchiveLimit = limit;
            return Task.FromResult(Archive);
        }
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
