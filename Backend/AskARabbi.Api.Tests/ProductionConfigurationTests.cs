using System.Net;
using System.Text.Json;
using AskARabbi.Api.Authentication;
using AskARabbi.Api.Configuration;
using AskARabbiLIB.AI;
using AskARabbiLIB.Grounding;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using WorkOS;

namespace AskARabbi.Api.Tests;

[TestClass]
public sealed class ProductionConfigurationTests
{
    [TestMethod]
    [TestCategory("Integration")]
    public async Task ProductionConfiguration_PublicDomainsMatchDeploymentTopology()
    {
        await using var application = new TestApplicationFactory(true, Environments.Production, false, null);
        using var client = application.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            BaseAddress = new Uri("https://api.askarabbi.ai"),
        });
        using var request = new HttpRequestMessage(HttpMethod.Options, "/health");
        request.Headers.Add("Origin", "https://app.askarabbi.ai");
        request.Headers.Add("Access-Control-Request-Method", "GET");

        using var response = await client.SendAsync(request);
        var workOs = application.Services.GetRequiredService<WorkOsAuthenticationOptions>();
        var groundedChat = application.Services.GetRequiredService<GroundedChatOptions>();
        var groundedPrompts = application.Services.GetRequiredService<GroundedPromptSet>();

        Assert.AreEqual("https://api.askarabbi.ai/api/user/callback", workOs.RedirectUri);
        Assert.AreEqual("https://app.askarabbi.ai/", workOs.FrontendUri);
        Assert.AreEqual(8_000, groundedChat.MaximumOutputTokens);
        Assert.AreEqual(3_200, groundedChat.ValidationMaximumOutputTokens);
        Assert.AreEqual(AIReasoningEffort.Medium, groundedChat.ReasoningEffort);
        Assert.AreEqual(AIServiceTier.Priority, groundedChat.ServiceTier);
        Assert.AreEqual(20, groundedChat.MaximumCandidates);
        Assert.AreEqual(10, groundedChat.MaximumEvidenceSegments);
        Assert.AreEqual(0, groundedChat.MaximumEnrichmentHits);
        Assert.AreEqual(2, groundedChat.RecentConversationTurns);
        Assert.AreEqual(600, groundedChat.RetrievalCacheSeconds);
        Assert.AreEqual("https://app.askarabbi.ai", response.Headers.GetValues("Access-Control-Allow-Origin").Single());
        Assert.IsTrue(response.Headers.GetValues("Access-Control-Allow-Credentials").Single().Equals("true", StringComparison.OrdinalIgnoreCase));
        StringAssert.StartsWith(groundedPrompts.CurrentQuestionInstruction, "Write one flowing, human answer.");
        StringAssert.Contains(groundedPrompts.CurrentQuestionInstruction, "Follow answerFocus as a required task definition");
        StringAssert.Contains(groundedPrompts.CurrentQuestionInstruction, "A why-question must explain the evidenced rationale");
        StringAssert.Contains(groundedPrompts.CurrentQuestionInstruction, "independently verifiable proposition");
        StringAssert.Contains(groundedPrompts.ValidationRepairPrompt, "add, remove, or reassign real evidence IDs");
        StringAssert.Contains(groundedPrompts.ValidationRepairPrompt, "never invent an evidence ID");
        StringAssert.Contains(groundedPrompts.ValidationRepairPrompt, "use remaining source-research calls");
        StringAssert.Contains(groundedPrompts.SupportValidationPrompt, "separate support obligation");
        StringAssert.Contains(groundedPrompts.SupportValidationPrompt, "isResponsive");
        StringAssert.Contains(groundedPrompts.SupportValidationPrompt, "stating that a rule is rabbinic does not answer why");
        StringAssert.Contains(groundedPrompts.SystemBehaviorPrompt, "Who was Rabbi Akiva?");
        StringAssert.Contains(groundedPrompts.SystemBehaviorPrompt, "Vampires are fictional");
        StringAssert.Contains(groundedPrompts.SupportValidationPrompt, "The draft's label is not authority to bypass these requirements");
        using var schema = JsonDocument.Parse(groundedPrompts.ResponseJsonSchema);
        var claimSchema = schema.RootElement.GetProperty("properties").GetProperty("claims").GetProperty("items");
        CollectionAssert.AreEqual(new[] { "Source", "Background", "Uncertainty" }, claimSchema.GetProperty("properties").GetProperty("kind").GetProperty("enum").EnumerateArray().Select(value => value.GetString()).ToArray());
        CollectionAssert.Contains(claimSchema.GetProperty("required").EnumerateArray().Select(value => value.GetString()).ToArray(), "kind");
    }

    /// <summary>Verifies production CORS rejects credentialed access from website and development origins.</summary>
    /// <param name="origin">An origin outside the production application.</param>
    /// <returns>The asynchronous test operation.</returns>
    [TestMethod]
    [DataRow("https://askarabbi.ai")]
    [DataRow("https://www.askarabbi.ai")]
    [DataRow("http://localhost:5173")]
    [TestCategory("Regression")]
    public async Task Preflight_NonApplicationOrigin_DoesNotAllowCredentials(string origin)
    {
        await using var application = new TestApplicationFactory(environmentName: Environments.Production, corsOrigin: null);
        using var client = application.CreateNonRedirectingClient();
        client.BaseAddress = new Uri("https://api.askarabbi.ai");
        using var request = new HttpRequestMessage(HttpMethod.Options, "/api/user/session");
        request.Headers.Add("Origin", origin);
        request.Headers.Add("Access-Control-Request-Method", "GET");

        using var response = await client.SendAsync(request);

        Assert.AreEqual(HttpStatusCode.NoContent, response.StatusCode);
        Assert.IsFalse(response.Headers.Contains("Access-Control-Allow-Origin"));
        Assert.IsFalse(response.Headers.Contains("Access-Control-Allow-Credentials"));
    }

    /// <summary>Verifies each successful sign-in flow returns to the application subdomain.</summary>
    /// <param name="parameters">The login method or signup hint.</param>
    /// <returns>The asynchronous test operation.</returns>
    [TestMethod]
    [DataRow("?email=amitai%40example.com")]
    [DataRow("?provider=google")]
    [DataRow("?screen=sign-up")]
    [TestCategory("Regression")]
    public async Task Callback_ProductionLogin_RedirectsToApplication(string parameters)
    {
        await using var application = new TestApplicationFactory(environmentName: Environments.Production, corsOrigin: null);
        using var client = application.CreateNonRedirectingClient();
        client.BaseAddress = new Uri("https://api.askarabbi.ai");
        using var login = await client.GetAsync($"/api/user/login{parameters}");
        var state = QueryHelpers.ParseQuery(login.Headers.Location!.Query)["state"].ToString();

        using var callback = await client.GetAsync($"/api/user/callback?code=test-code&state={Uri.EscapeDataString(state)}");
        using var session = await client.GetAsync("/api/user/session");

        Assert.AreEqual(HttpStatusCode.Redirect, callback.StatusCode);
        Assert.AreEqual("https://app.askarabbi.ai/", callback.Headers.Location?.AbsoluteUri);
        Assert.AreEqual(HttpStatusCode.OK, session.StatusCode);
    }

    /// <summary>Verifies both registration-capacity checks return to the application subdomain.</summary>
    /// <param name="atCallback">Whether capacity fills after hosted signup begins.</param>
    /// <returns>The asynchronous test operation.</returns>
    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    [TestCategory("Regression")]
    public async Task SignUp_ProductionRegistrationFull_RedirectsToApplication(bool atCallback)
    {
        await using var application = new TestApplicationFactory(environmentName: Environments.Production, corsOrigin: null) { AccountLimit = 1 };
        using var client = application.CreateNonRedirectingClient();
        client.BaseAddress = new Uri("https://api.askarabbi.ai");
        var requestUri = "/api/user/login?screen=sign-up";
        if (atCallback)
        {
            using var login = await client.GetAsync(requestUri);
            var state = QueryHelpers.ParseQuery(login.Headers.Location!.Query)["state"].ToString();
            requestUri = $"/api/user/callback?code=test-code&state={Uri.EscapeDataString(state)}";
        }
        application.Store.AdditionalAccountIdentities.Add("existing");

        using var response = await client.GetAsync(requestUri);

        Assert.AreEqual(HttpStatusCode.Redirect, response.StatusCode);
        Assert.AreEqual("https://app.askarabbi.ai/?registration=closed", response.Headers.Location?.AbsoluteUri);
        Assert.IsNull(await application.Store.GetByIdAsync(application.Store.UserId));
    }

    /// <summary>Verifies WorkOS handles the API callback and returns sign-out to the application.</summary>
    /// <returns>The asynchronous test operation.</returns>
    [TestMethod]
    [TestCategory("Regression")]
    public async Task WorkOs_ProductionUrls_UseApiCallbackAndApplicationLogout()
    {
        await using var application = new TestApplicationFactory(environmentName: Environments.Production, corsOrigin: null);
        var configured = application.Services.GetRequiredService<WorkOsAuthenticationOptions>();
        var options = new WorkOsAuthenticationOptions
        {
            ApiKey = "test-key",
            ClientId = "client_test",
            RedirectUri = configured.RedirectUri,
            FrontendUri = configured.FrontendUri,
        };
        var service = new WorkOsUserAuthenticationService(new WorkOSClient(new WorkOSOptions { ApiKey = options.ApiKey, ClientId = options.ClientId }), options);

        var authorization = service.GetAuthorizationUri(new AuthorizationRequest { State = "test-state", CodeChallenge = new string('a', 43) });
        var logout = service.GetLogoutUri("session_test");

        Assert.AreEqual("https://api.askarabbi.ai/api/user/callback", QueryHelpers.ParseQuery(authorization.Query)["redirect_uri"].ToString());
        Assert.AreEqual("https://app.askarabbi.ai/", QueryHelpers.ParseQuery(logout.Query)["return_to"].ToString());
    }
}
