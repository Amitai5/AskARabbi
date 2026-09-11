using System.Net;
using System.Net.Http.Json;
using AskARabbi.Api.Calendar;
using AskARabbi.Api.Controllers;
using AskARabbiLIB.Calendar;
using AskARabbiLIB.Usage;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace AskARabbi.Api.Tests;

[TestClass]
public sealed class CalendarControllerTests
{
    [TestMethod]
    [DataRow("overview?days=90")]
    [DataRow("preferences")]
    public async Task Get_Anonymous_RequiresAuthentication(string endpoint)
    {
        await using var app = new TestApplicationFactory();
        using var client = app.CreateNonRedirectingClient();
        using var response = await client.GetAsync("/api/calendar/" + endpoint);
        Assert.AreEqual(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.HasCount(0, app.Calendar.HolidayRequests);
    }

    [TestMethod]
    public async Task GetPreferences_OlderAccount_ExplicitDefaultsAndNoStore()
    {
        await using var app = new TestApplicationFactory();
        using var client = await app.CreateAuthenticatedClientAsync();
        using var response = await client.GetAsync("/api/calendar/preferences");
        var value = await response.Content.ReadFromJsonAsync<CalendarController.PreferencesResponse>();
        Assert.IsNotNull(value);
        Assert.IsNull(value.Preferences.Location);
        Assert.IsFalse(value.Preferences.InIsrael);
        Assert.IsTrue(value.Preferences.MajorHolidays && value.Preferences.MinorHolidays && value.Preferences.FastDays && value.Preferences.RoshChodesh);
        Assert.IsTrue(value.Preferences.SpecialShabbatot && value.Preferences.ModernObservances);
        Assert.IsTrue(response.Headers.CacheControl?.NoStore);
        Assert.HasCount(10, value.Cities);
    }

    [TestMethod]
    public async Task UpdatePreferences_CityMetadataAndOtherOwner_IgnoresSpoofedTimezoneAndKeepsOtherSettings()
    {
        await using var app = new TestApplicationFactory();
        using var client = await app.CreateAuthenticatedClientAsync();
        var other = Guid.Parse("22222222-2222-2222-2222-222222222222");
        await app.Store.UpsertCalendarPreferencesAsync(other, new() { InIsrael = true }, DateTimeOffset.MinValue);
        await app.Store.UpsertPreferencesAsync(app.Store.UserId, new() { EmailProductUpdates = true }, DateTimeOffset.MinValue);
        using var response = await client.PutAsJsonAsync("/api/calendar/preferences", new CalendarPreferences { Location = CalendarLocationCatalog.Cities[6] with { Label = "Spoofed", TimeZone = "America/Los_Angeles", DefaultCandleLightingMinutes = 1 } });
        var saved = await response.Content.ReadFromJsonAsync<CalendarPreferences>();
        Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
        Assert.AreEqual("Asia/Jerusalem", saved?.Location?.TimeZone);
        Assert.AreEqual(40, saved?.Location?.DefaultCandleLightingMinutes);
        Assert.IsTrue((await app.Store.GetPreferencesAsync(app.Store.UserId))?.EmailProductUpdates);
        Assert.IsTrue((await app.Store.GetCalendarPreferencesAsync(other))?.InIsrael);
    }

    [TestMethod]
    [DataRow(0)]
    [DataRow(31)]
    [DataRow(366)]
    public async Task GetOverview_InvalidRange_RejectsBeforeProvider(int days)
    {
        await using var app = new TestApplicationFactory();
        using var client = await app.CreateAuthenticatedClientAsync();
        using var response = await client.GetAsync($"/api/calendar/overview?days={days}");
        Assert.AreEqual(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.HasCount(0, app.Calendar.HolidayRequests);
    }

    [TestMethod]
    [DataRow(30)]
    [DataRow(90)]
    [DataRow(180)]
    [DataRow(360)]
    [DataRow(365)]
    public async Task GetOverview_SupportedRangeWithAllCategories_ReturnsNoStoreCalendar(int days)
    {
        await using var app = new TestApplicationFactory();
        using var client = await app.CreateAuthenticatedClientAsync();

        using var response = await client.GetAsync($"/api/calendar/overview?days={days}&includeAllCategories=true");

        Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
        Assert.IsTrue(response.Headers.CacheControl?.NoStore);
        Assert.AreEqual(0, app.GroundedAnswers.CallCount);
    }

    [TestMethod]
    public async Task GetOverview_ExhaustedAllowance_StillWorksWithoutAiOrTokens()
    {
        await using var app = new TestApplicationFactory(configureAi: false);
        using var client = await app.CreateAuthenticatedClientAsync();
        var start = new DateTimeOffset(2026, 8, 1, 0, 0, 0, TimeSpan.Zero);
        var now = new DateTimeOffset(2026, 8, 25, 12, 30, 0, TimeSpan.Zero);
        var lease = new ChatUsageLease(app.Store.UserId, Guid.Parse("33333333-3333-3333-3333-333333333333"), start, start.AddMonths(1), now.AddMinutes(1), 10_000_000);
        Assert.IsTrue(await app.Store.TryAcquireChatAsync(lease, now));
        Assert.IsTrue(await app.Store.RecordTokensAsync(lease, 10_000_000));
        await app.Store.ReleaseChatAsync(lease);

        using var response = await client.GetAsync("/api/calendar/overview?days=90");

        Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
        var json = await response.Content.ReadAsStringAsync();
        StringAssert.Contains(json, "\"gregorianDate\":\"2026-08-25\"");
        Assert.IsTrue(response.Headers.CacheControl?.NoStore);
        Assert.AreEqual(10_000_000L, await app.Store.GetTokenCountAsync(app.Store.UserId, start, start.AddMonths(1)));
        Assert.AreEqual(0, app.GroundedAnswers.CallCount);
    }

    [TestMethod]
    public async Task DeleteAccount_CalendarPreferences_RemovesOnlyOwnedCalendarData()
    {
        await using var app = new TestApplicationFactory();
        using var client = await app.CreateAuthenticatedClientAsync();
        var owner = app.Store.UserId;
        var other = Guid.Parse("22222222-2222-2222-2222-222222222222");
        await app.Store.UpsertCalendarPreferencesAsync(owner, new() { InIsrael = true }, DateTimeOffset.MinValue);
        await app.Store.UpsertCalendarPreferencesAsync(other, new(), DateTimeOffset.MinValue);
        using var request = new HttpRequestMessage(HttpMethod.Delete, "/api/user/data/account");
        request.Headers.Add("X-Confirm-Deletion", "DELETE ACCOUNT");
        using var response = await client.SendAsync(request);
        Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
        Assert.IsNull(await app.Store.GetCalendarPreferencesAsync(owner));
        Assert.IsNotNull(await app.Store.GetCalendarPreferencesAsync(other));
    }

    [TestMethod]
    [DataRow("city", "12345", 18, "nightfall", 42)]
    [DataRow("zip", "9021", 18, "nightfall", 42)]
    [DataRow("zip", "90abc", 18, "nightfall", 42)]
    [DataRow("city", "5128581", 0, "nightfall", 42)]
    [DataRow("city", "5128581", 91, "nightfall", 42)]
    [DataRow("city", "5128581", 18, "other", 42)]
    [DataRow("city", "5128581", 18, "fixed", 13)]
    public async Task UpdatePreferences_InvalidInput_DoesNotPersist(string kind, string id, int candles, string havdalah, int minutes)
    {
        await using var app = new TestApplicationFactory();
        using var client = await app.CreateAuthenticatedClientAsync();
        using var response = await client.PutAsJsonAsync("/api/calendar/preferences", new CalendarPreferences { Location = new() { Kind = kind, Id = id, Label = id, TimeZone = "UTC" }, CandleLightingMinutes = candles, Havdalah = havdalah, HavdalahMinutes = minutes });
        Assert.AreEqual(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.IsNull(await app.Store.GetCalendarPreferencesAsync(app.Store.UserId));
    }

    [TestMethod]
    public async Task UpdatePreferences_Zip_ResolvesTimezoneAndDoesNotOverwriteOnFailure()
    {
        var store = new InMemoryApplicationStore();
        var provider = new FakeCalendarProvider();
        var clock = new MutableCalendarClock();
        var service = new CalendarPreferencesService(store, provider, clock);
        var owner = Guid.Parse("11111111-1111-1111-1111-111111111111");
        var zip = new CalendarLocation { Kind = "zip", Id = "90210", Label = "Beverly Hills, CA 90210", TimeZone = "America/Los_Angeles" };
        provider.Solar = FakeCalendarProvider.Result(new([], new Dictionary<DateOnly, HebcalData.SunTimes>(), zip));
        var saved = await service.UpdateAsync(owner, new() { Location = zip with { TimeZone = "fake" } }, default);
        Assert.AreEqual(zip, saved?.Location);
        provider.Solar = FakeCalendarProvider.Result(null);
        var failure = await service.UpdateAsync(owner, new() { Location = zip with { Id = "10001" } }, default);
        Assert.IsNull(failure);
        Assert.AreEqual(zip, (await service.GetAsync(owner, default)).Location);
    }
}
