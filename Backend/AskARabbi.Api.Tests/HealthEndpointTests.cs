using System.Net;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace AskARabbi.Api.Tests;

[TestClass]
public sealed class HealthEndpointTests
{
    [TestMethod]
    [TestCategory("Integration")]
    public async Task GetHealth_WhenApplicationStarts_ReturnsHealthy()
    {
        await using var application = new TestApplicationFactory();
        using var client = application.CreateNonRedirectingClient();

        using var response = await client.GetAsync("/health");
        var responseBody = await response.Content.ReadAsStringAsync();

        Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
        Assert.AreEqual("Healthy", responseBody);
    }
}
