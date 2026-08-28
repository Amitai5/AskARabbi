using System.Net;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace AskARabbi.Api.Tests;

[TestClass]
public sealed class CorsPolicyTests
{
    [TestMethod]
    [TestCategory("Integration")]
    public async Task Preflight_ConfiguredFrontendOrigin_AllowsCredentialedRequest()
    {
        await using var application = new TestApplicationFactory(environmentName: Microsoft.Extensions.Hosting.Environments.Development);
        using var client = application.CreateNonRedirectingClient();
        using var request = new HttpRequestMessage(HttpMethod.Options, "/api/conversations");
        request.Headers.Add("Origin", "http://localhost:5173");
        request.Headers.Add("Access-Control-Request-Method", "GET");

        using var response = await client.SendAsync(request);

        Assert.AreEqual(HttpStatusCode.NoContent, response.StatusCode);
        Assert.AreEqual("http://localhost:5173", response.Headers.GetValues("Access-Control-Allow-Origin").Single());
        Assert.AreEqual("true", response.Headers.GetValues("Access-Control-Allow-Credentials").Single());
    }

    [TestMethod]
    [TestCategory("Integration")]
    public async Task Preflight_UnconfiguredOrigin_DoesNotReturnAllowOrigin()
    {
        await using var application = new TestApplicationFactory(environmentName: Microsoft.Extensions.Hosting.Environments.Development);
        using var client = application.CreateNonRedirectingClient();
        using var request = new HttpRequestMessage(HttpMethod.Options, "/api/conversations");
        request.Headers.Add("Origin", "https://malicious.example");
        request.Headers.Add("Access-Control-Request-Method", "GET");

        using var response = await client.SendAsync(request);

        Assert.AreEqual(HttpStatusCode.NoContent, response.StatusCode);
        Assert.IsFalse(response.Headers.Contains("Access-Control-Allow-Origin"));
    }
}
