using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using AskARabbi.Api.Calendar;
using AskARabbi.Api.Contracts.Conversations;
using AskARabbi.Api.Contracts.ConversationSettings;
using AskARabbiLIB.Calendar;
using AskARabbiLIB.ConversationSettings;
using AskARabbiLIB.AI.Tools;
using AskARabbiLIB.Profiles;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace AskARabbi.Api.Tests;

[TestClass]
public sealed class PersonalizationLocationTests
{
    private static readonly CalendarLocation Zip = new() { Kind = "zip", Id = "91302", Label = "Calabasas, CA 91302", TimeZone = "America/Los_Angeles", Latitude = 34.15778, Longitude = -118.63842 };
    private static readonly CalendarLocation Jerusalem = CalendarLocationCatalog.Cities.Single(city => city.Id == "281184") with { Latitude = 31.76904, Longitude = 35.21633 };

    [TestMethod]
    [TestCategory("Integration")]
    public async Task UpdatePersonalization_SameZip_ResolvesOnceAndNeverSendsBirthDate()
    {
        await using var app = new TestApplicationFactory();
        using var client = await app.CreateAuthenticatedClientAsync();
        app.Calendar.Solar = FakeCalendarProvider.Result(new([], new Dictionary<DateOnly, HebcalData.SunTimes>(), Zip));
        var request = Request() with { BirthTimeZone = "Spoofed/Timezone" };

        using var response = await client.PutAsJsonAsync("/api/conversation-settings/personalization", request);
        var body = await response.Content.ReadFromJsonAsync<PersonalizationEnvelopeResponse>();

        Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
        Assert.AreEqual(Zip, body?.Personalization?.BirthLocation);
        Assert.AreEqual(Zip, body?.Personalization?.CurrentLocation);
        Assert.AreEqual(Zip.TimeZone, body?.Personalization?.BirthTimeZone);
        Assert.HasCount(1, app.Calendar.SolarRequests);
        var lookup = app.Calendar.SolarRequests[0];
        Assert.AreEqual(new DateOnly(2026, 8, 25), lookup.Start);
        Assert.AreEqual(new DateOnly(2026, 8, 26), lookup.End);
        Assert.AreEqual(string.Empty, lookup.Location.Label);
        Assert.AreEqual(string.Empty, lookup.Location.TimeZone);
        Assert.IsNull(lookup.Location.Latitude);
        Assert.AreEqual("91302", lookup.Location.Id);
        var stored = await app.Store.GetPersonalizationAsync(app.Store.UserId);
        Assert.AreEqual(Zip, stored?.CurrentLocation);
        Assert.AreEqual(Zip, stored?.BirthLocation);

        app.Calendar.Solar = FakeCalendarProvider.Result(null);
        using var unchanged = await client.PutAsJsonAsync("/api/conversation-settings/personalization", request);
        Assert.AreEqual(HttpStatusCode.OK, unchanged.StatusCode);
        Assert.HasCount(1, app.Calendar.SolarRequests, "Unchanged resolved locations must not require another external lookup.");
    }

    [TestMethod]
    [TestCategory("Integration")]
    public async Task UpdatePersonalization_DifferentBirthAndCurrentLocations_CalendarUsesCurrentLocationAndDefaults()
    {
        await using var app = new TestApplicationFactory();
        using var client = await app.CreateAuthenticatedClientAsync();
        app.Calendar.SolarLookup = (location, _, _) => FakeCalendarProvider.Result(new([], new Dictionary<DateOnly, HebcalData.SunTimes>(), location.Kind == "zip" ? Zip : Jerusalem));
        await app.Store.UpsertCalendarPreferencesAsync(app.Store.UserId, new() { Location = Zip, ShowLocalTimes = false, CandleLightingMinutes = 70, Havdalah = "fixed", HavdalahMinutes = 72, ModernObservances = true }, DateTimeOffset.MinValue);

        using var update = await client.PutAsJsonAsync("/api/conversation-settings/personalization", Request() with { CurrentLocation = new() { Kind = "city", Id = Jerusalem.Id } });
        using var response = await client.GetAsync("/api/calendar/overview?days=30");
        var overview = await response.Content.ReadFromJsonAsync<CalendarOverview>();
        var stored = await app.Store.GetPersonalizationAsync(app.Store.UserId);

        Assert.AreEqual(HttpStatusCode.OK, update.StatusCode);
        Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
        Assert.IsNotNull(overview);
        Assert.AreEqual(Zip, stored?.BirthLocation);
        Assert.AreEqual(Zip.TimeZone, stored?.BirthTimeZone);
        Assert.AreEqual(Jerusalem, overview.Preferences.Location);
        Assert.AreEqual("Asia/Jerusalem", overview.Today.TimeZone);
        Assert.IsTrue(overview.Preferences.InIsrael);
        Assert.IsTrue(overview.Preferences.ShowLocalTimes);
        Assert.IsTrue(overview.Preferences.ModernObservances);
        Assert.IsNull(overview.Preferences.CandleLightingMinutes);
        Assert.AreEqual("nightfall", overview.Preferences.Havdalah);
        Assert.HasCount(1, app.Calendar.TimingRequests);
        Assert.AreEqual(Jerusalem, app.Calendar.TimingRequests[0].Location);
    }

    [TestMethod]
    [TestCategory("Integration")]
    public async Task GetPersonalization_LegacyCalendarLocation_ReadsWithoutInventingBirthplace()
    {
        await using var app = new TestApplicationFactory();
        using var client = await app.CreateAuthenticatedClientAsync();
        var legacy = LegacyProfile();
        await app.Store.UpsertPersonalizationAsync(app.Store.UserId, legacy, DateTimeOffset.MinValue);
        await app.Store.UpsertCalendarPreferencesAsync(app.Store.UserId, new() { Location = Zip }, DateTimeOffset.MinValue);

        using var response = await client.GetAsync("/api/conversation-settings/personalization");
        var profile = await response.Content.ReadFromJsonAsync<PersonalizationEnvelopeResponse>();

        Assert.IsNotNull(profile);
        Assert.IsTrue(profile.IsConfigured);
        Assert.IsTrue(response.Headers.CacheControl?.NoStore);
        Assert.AreEqual(Zip, profile.Personalization?.CurrentLocation);
        Assert.IsNull(profile.Personalization?.BirthLocation);
        Assert.AreEqual("America/New_York", profile.Personalization?.BirthTimeZone);
        Assert.HasCount(0, app.Calendar.SolarRequests);
        Assert.AreEqual(legacy, await app.Store.GetPersonalizationAsync(app.Store.UserId), "Reading a legacy profile must not mutate it.");
    }

    [TestMethod]
    [DataRow("zip", "9130")]
    [DataRow("zip", "abcde")]
    [DataRow("city", "12345")]
    [DataRow("url", "12345")]
    [TestCategory("Integration")]
    public async Task UpdatePersonalization_InvalidLocation_RejectsWithoutPersistingOrLookup(string kind, string id)
    {
        await using var app = new TestApplicationFactory();
        using var client = await app.CreateAuthenticatedClientAsync();
        var location = new PersonalizationLocationRequest { Kind = kind, Id = id };

        using var response = await client.PutAsJsonAsync("/api/conversation-settings/personalization", Request() with { BirthLocation = location, CurrentLocation = location });

        Assert.AreEqual(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.IsNull(await app.Store.GetPersonalizationAsync(app.Store.UserId));
        Assert.HasCount(0, app.Calendar.SolarRequests);
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    [TestCategory("Integration")]
    public async Task UpdatePersonalization_UnavailableOrIncompleteLocation_KeepsPreviousSettings(bool missingCoordinates)
    {
        await using var app = new TestApplicationFactory();
        using var client = await app.CreateAuthenticatedClientAsync();
        var previous = LegacyProfile();
        await app.Store.UpsertPersonalizationAsync(app.Store.UserId, previous, DateTimeOffset.MinValue);
        app.Calendar.Solar = FakeCalendarProvider.Result(missingCoordinates ? new([], new Dictionary<DateOnly, HebcalData.SunTimes>(), Zip with { Latitude = null, Longitude = null }) : null);

        using var response = await client.PutAsJsonAsync("/api/conversation-settings/personalization", Request());

        Assert.AreEqual(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        Assert.AreEqual(previous, await app.Store.GetPersonalizationAsync(app.Store.UserId));
    }

    [TestMethod]
    [TestCategory("Integration")]
    public async Task CurrentDate_SharedCalendarAndChatProvider_UsesIdenticalSunsetData()
    {
        var sunset = new DateTimeOffset(2026, 9, 11, 19, 0, 0, TimeSpan.FromHours(-7));
        await using var app = new TestApplicationFactory { Clock = new MutableCalendarClock { Now = sunset } };
        using var client = await app.CreateAuthenticatedClientAsync();
        var date = new DateOnly(2026, 9, 11);
        app.Calendar.Solar = FakeCalendarProvider.Result(new([], new Dictionary<DateOnly, HebcalData.SunTimes> { [date] = new(sunset, sunset.AddHours(1), sunset.AddHours(-13)) }, Zip));
        await app.Store.UpsertPersonalizationAsync(app.Store.UserId, LegacyProfile() with { BirthLocation = Jerusalem, CurrentLocation = Zip }, sunset);

        var overview = await client.GetFromJsonAsync<CalendarOverview>("/api/calendar/overview?days=30");
        var profile = new UserProfile { Name = "Reader", DateOfBirth = new(2001, 12, 17), JewishHeritage = "Mizrahi", BirthLocation = Jerusalem, CurrentLocation = Zip };
        var result = await app.Services.GetRequiredService<CalendarAITools>().GetTodayAsHebrewAndGregorianAsync(new(profile, sunset));
        var data = JsonSerializer.SerializeToElement(result.Data);

        Assert.IsNotNull(overview);
        Assert.IsTrue(overview.Today.IsAfterSunset);
        Assert.IsTrue(data.GetProperty("occurredAfterSunset").GetBoolean());
        Assert.AreEqual(overview.Today.GregorianDate.ToString("yyyy-MM-dd"), data.GetProperty("GregorianDate").GetString());
        Assert.AreEqual(overview.Today.HebrewScript, data.GetProperty("HebrewText").GetString());
        Assert.AreEqual(overview.Today.TimeZone, data.GetProperty("timeZoneId").GetString());
        Assert.HasCount(2, app.Calendar.SolarRequests);
        Assert.AreEqual(app.Calendar.SolarRequests[0], app.Calendar.SolarRequests[1], "Calendar and chat should use the same query and therefore the same production provider cache entry.");
    }

    [TestMethod]
    [TestCategory("Integration")]
    public async Task UpdatePersonalization_LocationChanged_UsesSavedLocationsInNextChatTurn()
    {
        await using var app = new TestApplicationFactory();
        using var client = await app.CreateAuthenticatedClientAsync();
        app.Calendar.SolarLookup = (location, _, _) => FakeCalendarProvider.Result(new([], new Dictionary<DateOnly, HebcalData.SunTimes>(), location.Kind == "zip" ? Zip : Jerusalem));
        using var saved = await client.PutAsJsonAsync("/api/conversation-settings/personalization", Request() with { CurrentLocation = new() { Kind = "city", Id = Jerusalem.Id } });

        using var created = await client.PostAsJsonAsync("/api/conversations", new CreateConversationRequest { MessageId = Guid.Parse("11111111-2222-3333-4444-555555555555"), Content = "What is today's Hebrew date?" });

        Assert.AreEqual(HttpStatusCode.OK, saved.StatusCode);
        Assert.AreEqual(HttpStatusCode.Created, created.StatusCode);
        Assert.AreEqual(Zip, app.GroundedAnswers.LastQuestion?.UserProfile?.BirthLocation);
        Assert.AreEqual(Zip.TimeZone, app.GroundedAnswers.LastQuestion?.UserProfile?.BirthTimeZone);
        Assert.AreEqual(Jerusalem, app.GroundedAnswers.LastQuestion?.UserProfile?.CurrentLocation);
    }

    [TestMethod]
    [TestCategory("Integration")]
    public async Task GetLocations_RequiresAuthenticationAndDoesNotCallProvider()
    {
        await using var app = new TestApplicationFactory();
        using var anonymous = app.CreateNonRedirectingClient();
        using var denied = await anonymous.GetAsync("/api/conversation-settings/locations");
        using var client = await app.CreateAuthenticatedClientAsync();
        var cities = await client.GetFromJsonAsync<List<CalendarLocation>>("/api/conversation-settings/locations");

        Assert.AreEqual(HttpStatusCode.Unauthorized, denied.StatusCode);
        Assert.IsNotNull(cities);
        Assert.HasCount(10, cities);
        Assert.HasCount(0, app.Calendar.SolarRequests);
    }

    private static PersonalizationRequest Request() => new() { FullName = "Reader", BirthDateTime = new(2001, 12, 17, 20, 0, 0, DateTimeKind.Unspecified), BirthLocation = new() { Kind = "zip", Id = "91302" }, CurrentLocation = new() { Kind = "zip", Id = "91302" }, ConversationLanguage = "English", QuotationLanguage = "Hebrew", ReligiousMovement = "Traditional", JewishHeritage = "Mizrahi" };
    private static PersonalizationSettings LegacyProfile() => new() { FullName = "Reader", BirthDate = new(2001, 12, 17), BirthTime = new(20, 0), BirthTimeZone = "America/New_York", ConversationLanguage = "English", QuotationLanguage = "Hebrew", ReligiousMovement = "Traditional", JewishHeritage = "Mizrahi" };
}
