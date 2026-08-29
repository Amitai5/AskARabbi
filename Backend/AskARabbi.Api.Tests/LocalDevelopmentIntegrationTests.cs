using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using AskARabbi.Api.Contracts.Conversations;
using AskARabbi.Api.Contracts.ConversationSettings;
using AskARabbi.Api.Contracts.Users;
using AskARabbi.Api.Authentication;
using AskARabbi.Api.Development;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace AskARabbi.Api.Tests;

[TestClass]
public sealed class LocalDevelopmentIntegrationTests
{
    private static readonly JsonSerializerOptions JsonOptions = CreateJsonOptions();

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

        using var createResponse = await client.PostAsJsonAsync("/api/conversations", new CreateConversationRequest());
        var created = await createResponse.Content.ReadFromJsonAsync<ConversationResponse>();
        Assert.AreEqual(HttpStatusCode.Created, createResponse.StatusCode);
        Assert.IsNotNull(created);

        using var messageResponse = await client.PostAsJsonAsync($"/api/conversations/{created.Id:D}/messages", new AppendMessageRequest
        {
            MessageId = Guid.Parse("22222222-2222-2222-2222-222222222222"),
            Content = "Why do Jewish customs differ?",
        });
        var turn = await messageResponse.Content.ReadFromJsonAsync<ConversationTurnResponse>(JsonOptions);

        Assert.AreEqual(HttpStatusCode.OK, messageResponse.StatusCode);
        Assert.AreEqual("answered", turn?.Status);
        Assert.IsNotNull(turn);
        Assert.HasCount(2, turn.Conversation.Messages);
        Assert.AreEqual("English", application.GroundedAnswers.LastQuestion?.ConversationLanguage);
        Assert.AreEqual("Hebrew", application.GroundedAnswers.LastQuestion?.QuotationLanguage);
        Assert.HasCount(0, application.GroundedAnswers.LastQuestion?.Languages ?? []);

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
