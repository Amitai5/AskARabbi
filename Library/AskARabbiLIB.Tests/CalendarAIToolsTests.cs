using System.Text.Json;
using AskARabbiLIB.AI.Tools;
using AskARabbiLIB.Calendar;
using AskARabbiLIB.Profiles;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace AskARabbiLIB.Tests;

[TestClass]
[TestCategory("Regression")]
public sealed class CalendarAIToolsTests
{
    private static readonly CalendarLocation LosAngeles = new() { Kind = "zip", Id = "91302", Label = "Calabasas", TimeZone = "America/Los_Angeles", Latitude = 34.15778, Longitude = -118.63842 };
    private static readonly CalendarLocation Jerusalem = new() { Kind = "city", Id = "281184", Label = "Jerusalem", TimeZone = "Asia/Jerusalem", Latitude = 31.76904, Longitude = 35.21633 };

    [TestMethod]
    [DataRow(-1, false)]
    [DataRow(0, true)]
    [DataRow(1, true)]
    public async Task GetTodayAsHebrewAndGregorianAsync_AtCurrentLocationSunset_AdvancesHebrewDateOnly(int secondsFromSunset, bool afterSunset)
    {
        var sunset = new DateTimeOffset(2026, 9, 11, 19, 0, 0, TimeSpan.FromHours(-7));
        var context = new AIToolExecutionContext(Profile(), sunset.AddSeconds(secondsFromSunset));
        var solar = new TestSolarTimesProvider { ExpectedLocation = LosAngeles, ExpectedDate = new(2026, 9, 11), Result = new(sunset, null, false) };
        var calendar = new HebrewCalendarService();
        var expected = calendar.ConvertToHebrew(new(2026, 9, 11), afterSunset);

        var result = await new CalendarAITools(calendar, solar).GetTodayAsHebrewAndGregorianAsync(context);
        var data = JsonSerializer.SerializeToElement(result.Data);

        Assert.IsTrue(result.IsSuccess);
        Assert.AreEqual("2026-09-11", data.GetProperty("GregorianDate").GetString());
        Assert.AreEqual(expected.HebrewDay, data.GetProperty("HebrewDay").GetInt32());
        Assert.AreEqual(afterSunset, data.GetProperty("occurredAfterSunset").GetBoolean());
        Assert.IsTrue(data.GetProperty("sunsetWasResolved").GetBoolean());
        Assert.AreEqual(LosAngeles.TimeZone, data.GetProperty("timeZoneId").GetString());
        Assert.AreEqual(1, solar.Calls);
    }

    [TestMethod]
    [DataRow(-1, "2026-09-11")]
    [DataRow(0, "2026-09-12")]
    public async Task GetTodayAsHebrewAndGregorianAsync_LocalMidnight_ChangesCivilDateUsingCurrentNotBirthLocation(int secondsFromMidnight, string expectedDate)
    {
        var instant = new DateTimeOffset(2026, 9, 12, 0, 0, 0, TimeSpan.FromHours(-7)).AddSeconds(secondsFromMidnight);

        var result = await new CalendarAITools(new HebrewCalendarService()).GetTodayAsHebrewAndGregorianAsync(new(Profile(), instant));
        var data = JsonSerializer.SerializeToElement(result.Data);

        Assert.AreEqual(expectedDate, data.GetProperty("GregorianDate").GetString());
        Assert.IsFalse(data.GetProperty("sunsetWasResolved").GetBoolean());
        StringAssert.Contains(result.Evidence?.ExactText, "Sunset data is unavailable");
    }

    [TestMethod]
    public async Task GetTodayAsHebrewAndGregorianAsync_LegacyProfileWithoutCurrentLocation_UsesExplicitUtcFallback()
    {
        var context = new AIToolExecutionContext(Profile() with { CurrentLocation = null }, new(2026, 9, 12, 2, 0, 0, TimeSpan.Zero));
        var solar = new TestSolarTimesProvider();

        var result = await new CalendarAITools(new HebrewCalendarService(), solar).GetTodayAsHebrewAndGregorianAsync(context);
        var data = JsonSerializer.SerializeToElement(result.Data);

        Assert.AreEqual("UTC", data.GetProperty("timeZoneId").GetString());
        Assert.AreEqual("2026-09-12", data.GetProperty("GregorianDate").GetString());
        StringAssert.Contains(result.Evidence?.ExactText, "No current location is saved; UTC is used");
        Assert.AreEqual(0, solar.Calls);
    }

    [TestMethod]
    public async Task FindParashahForWeekAsync_BarMitzvah_UsesPrivateBirthSunsetAndCurrentReadingCycle()
    {
        var profile = Profile() with { BirthLocation = LosAngeles, CurrentLocation = Jerusalem };
        var calendar = new HebrewCalendarService();
        var solar = new TestSolarTimesProvider();
        var tools = new CalendarAITools(calendar, solar);
        var context = new AIToolExecutionContext(profile, new(2026, 9, 11, 12, 0, 0, TimeSpan.Zero));
        var expected = calendar.FindHebrewAnniversaryParashah(new(2001, 12, 17, 20, 0, 0), 13, true, true);

        var result = await tools.FindParashahForWeekAsync(context, hebrewAnniversaryAge: 13);
        var data = JsonSerializer.SerializeToElement(result.Data);

        Assert.AreEqual(expected.ShabbatDate.ToString("yyyy-MM-dd"), data.GetProperty("ShabbatDate").GetString());
        Assert.IsTrue(data.GetProperty("InIsrael").GetBoolean());
        StringAssert.Contains(result.Evidence?.ExactText, "birth was treated as occurring after local sunset");
        StringAssert.Contains(result.Evidence?.ExactText, "calculated privately");
        Assert.IsFalse(JsonSerializer.Serialize(result.Data).Contains("2001", StringComparison.Ordinal));
        Assert.AreEqual(0, solar.Calls);

        var explicitResult = await tools.FindParashahForWeekAsync(context, hebrewAnniversaryAge: 13, inIsrael: false, occurredAfterSunset: false);
        Assert.IsFalse(JsonSerializer.SerializeToElement(explicitResult.Data).GetProperty("InIsrael").GetBoolean());
        StringAssert.Contains(explicitResult.Evidence?.ExactText, "explicit before/after-sunset information");
    }

    [TestMethod]
    [DataRow(-1, "2026-09-12")]
    [DataRow(0, "2026-09-19")]
    public async Task FindParashahForWeekAsync_SaturdayNightfall_MatchesUpcomingCalendarWeek(int secondsFromNightfall, string expectedShabbat)
    {
        var nightfall = new DateTimeOffset(2026, 9, 12, 20, 0, 0, TimeSpan.FromHours(-7));
        var solar = new TestSolarTimesProvider { ExpectedLocation = LosAngeles, ExpectedDate = new(2026, 9, 12), Result = new(nightfall.AddHours(-1), nightfall, false) };

        var result = await new CalendarAITools(new HebrewCalendarService(), solar).FindParashahForWeekAsync(new(Profile(), nightfall.AddSeconds(secondsFromNightfall)));

        Assert.AreEqual(expectedShabbat, JsonSerializer.SerializeToElement(result.Data).GetProperty("ShabbatDate").GetString());
        Assert.AreEqual(1, solar.Calls);
    }

    [TestMethod]
    public void ConvertBirthdateToHebrew_SavedBirthplaceOrExplicitDate_DoesNotApplySavedLocationToSomeoneElse()
    {
        var tools = new CalendarAITools(new HebrewCalendarService());
        var context = new AIToolExecutionContext(Profile(), new(2026, 9, 11, 12, 0, 0, TimeSpan.Zero));

        var saved = tools.ConvertBirthdateToHebrew(context);
        var supplied = tools.ConvertBirthdateToHebrew(context, new(2002, 12, 17, 20, 0, 0));

        Assert.IsTrue(JsonSerializer.SerializeToElement(saved.Data).GetProperty("occurredAfterSunset").GetBoolean());
        Assert.IsFalse(JsonSerializer.SerializeToElement(supplied.Data).GetProperty("sunsetWasResolved").GetBoolean());
        StringAssert.Contains(supplied.Evidence?.ExactText, "Sunset is unknown");
    }

    private static UserProfile Profile() => new() { Name = "Reader", DateOfBirth = new(2001, 12, 17), TimeOfBirth = new(20, 0), BirthTimeZone = "Asia/Jerusalem", BirthLocation = Jerusalem, CurrentLocation = LosAngeles, JewishHeritage = "Mizrahi" };

    private sealed class TestSolarTimesProvider : ICalendarSolarTimesProvider
    {
        public CalendarLocation? ExpectedLocation { get; init; }
        public DateOnly ExpectedDate { get; init; }
        public CalendarSolarTimes? Result { get; init; }
        public int Calls { get; private set; }

        public Task<CalendarSolarTimes?> GetAsync(CalendarLocation location, DateOnly date, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Assert.AreEqual(ExpectedLocation, location, "Only the saved current location may be sent to the solar provider.");
            Assert.AreEqual(ExpectedDate, date);
            Calls++;
            return Task.FromResult(Result);
        }
    }
}
