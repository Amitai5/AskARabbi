using System.Net;
using System.Net.Http.Json;
using System.Text;
using AskARabbiLIB.ConversationSettings;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace AskARabbi.Api.Tests;

[TestClass]
[TestCategory("Integration")]
public sealed class ReadingPreferencesTests
{
    [TestMethod]
    public async Task ReadingPreferences_NewAccount_ReturnsDefaultsWithoutCaching()
    {
        await using var app = new TestApplicationFactory();
        using var client = await app.CreateAuthenticatedClientAsync();

        using var response = await client.GetAsync("/api/conversation-settings/reading");
        var value = await response.Content.ReadFromJsonAsync<ReadingPreferences>();

        Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
        Assert.AreEqual(new ReadingPreferences(), value);
        Assert.IsTrue(response.Headers.CacheControl?.NoStore);
        Assert.IsNull(await app.Store.GetReadingPreferencesAsync(app.Store.UserId));
    }

    [TestMethod]
    public async Task ReadingPreferences_UpdateThenRead_PreservesOtherSettingsAndOtherUsers()
    {
        await using var app = new TestApplicationFactory();
        using var client = await app.CreateAuthenticatedClientAsync();
        var otherId = Guid.Parse("22222222-2222-2222-2222-222222222222");
        var other = new ReadingPreferences { TextSize = "small", Theme = "light" };
        await app.Store.UpsertReadingPreferencesAsync(otherId, other, DateTimeOffset.MinValue);
        var preferences = new ConversationPreferences { EmailProductUpdates = true, ShowSourceContextByDefault = true };
        await app.Store.UpsertPreferencesAsync(app.Store.UserId, preferences, DateTimeOffset.MinValue);
        var expected = new ReadingPreferences { TextSize = "extra-large", LineSpacing = "relaxed", Theme = "dark", FocusLongContent = true };

        using var update = await client.PutAsJsonAsync("/api/conversation-settings/reading", expected);
        var result = await client.GetFromJsonAsync<ReadingPreferences>("/api/conversation-settings/reading");
        using var legacyUpdate = await client.PutAsJsonAsync("/api/conversation-settings/preferences", new { showSourceContextByDefault = false, emailProductUpdates = true });

        Assert.AreEqual(HttpStatusCode.OK, update.StatusCode);
        Assert.AreEqual(HttpStatusCode.OK, legacyUpdate.StatusCode);
        Assert.AreEqual(expected, result);
        Assert.AreEqual(expected, await app.Store.GetReadingPreferencesAsync(app.Store.UserId));
        Assert.AreEqual(other, await app.Store.GetReadingPreferencesAsync(otherId));
        Assert.IsTrue((await app.Store.GetPreferencesAsync(app.Store.UserId))?.EmailProductUpdates);
    }

    [TestMethod]
    [DataRow("huge", "default", "system")]
    [DataRow("default", "100", "light")]
    [DataRow("default", "relaxed", "injected")]
    [DataRow("", "default", "system")]
    public async Task ReadingPreferences_UnsupportedPreset_RejectsWithoutWriting(string size, string spacing, string theme)
    {
        await using var app = new TestApplicationFactory();
        using var client = await app.CreateAuthenticatedClientAsync();

        using var response = await client.PutAsJsonAsync("/api/conversation-settings/reading", new { textSize = size, lineSpacing = spacing, theme });

        Assert.AreEqual(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.IsNull(await app.Store.GetReadingPreferencesAsync(app.Store.UserId));
    }

    [TestMethod]
    public async Task ReadingPreferences_MissingPresets_RejectsIncompleteRequest()
    {
        await using var app = new TestApplicationFactory();
        using var client = await app.CreateAuthenticatedClientAsync();
        using var body = new StringContent("{}", Encoding.UTF8, "application/json");

        using var response = await client.PutAsync("/api/conversation-settings/reading", body);

        Assert.AreEqual(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.IsNull(await app.Store.GetReadingPreferencesAsync(app.Store.UserId));
    }

    [TestMethod]
    public async Task ReadingPreferences_AnonymousRequest_RequiresAuthentication()
    {
        await using var app = new TestApplicationFactory();
        using var client = app.CreateClient();

        using var get = await client.GetAsync("/api/conversation-settings/reading");
        using var put = await client.PutAsJsonAsync("/api/conversation-settings/reading", new ReadingPreferences());

        Assert.AreEqual(HttpStatusCode.Unauthorized, get.StatusCode);
        Assert.AreEqual(HttpStatusCode.Unauthorized, put.StatusCode);
    }
}
