using AskARabbi.Api.Authentication;
using AskARabbiLIB.Grounding;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.VisualStudio.TestTools.UnitTesting;

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
        request.Headers.Add("Origin", "https://askarabbi.ai");
        request.Headers.Add("Access-Control-Request-Method", "GET");

        using var response = await client.SendAsync(request);
        var workOs = application.Services.GetRequiredService<WorkOsAuthenticationOptions>();
        var groundedPrompts = application.Services.GetRequiredService<GroundedPromptSet>();

        Assert.AreEqual("https://api.askarabbi.ai/api/user/callback", workOs.RedirectUri);
        Assert.AreEqual("https://askarabbi.ai/", workOs.FrontendUri);
        Assert.AreEqual("https://askarabbi.ai", response.Headers.GetValues("Access-Control-Allow-Origin").Single());
        Assert.IsTrue(response.Headers.GetValues("Access-Control-Allow-Credentials").Single().Equals("true", StringComparison.OrdinalIgnoreCase));
        StringAssert.StartsWith(groundedPrompts.CurrentQuestionInstruction, "Write one flowing, human answer.");
    }
}
