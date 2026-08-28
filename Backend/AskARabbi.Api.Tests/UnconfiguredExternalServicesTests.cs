using System.Net;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace AskARabbi.Api.Tests;

[TestClass]
public sealed class UnconfiguredExternalServicesTests
{
    [TestMethod]
    [TestCategory("Integration")]
    public async Task HealthAndLogin_ExternalServicesUnconfigured_HealthWorksAndLoginFailsExplicitly()
    {
        await using var application = new TestApplicationFactory(false);
        using var client = application.CreateNonRedirectingClient();

        using var healthResponse = await client.GetAsync("/health");
        using var loginResponse = await client.GetAsync("/api/user/login");
        var problem = await loginResponse.Content.ReadAsStringAsync();

        Assert.AreEqual(HttpStatusCode.OK, healthResponse.StatusCode);
        Assert.AreEqual(HttpStatusCode.ServiceUnavailable, loginResponse.StatusCode);
        StringAssert.Contains(problem, "authentication_unavailable");
    }
}
