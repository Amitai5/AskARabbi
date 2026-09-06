using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using AskARabbi.Api.Contracts.Conversations;
using AskARabbi.Api.Contracts.ConversationSettings;
using AskARabbi.Api.Contracts.Users;
using AskARabbi.Api.Authentication;
using AskARabbi.Api.Development;
using AskARabbiLIB.ConversationSettings;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace AskARabbi.Api.Tests;

[TestClass]
public sealed class LocalDevelopmentIntegrationTests
{
    private static readonly JsonSerializerOptions JsonOptions = CreateJsonOptions();

    [TestMethod]
    [TestCategory("Regression")]
    public async Task Personalization_ChangedDuringExistingChat_UsesEveryLanguageAndTimeZoneOnNextReply()
    {
        await using var application = new TestApplicationFactory(false, Environments.Development, true);
        using var client = application.CreateNonRedirectingClient();
        using var login = await client.GetAsync("/api/user/login");
        Assert.IsNotNull(login.Headers.Location);
        using var callback = await client.GetAsync(login.Headers.Location.PathAndQuery);
        Assert.AreEqual(HttpStatusCode.Redirect, callback.StatusCode);
        var profile = new PersonalizationRequest
        {
            FullName = "Learner Example", BirthDateTime = new DateTime(1990, 1, 2, 9, 30, 0, DateTimeKind.Unspecified), BirthTimeZone = "America/New_York",
            ConversationLanguage = "English", QuotationLanguage = "Hebrew", ReligiousMovement = "Conservadox", JewishHeritage = "Mizrahi", AdditionalContext = "Explain unfamiliar terms.",
        };
        using var initialSettings = await client.PutAsJsonAsync("/api/conversation-settings/personalization", profile);
        Assert.AreEqual(HttpStatusCode.OK, initialSettings.StatusCode);
        using var create = await client.PostAsJsonAsync("/api/conversations", new CreateConversationRequest { MessageId = Guid.Parse("10000000-0000-0000-0000-000000000001"), Content = "Explain a Jewish custom." });
        var turn = await create.Content.ReadFromJsonAsync<ConversationTurnResponse>(JsonOptions);
        Assert.AreEqual(HttpStatusCode.Created, create.StatusCode);
        Assert.IsNotNull(turn);

        for (var index = 0; index < PersonalizationCatalog.Languages.Count; index++)
        {
            var updated = profile with
            {
                ConversationLanguage = PersonalizationCatalog.Languages[index],
                QuotationLanguage = PersonalizationCatalog.Languages[(index + 3) % PersonalizationCatalog.Languages.Count],
                BirthTimeZone = PersonalizationCatalog.UnitedStatesTimeZones[index],
                AdditionalContext = $"Preference revision {index}: explain unfamiliar terms briefly.",
                JewishHeritage = index % 2 == 0 ? "Ashkenazi" : "Mizrahi",
                ReligiousMovement = index % 2 == 0 ? "Orthodox" : "Conservative / Masorti",
            };
            using var saved = await client.PutAsJsonAsync("/api/conversation-settings/personalization", updated);
            Assert.AreEqual(HttpStatusCode.OK, saved.StatusCode);
            using var loaded = await client.GetAsync("/api/conversation-settings/personalization");
            var envelope = await loaded.Content.ReadFromJsonAsync<PersonalizationEnvelopeResponse>();
            Assert.AreEqual(updated.ConversationLanguage, envelope?.Personalization?.ConversationLanguage);
            Assert.AreEqual(updated.QuotationLanguage, envelope?.Personalization?.QuotationLanguage);
            using var reply = await client.PostAsJsonAsync($"/api/conversations/{turn.Conversation.Id}/messages", new AppendMessageRequest { MessageId = Guid.Parse($"20000000-0000-0000-0000-{index + 1:D12}"), Content = "Explain that custom again." });
            Assert.AreEqual(HttpStatusCode.OK, reply.StatusCode, await reply.Content.ReadAsStringAsync());
            var question = application.GroundedAnswers.LastQuestion;
            Assert.IsNotNull(question);
            Assert.AreEqual(updated.ConversationLanguage, question.ConversationLanguage);
            Assert.AreEqual(updated.QuotationLanguage, question.QuotationLanguage);
            Assert.AreEqual(updated.AdditionalContext, question.UserProfile?.Bio);
            Assert.AreEqual(updated.FullName, question.UserProfile?.Name);
            Assert.AreEqual(updated.ReligiousMovement, question.UserProfile?.ReligiousBackground);
            Assert.AreEqual(updated.JewishHeritage, question.UserProfile?.JewishHeritage);
            Assert.AreEqual(updated.BirthTimeZone, question.UserProfile?.BirthTimeZone);
            Assert.AreEqual(new DateOnly(1990, 1, 2), question.UserProfile?.DateOfBirth);
            Assert.AreEqual(new TimeOnly(9, 30), question.UserProfile?.TimeOfBirth);
            Assert.HasCount(0, question.Languages, "Quotation preference must not disable fallback source editions.");
        }
    }

    [TestMethod]
    [TestCategory("Integration")]
    public async Task LocalDemo_LoginAndAccountWorkflow_UsesRealHttpControllers()
    {
        await using var application = new TestApplicationFactory(false, Environments.Development, true);
        using var client = application.CreateNonRedirectingClient();
        Assert.IsInstanceOfType<LocalDevelopmentAuthenticationService>(application.Services.GetRequiredService<IUserAuthenticationService>());

        using var loginResponse = await client.GetAsync("/api/user/login");
        Assert.AreEqual(HttpStatusCode.Redirect, loginResponse.StatusCode, await loginResponse.Content.ReadAsStringAsync());
        Assert.IsNotNull(loginResponse.Headers.Location);
        using var callbackResponse = await client.GetAsync(loginResponse.Headers.Location!.PathAndQuery);
        using var sessionResponse = await client.GetAsync("/api/user/session");
        var session = await sessionResponse.Content.ReadFromJsonAsync<UserSessionResponse>();

        Assert.AreEqual(HttpStatusCode.Redirect, callbackResponse.StatusCode);
        Assert.AreEqual(HttpStatusCode.OK, sessionResponse.StatusCode);
        Assert.AreEqual("Amitai Erfanian", session?.DisplayName);

        var profileRequest = new PersonalizationRequest
        {
            FullName = "Amitai Erfanian",
            BirthDateTime = new DateTime(2001, 12, 17, 9, 30, 0, DateTimeKind.Unspecified),
            BirthTimeZone = "America/Los_Angeles",
            ConversationLanguage = "English",
            QuotationLanguage = "Hebrew",
            ReligiousMovement = "Conservadox",
            JewishHeritage = "Mizrahi",
            AdditionalContext = "Iranian Jewish family background.",
        };
        using var profileResponse = await client.PutAsJsonAsync("/api/conversation-settings/personalization", profileRequest);
        var profile = await profileResponse.Content.ReadFromJsonAsync<PersonalizationEnvelopeResponse>();

        Assert.AreEqual(HttpStatusCode.OK, profileResponse.StatusCode);
        Assert.IsTrue(profile?.IsConfigured);
        Assert.AreEqual("Hebrew", profile?.Personalization?.QuotationLanguage);

        using var createResponse = await client.PostAsJsonAsync("/api/conversations", new CreateConversationRequest
        {
            MessageId = Guid.Parse("22222222-2222-2222-2222-222222222222"),
            Content = "Why do Jewish customs differ?",
        });
        var turn = await createResponse.Content.ReadFromJsonAsync<ConversationTurnResponse>(JsonOptions);
        Assert.AreEqual(HttpStatusCode.Created, createResponse.StatusCode);
        Assert.AreEqual("answered", turn?.Status);
        Assert.IsNotNull(turn);
        Assert.HasCount(2, turn.Conversation.Messages);
        Assert.AreEqual("English", application.GroundedAnswers.LastQuestion?.ConversationLanguage);
        Assert.AreEqual("Hebrew", application.GroundedAnswers.LastQuestion?.QuotationLanguage);
        Assert.HasCount(0, application.GroundedAnswers.LastQuestion?.Languages ?? []);
        Assert.AreEqual(new DateOnly(2001, 12, 17), application.GroundedAnswers.LastQuestion?.UserProfile?.DateOfBirth);
        Assert.AreEqual(new TimeOnly(9, 30), application.GroundedAnswers.LastQuestion?.UserProfile?.TimeOfBirth);
        Assert.AreEqual("America/Los_Angeles", application.GroundedAnswers.LastQuestion?.UserProfile?.BirthTimeZone);

        using var usageResponse = await client.GetAsync("/api/conversation-settings/usage");
        var usage = await usageResponse.Content.ReadFromJsonAsync<UsageResponse>();

        Assert.AreEqual(HttpStatusCode.OK, usageResponse.StatusCode);
        Assert.AreEqual(1, usage?.AnswersUsed);
        Assert.AreEqual(new DateTimeOffset(2026, 8, 1, 0, 0, 0, TimeSpan.Zero), usage?.PeriodStartUtc);
    }

    private static JsonSerializerOptions CreateJsonOptions()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        options.Converters.Add(new JsonStringEnumConverter());
        return options;
    }
}
