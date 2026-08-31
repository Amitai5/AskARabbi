using System.Net;
using System.Net.Http.Json;
using AskARabbi.Api.Contracts.ConversationSettings;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace AskARabbi.Api.Tests;

[TestClass]
public sealed class ConversationSettingsControllerTests
{
    [TestMethod]
    [TestCategory("Integration")]
    public async Task GetUsage_CurrentBillingPeriod_ReturnsExactUtcDatesAndCounts()
    {
        await using var application = new TestApplicationFactory();
        using var client = await application.CreateAuthenticatedClientAsync();

        using var response = await client.GetAsync("/api/conversation-settings/usage");
        var usage = await response.Content.ReadFromJsonAsync<UsageResponse>();

        Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
        Assert.IsNotNull(usage);
        Assert.AreEqual(new DateTimeOffset(2026, 8, 1, 0, 0, 0, TimeSpan.Zero), usage.PeriodStartUtc);
        Assert.AreEqual(new DateTimeOffset(2026, 9, 1, 0, 0, 0, TimeSpan.Zero), usage.PeriodEndUtc);
        Assert.AreEqual(7, usage.AnswersUsed);
        Assert.AreEqual(50, usage.AnswerLimit);
        Assert.AreEqual(43, usage.AnswersRemaining);
    }

    [TestMethod]
    [TestCategory("Integration")]
    public async Task Personalization_UpdateThenGet_ReturnsNormalizedAccountSettings()
    {
        await using var application = new TestApplicationFactory();
        using var client = await application.CreateAuthenticatedClientAsync();
        using var initialResponse = await client.GetAsync("/api/conversation-settings/personalization");
        var initial = await initialResponse.Content.ReadFromJsonAsync<PersonalizationEnvelopeResponse>();

        using var updateResponse = await client.PutAsJsonAsync("/api/conversation-settings/personalization", new
        {
            fullName = "  Amitai Erfanian  ",
            birthDateTime = "2001-12-17T15:30:00",
            birthTimeZone = "America/Los_Angeles",
            conversationLanguage = "English",
            quotationLanguage = "Hebrew",
            religiousMovement = "Between Modern Orthodox and Conservative",
            jewishHeritage = "Mizrahi (Iranian)",
            additionalContext = "  Builds software.  ",
        });
        var updated = await updateResponse.Content.ReadFromJsonAsync<PersonalizationEnvelopeResponse>();
        using var getResponse = await client.GetAsync("/api/conversation-settings/personalization");
        var current = await getResponse.Content.ReadFromJsonAsync<PersonalizationEnvelopeResponse>();

        Assert.IsNotNull(initial);
        Assert.IsFalse(initial.IsConfigured);
        Assert.AreEqual(HttpStatusCode.OK, updateResponse.StatusCode);
        Assert.IsNotNull(updated?.Personalization);
        Assert.AreEqual("Amitai Erfanian", updated.Personalization.FullName);
        Assert.AreEqual("Builds software.", updated.Personalization.AdditionalContext);
        Assert.IsNotNull(current?.Personalization);
        Assert.AreEqual(updated.Personalization, current.Personalization);
    }

    [TestMethod]
    [TestCategory("Integration")]
    public async Task UpdatePersonalization_UnsupportedTimeZone_ReturnsProblemDetails()
    {
        await using var application = new TestApplicationFactory();
        using var client = await application.CreateAuthenticatedClientAsync();

        using var response = await client.PutAsJsonAsync("/api/conversation-settings/personalization", new
        {
            fullName = "Amitai Erfanian",
            birthDateTime = "2001-12-17T15:30:00",
            birthTimeZone = "Europe/London",
            conversationLanguage = "English",
            quotationLanguage = "Hebrew",
            religiousMovement = "Conservative",
            jewishHeritage = "Mizrahi",
        });

        Assert.AreEqual(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.AreEqual("application/problem+json", response.Content.Headers.ContentType?.MediaType);
    }

    [TestMethod]
    [TestCategory("Integration")]
    public async Task Preferences_UpdateThenGet_PersistsAccountSettings()
    {
        await using var application = new TestApplicationFactory();
        using var client = await application.CreateAuthenticatedClientAsync();
        using var initialResponse = await client.GetAsync("/api/conversation-settings/preferences");
        var initial = await initialResponse.Content.ReadFromJsonAsync<ConversationPreferencesResponse>();

        using var updateResponse = await client.PutAsJsonAsync("/api/conversation-settings/preferences", new
        {
            showSourceContextByDefault = true,
            emailProductUpdates = true,
        });
        var updated = await updateResponse.Content.ReadFromJsonAsync<ConversationPreferencesResponse>();
        using var getResponse = await client.GetAsync("/api/conversation-settings/preferences");
        var current = await getResponse.Content.ReadFromJsonAsync<ConversationPreferencesResponse>();

        Assert.IsNotNull(initial);
        Assert.IsFalse(initial.ShowSourceContextByDefault);
        Assert.IsFalse(initial.EmailProductUpdates);
        Assert.AreEqual(HttpStatusCode.OK, updateResponse.StatusCode);
        Assert.IsNotNull(updated);
        Assert.IsTrue(updated.ShowSourceContextByDefault);
        Assert.IsTrue(updated.EmailProductUpdates);
        Assert.AreEqual(updated, current);
    }
}
