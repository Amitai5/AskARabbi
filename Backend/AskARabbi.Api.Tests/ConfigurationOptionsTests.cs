using AskARabbi.Api.Authentication;
using AskARabbi.Api.Configuration;
using AskARabbi.Api.Development;
using AskARabbi.Api.Usage;
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
}
