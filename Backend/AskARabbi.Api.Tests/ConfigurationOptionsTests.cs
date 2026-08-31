using AskARabbi.Api.Authentication;
using AskARabbi.Api.Configuration;
using AskARabbi.Api.Development;
using AskARabbi.Api.Usage;
using AskARabbiLIB.AI;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace AskARabbi.Api.Tests;

[TestClass]
public sealed class ConfigurationOptionsTests
{
    [TestMethod]
    [TestCategory("Unit")]
    public void Validate_CompleteWorkOsOptions_DoesNotExposeApiKey()
    {
        const string apiKey = "secret-workos-key";
        var options = new WorkOsAuthenticationOptions
        {
            ApiKey = apiKey,
            ClientId = "client_01",
            RedirectUri = "https://api.askrabbi.test/api/user/callback",
            FrontendUri = "https://askrabbi.test/",
        };

        options.Validate();

        Assert.IsFalse(options.ToString()?.Contains(apiKey, StringComparison.Ordinal) ?? false);
    }

    [TestMethod]
    [DataRow("missing-credentials")]
    [DataRow("invalid-redirect")]
    [DataRow("invalid-frontend")]
    [TestCategory("Unit")]
    public void Validate_InvalidWorkOsOptions_Throws(string scenario)
    {
        var options = scenario switch
        {
            "missing-credentials" => new WorkOsAuthenticationOptions(),
            "invalid-redirect" => new WorkOsAuthenticationOptions { ApiKey = "key", ClientId = "client", RedirectUri = "relative" },
            _ => new WorkOsAuthenticationOptions { ApiKey = "key", ClientId = "client", FrontendUri = "relative" },
        };

        Assert.ThrowsExactly<InvalidOperationException>(() => options.Validate());
    }

    [TestMethod]
    [TestCategory("Unit")]
    public void Validate_ProductionWorkOsUrisUseHttp_Throws()
    {
        var options = new WorkOsAuthenticationOptions
        {
            ApiKey = "key",
            ClientId = "client",
            RedirectUri = "http://api.askarabbi.ai/api/user/callback",
            FrontendUri = "http://askarabbi.ai/",
        };

        Assert.ThrowsExactly<InvalidOperationException>(() => options.Validate(true));
    }

    [TestMethod]
    [DataRow("RedirectUri")]
    [DataRow("FrontendUri")]
    [TestCategory("Unit")]
    public void ValidateRedirectUris_NonWebScheme_Throws(string propertyName)
    {
        var options = new WorkOsAuthenticationOptions
        {
            RedirectUri = propertyName == nameof(WorkOsAuthenticationOptions.RedirectUri) ? "file:///tmp/callback" : "http://localhost:5090/api/user/callback",
            FrontendUri = propertyName == nameof(WorkOsAuthenticationOptions.FrontendUri) ? "file:///tmp/frontend" : "http://localhost:5173/",
        };

        Assert.ThrowsExactly<InvalidOperationException>(() => options.ValidateRedirectUris());
    }

    [TestMethod]
    [TestCategory("Unit")]
    public void Validate_NonPositiveUsageLimit_Throws()
    {
        var options = new MonthlyUsageOptions { MonthlyAnswerLimit = 0 };

        Assert.ThrowsExactly<InvalidOperationException>(options.Validate);
    }

    [TestMethod]
    [TestCategory("Unit")]
    public void GetAllowedOrigins_DevelopmentWithoutConfiguration_ReturnsViteOrigin()
    {
        var options = new FrontendCorsOptions();

        var origins = options.GetAllowedOrigins(true);

        CollectionAssert.AreEqual(new[] { "http://localhost:5173" }, origins.ToArray());
    }

    [TestMethod]
    [TestCategory("Unit")]
    public void GetAllowedOrigins_OriginWithPath_Throws()
    {
        var options = new FrontendCorsOptions { AllowedOrigins = ["https://askrabbi.test/application"] };

        Assert.ThrowsExactly<InvalidOperationException>(() => options.GetAllowedOrigins(false));
    }

    [TestMethod]
    [TestCategory("Unit")]
    public void Validate_LocalDemoOutsideDevelopment_Throws()
    {
        var options = new LocalDevelopmentOptions { UseDemoServices = true };

        Assert.ThrowsExactly<InvalidOperationException>(() => options.Validate("Production"));
    }

    [TestMethod]
    [TestCategory("Unit")]
    public void Validate_NoGroundedChatProviderConfiguration_AllowsProcessHealthMode()
    {
        var options = new GroundedChatOptions();

        options.Validate();

        Assert.IsFalse(options.IsConfigured);
    }

    [TestMethod]
    [TestCategory("Unit")]
    public void Validate_PartialGroundedChatConfiguration_Throws()
    {
        var options = new GroundedChatOptions { ProjectEndpoint = "https://openai.askrabbi.test/" };

        Assert.ThrowsExactly<InvalidOperationException>(options.Validate);
    }

    [TestMethod]
    [DataRow("validation-tokens")]
    [DataRow("retry-count")]
    [DataRow("reasoning-effort")]
    [DataRow("enrichment-hits")]
    [DataRow("cache-duration")]
    [DataRow("cache-capacity")]
    [TestCategory("Unit")]
    public void Validate_InvalidGroundedChatPerformanceLimit_Throws(string scenario)
    {
        var options = scenario switch
        {
            "validation-tokens" => new GroundedChatOptions { ValidationMaximumOutputTokens = 0 },
            "retry-count" => new GroundedChatOptions { MaximumRetryCount = 6 },
            "reasoning-effort" => new GroundedChatOptions { ReasoningEffort = (AIReasoningEffort)999 },
            "enrichment-hits" => new GroundedChatOptions { MaximumEnrichmentHits = 11 },
            "cache-duration" => new GroundedChatOptions { RetrievalCacheSeconds = 0 },
            "cache-capacity" => new GroundedChatOptions { RetrievalCacheMaximumEntries = 0 },
            _ => throw new AssertFailedException($"Unknown scenario '{scenario}'."),
        };

        Assert.ThrowsExactly<InvalidOperationException>(options.Validate);
    }

    [TestMethod]
    [TestCategory("Unit")]
    public void CreatePerformanceOptions_ValidConfiguration_MapsEveryLimit()
    {
        var options = new GroundedChatOptions
        {
            MaximumCandidates = 12,
            MaximumEvidenceSegments = 8,
            MaximumEvidenceCharacters = 12_000,
            MaximumCharactersPerSegment = 2_000,
            MaximumSegmentsPerDocument = 2,
            ContextRadius = 1,
            MaximumEnrichmentHits = 0,
            RecentConversationTurns = 2,
            RetrievalCacheSeconds = 90,
            RetrievalCacheMaximumEntries = 32,
        };

        options.Validate();
        var answerOptions = options.CreateGroundedAnswerOptions();
        var cacheOptions = options.CreateRetrieverCacheOptions();

        Assert.AreEqual(12, answerOptions.MaximumCandidates);
        Assert.AreEqual(8, answerOptions.MaximumEvidenceSegments);
        Assert.AreEqual(0, answerOptions.MaximumEnrichmentHits);
        Assert.AreEqual(2, answerOptions.RecentConversationTurns);
        Assert.AreEqual(TimeSpan.FromSeconds(90), cacheOptions.Duration);
        Assert.AreEqual(32, cacheOptions.MaximumEntries);
    }
}
