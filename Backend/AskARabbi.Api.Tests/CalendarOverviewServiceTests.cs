using AskARabbi.Api.Calendar;
using AskARabbiLIB.Calendar;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace AskARabbi.Api.Tests;

[TestClass]
public sealed class CalendarOverviewServiceTests
{
    private static readonly Guid Owner = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private readonly InMemoryApplicationStore store = new();
    private readonly FakeCalendarProvider provider = new();
    private readonly MutableCalendarClock clock = new();

    [TestMethod]
    public async Task GetAsync_NoLocationOrProvider_ReturnsDaytimeDatesAndFestivalReading()
    {
        provider.Holidays = FakeCalendarProvider.Result(null);

        var result = await CreateService().GetAsync(Owner, 90, default);

        Assert.AreEqual(new DateOnly(2026, 9, 10), result.Today.GregorianDate);
        Assert.IsTrue(result.Today.IsDaytimeOnly);
        Assert.IsFalse(result.Holidays.IsAvailable);
        Assert.IsFalse(result.Timing.IsAvailable);
        Assert.AreEqual(new DateOnly(2026, 9, 12), result.Shabbat.ShabbatDate);
        Assert.IsNull(result.Shabbat.Parashah);
        Assert.IsNotNull(result.Shabbat.Holiday);
        Assert.HasCount(0, provider.TimingRequests);
        Assert.IsTrue(result.NextRefreshAtUtc > clock.Now);
    }

    [TestMethod]
    [DataRow("2026-09-11T02:06:59Z", false)]
    [DataRow("2026-09-11T02:07:00Z", true)]
    public async Task GetAsync_SunsetBoundary_AdvancesOnlyHebrewDate(string now, bool advanced)
    {
        await SetLocationAsync("5368361");
        clock.Now = DateTimeOffset.Parse(now);
        provider.Solar = SolarDay("2026-09-10", "2026-09-10T19:07:00-07:00", "2026-09-10T19:44:00-07:00");

        var result = await CreateService().GetAsync(Owner, 30, default);

        Assert.AreEqual(new DateOnly(2026, 9, 10), result.Today.GregorianDate);
        Assert.AreEqual(advanced, result.Today.IsAfterSunset);
        var expected = new HebrewCalendarService().ConvertToHebrew(new(2026, 9, 10), advanced);
        Assert.AreEqual(expected.EnglishText, result.Today.HebrewDate);
        Assert.AreEqual(expected.HebrewText, result.Today.HebrewScript);
        Assert.IsFalse(result.Today.IsDaytimeOnly);
    }

    [TestMethod]
    [DataRow("2026-09-10T06:59:59Z", "2026-09-09")]
    [DataRow("2026-09-10T07:00:00Z", "2026-09-10")]
    [DataRow("2026-03-08T09:59:59Z", "2026-03-08")]
    [DataRow("2026-03-08T10:00:00Z", "2026-03-08")]
    [DataRow("2026-11-01T08:59:59Z", "2026-11-01")]
    [DataRow("2026-11-01T09:00:00Z", "2026-11-01")]
    public async Task GetAsync_LocalMidnightAndDst_UsesSelectedZone(string now, string expected)
    {
        await SetLocationAsync("5368361");
        clock.Now = DateTimeOffset.Parse(now);

        var result = await CreateService().GetAsync(Owner, 30, default);

        Assert.AreEqual(DateOnly.Parse(expected), result.Today.GregorianDate);
        Assert.AreEqual("America/Los_Angeles", result.Today.TimeZone);
        Assert.IsTrue(result.NextRefreshAtUtc > clock.Now);
    }

    [TestMethod]
    public async Task GetAsync_CachedSolarTimesWithLocalTimesDisabled_ExposesBoundaryFreshness()
    {
        await store.UpsertCalendarPreferencesAsync(Owner, new() { Location = CalendarLocationCatalog.Cities.Single(city => city.Id == "5368361"), ShowLocalTimes = false }, clock.Now);
        provider.Solar = SolarDay("2026-09-10", "2026-09-10T19:07:00-07:00", "2026-09-10T19:44:00-07:00") with { IsStale = true, Problem = "unavailable" };

        var result = await CreateService().GetAsync(Owner, 30, default);

        Assert.IsNotNull(result.Today.SolarData);
        Assert.IsTrue(result.Today.SolarData.IsStale);
        Assert.IsTrue(result.Today.SolarData.IsAvailable);
        Assert.AreEqual(provider.Solar.FetchedAtUtc, result.Today.SolarData.FetchedAtUtc);
        StringAssert.Contains(result.Today.SolarData.Message, "saved solar times");
        Assert.HasCount(0, provider.TimingRequests);
    }

    [TestMethod]
    [DataRow("2026-09-06T02:43:59Z", "2026-09-05")]
    [DataRow("2026-09-06T02:44:00Z", "2026-09-12")]
    public async Task GetAsync_SaturdayNightfall_ChangesUpcomingReading(string now, string expected)
    {
        await SetLocationAsync("5368361");
        clock.Now = DateTimeOffset.Parse(now);
        provider.Solar = SolarDay("2026-09-05", "2026-09-05T19:00:00-07:00", "2026-09-05T19:44:00-07:00");

        var result = await CreateService().GetAsync(Owner, 90, default);

        Assert.AreEqual(DateOnly.Parse(expected), result.Shabbat.ShabbatDate);
    }

    [TestMethod]
    public async Task GetAsync_IsraelDiasporaDifference_UsesLibraryAndProviderCycle()
    {
        clock.Now = DateTimeOffset.Parse("2022-04-22T12:00:00Z");
        var diaspora = await CreateService().GetAsync(Owner, 30, default);
        await store.UpsertCalendarPreferencesAsync(Owner, new() { InIsrael = true }, clock.Now);

        var israel = await CreateService().GetAsync(Owner, 30, default);

        Assert.IsNull(diaspora.Shabbat.Parashah);
        Assert.IsNotNull(israel.Shabbat.Parashah);
        Assert.IsTrue(provider.HolidayRequests.Any(value => value.InIsrael));
        Assert.IsTrue(provider.HolidayRequests.Any(value => !value.InIsrael));
    }

    [TestMethod]
    public async Task GetAsync_LeapYearAndCrossYear_KeepsSeparateMonthlyOccurrences()
    {
        clock.Now = DateTimeOffset.Parse("2027-12-28T12:00:00Z");
        provider.Holidays = Holidays(FakeCalendarProvider.Holiday("Rosh Chodesh Adar I", "2028-01-28", category: "roshchodesh"), FakeCalendarProvider.Holiday("Rosh Chodesh Adar II", "2028-02-27", category: "roshchodesh"));

        var result = await CreateService().GetAsync(Owner, 365, default);

        CollectionAssert.AreEquivalent(new[] { 2027, 2028, 2029 }, provider.HolidayRequests.Select(value => value.Year).ToArray());
        Assert.HasCount(2, result.Events);
        Assert.AreNotEqual(result.Events[0].Kind, result.Events[1].Kind);
        StringAssert.Contains(result.Today.HebrewDate, "5788");
    }

    [TestMethod]
    public async Task GetAsync_OngoingMultiDayHoliday_PreservesBeginningAndDistinctAssociatedHolidays()
    {
        clock.Now = DateTimeOffset.Parse("2026-09-28T12:00:00Z");
        provider.Holidays = Holidays(
            FakeCalendarProvider.Holiday("Erev Sukkot", "2026-09-25"),
            FakeCalendarProvider.Holiday("Sukkot I", "2026-09-26"),
            FakeCalendarProvider.Holiday("Sukkot II", "2026-09-27"),
            FakeCalendarProvider.Holiday("Sukkot III (CH’’M)", "2026-09-28"),
            FakeCalendarProvider.Holiday("Sukkot VII (Hoshana Raba)", "2026-10-02"),
            FakeCalendarProvider.Holiday("Shmini Atzeret", "2026-10-03"),
            FakeCalendarProvider.Holiday("Simchat Torah", "2026-10-04"));

        var result = await CreateService().GetAsync(Owner, 90, default);

        Assert.HasCount(4, result.Events);
        Assert.IsNotNull(result.Highlight);
        Assert.AreEqual("sukkot", result.Highlight.Kind);
        Assert.IsTrue(result.Highlight.IsOngoing);
        Assert.HasCount(3, result.Highlight.Occurrences);
        Assert.AreEqual(new DateOnly(2026, 9, 25), result.Highlight.BeginningDate);
    }

    [TestMethod]
    public async Task GetAsync_CompletedHoliday_RemovesAfterNightfallNotMidnight()
    {
        await SetLocationAsync("5368361");
        clock.Now = DateTimeOffset.Parse("2026-09-14T02:45:00Z");
        provider.Solar = SolarDay("2026-09-13", "2026-09-13T19:00:00-07:00", "2026-09-13T19:44:00-07:00");
        provider.Holidays = Holidays(FakeCalendarProvider.Holiday("Rosh Hashana II", "2026-09-13"));

        var result = await CreateService().GetAsync(Owner, 90, default);

        Assert.HasCount(0, result.Events);
    }

    [TestMethod]
    public async Task GetAsync_ConsecutiveFestivalTransitions_PreservesProviderInstantsAndLocation()
    {
        await SetLocationAsync("281184");
        provider.Local = Holidays(
            new("Candle lighting: 18:10", new(2026, 9, 11), DateTimeOffset.Parse("2026-09-11T18:10:00+03:00"), "candles", null, null, "Erev Rosh Hashana"),
            new("Candle lighting: 19:26", new(2026, 9, 12), DateTimeOffset.Parse("2026-09-12T19:26:00+03:00"), "candles", null, null, "Rosh Hashana I"),
            new("Havdalah", new(2026, 9, 13), DateTimeOffset.Parse("2026-09-13T19:24:00+03:00"), "havdalah", null, null, "Rosh Hashana II"));

        var result = await CreateService().GetAsync(Owner, 90, default);

        Assert.HasCount(3, result.LocalTimes);
        Assert.AreEqual("Asia/Jerusalem", result.LocalTimes[1].TimeZone);
        Assert.AreEqual(TimeSpan.FromHours(3), result.LocalTimes[1].At.Offset);
        Assert.AreEqual(19, result.LocalTimes[1].At.Hour);
        StringAssert.Contains(result.TimingConvention, "40 minutes");
    }

    [TestMethod]
    public async Task GetAsync_FiltersAndRepeatedAnnualHoliday_AppliesPreferencesWithoutMergingYears()
    {
        provider.Holidays = Holidays(FakeCalendarProvider.Holiday("Tu BiShvat", "2026-09-12", "minor"), FakeCalendarProvider.Holiday("Tu BiShvat", "2027-09-01", "minor"), FakeCalendarProvider.Holiday("Shabbat Shuva", "2026-09-19", "shabbat"));
        var result = await CreateService().GetAsync(Owner, 365, default);
        Assert.HasCount(2, result.Events);
        Assert.AreNotEqual(result.Events[0].Id, result.Events[1].Id);
        await store.UpsertCalendarPreferencesAsync(Owner, new() { MinorHolidays = false, SpecialShabbatot = true }, clock.Now);
        var filtered = await CreateService().GetAsync(Owner, 90, default);
        Assert.HasCount(1, filtered.Events);
        Assert.AreEqual("specialShabbat", filtered.Events[0].Category);
    }

    [TestMethod]
    [DataRow("Tzom Gedaliah", "fast", "dawn")]
    [DataRow("Tish’a B’Av", "major", "previousSunset")]
    [DataRow("Yom HaAtzma’ut", "modern", "civilDate")]
    [DataRow("Leil Selichot", "minor", "nightfall")]
    [DataRow("Chanukah: 1 Candle", "major", "sameEvening")]
    public void Describe_EventKinds_UsesSpecificBeginning(string title, string category, string expected)
    {
        var description = CalendarEventDescriptions.Describe(FakeCalendarProvider.Holiday(title, "2026-09-10", category));
        Assert.AreEqual(expected, description.Beginning);
    }

    private CalendarOverviewService CreateService() => new(new(store, provider, clock), new HebrewCalendarService(), provider, clock);
    private Task SetLocationAsync(string id) => store.UpsertCalendarPreferencesAsync(Owner, new() { Location = CalendarLocationCatalog.Cities.Single(city => city.Id == id) }, clock.Now);
    private static CalendarProviderResult Holidays(params HebcalData.Event[] events) => FakeCalendarProvider.Result(new(events, new Dictionary<DateOnly, HebcalData.SunTimes>(), null));
    private static CalendarProviderResult SolarDay(string date, string sunset, string nightfall) => FakeCalendarProvider.Result(new([], new Dictionary<DateOnly, HebcalData.SunTimes> { [DateOnly.Parse(date)] = new(DateTimeOffset.Parse(sunset), DateTimeOffset.Parse(nightfall), null) }, null));
}

internal sealed class MutableCalendarClock : TimeProvider
{
    internal DateTimeOffset Now { get; set; } = DateTimeOffset.Parse("2026-09-10T12:00:00Z");
    public override DateTimeOffset GetUtcNow() => Now;
}
