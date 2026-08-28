using System.Net;
using System.Net.Http.Json;
using AskARabbi.Api.Authentication;
using AskARabbi.Api.Contracts.Users;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace AskARabbi.Api.Tests;

[TestClass]
public sealed class UserControllerTests
{
    [TestMethod]
    [TestCategory("Integration")]
    public async Task Login_AnonymousRequest_RedirectsWithAntiForgeryState()
    {
        await using var application = new TestApplicationFactory();
        using var client = application.CreateNonRedirectingClient();

        using var response = await client.GetAsync("/api/user/login");
        var state = QueryHelpers.ParseQuery(response.Headers.Location!.Query)["state"].ToString();

        Assert.AreEqual(HttpStatusCode.Redirect, response.StatusCode);
        Assert.AreEqual(64, state.Length);
        Assert.IsNotNull(application.Authentication.LastCodeChallenge);
        Assert.AreEqual(43, application.Authentication.LastCodeChallenge.Length);
        StringAssert.Contains(string.Join(';', response.Headers.GetValues("Set-Cookie")), "AskRabbi.AuthState=");
    }

    [TestMethod]
    [TestCategory("Integration")]
    public async Task Login_EmailGoogleSignUp_MapsHintsToWorkOsRequest()
    {
        await using var application = new TestApplicationFactory();
        using var client = application.CreateNonRedirectingClient();

        using var response = await client.GetAsync("/api/user/login?email=amitai%40example.com&provider=google&screen=sign-up");

        Assert.AreEqual(HttpStatusCode.Redirect, response.StatusCode);
        Assert.IsNotNull(application.Authentication.LastAuthorizationRequest);
        Assert.AreEqual("amitai@example.com", application.Authentication.LastAuthorizationRequest.LoginHint);
        Assert.AreEqual(ExternalAuthenticationProvider.Google, application.Authentication.LastAuthorizationRequest.Provider);
        Assert.IsTrue(application.Authentication.LastAuthorizationRequest.IsSignUp);
    }

    [TestMethod]
    [TestCategory("Integration")]
    public async Task Login_UnknownProvider_ReturnsBadRequestWithoutRedirect()
    {
        await using var application = new TestApplicationFactory();
        using var client = application.CreateNonRedirectingClient();

        using var response = await client.GetAsync("/api/user/login?provider=unknown");

        Assert.AreEqual(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.IsNull(application.Authentication.LastAuthorizationRequest);
    }

    [TestMethod]
    [TestCategory("Integration")]
    public async Task Callback_StateMismatch_RejectsWithoutCallingProvider()
    {
        await using var application = new TestApplicationFactory();
        using var client = application.CreateNonRedirectingClient();
        using var loginResponse = await client.GetAsync("/api/user/login");

        using var response = await client.GetAsync("/api/user/callback?code=test-code&state=wrong");

        Assert.AreEqual(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.AreEqual(0, application.Authentication.AuthenticateCallCount);
    }

    [TestMethod]
    [TestCategory("Integration")]
    public async Task Callback_ValidCode_CreatesApplicationSession()
    {
        await using var application = new TestApplicationFactory();
        using var client = await application.CreateAuthenticatedClientAsync();

        using var response = await client.GetAsync("/api/user/session");
        var session = await response.Content.ReadFromJsonAsync<UserSessionResponse>();

        Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
        Assert.IsNotNull(session);
        Assert.AreEqual("Amitai Erfanian", session.DisplayName);
        Assert.AreEqual("amitai@example.com", session.Email);
        Assert.IsTrue(session.IsEmailVerified);
        Assert.IsNotNull(application.Authentication.LastCodeVerifier);
        Assert.AreEqual(43, application.Authentication.LastCodeVerifier.Length);
    }

    [TestMethod]
    [TestCategory("Integration")]
    public async Task Session_ProviderTokenNearExpiry_RotatesWorkOsRefreshTokenOnce()
    {
        await using var application = new TestApplicationFactory();
        application.Authentication.InitialRefreshToken = "initial-refresh-token";
        application.Authentication.InitialAccessTokenExpiresAtUtc = new DateTimeOffset(2026, 8, 25, 12, 32, 0, TimeSpan.Zero);
        application.Authentication.RefreshedIdentity = new AuthenticatedIdentity(new AskARabbiLIB.Accounts.ExternalUserIdentity
        {
            ProviderUserId = "user_workos_01",
            Email = "amitai@example.com",
            IsEmailVerified = true,
            FirstName = "Amitai",
            LastName = "Erfanian",
        }, "session_workos_01", "rotated-refresh-token", new DateTimeOffset(2026, 8, 25, 13, 30, 0, TimeSpan.Zero));
        using var client = await application.CreateAuthenticatedClientAsync();

        using var firstResponse = await client.GetAsync("/api/user/session");
        using var secondResponse = await client.GetAsync("/api/user/session");

        Assert.AreEqual(HttpStatusCode.OK, firstResponse.StatusCode);
        Assert.AreEqual(HttpStatusCode.OK, secondResponse.StatusCode);
        Assert.AreEqual(1, application.Authentication.RefreshCallCount);
        Assert.AreEqual("initial-refresh-token", application.Authentication.LastRefreshToken);
    }

    [TestMethod]
    [TestCategory("Integration")]
    public async Task PasswordRecovery_ValidRequests_DelegatesWithoutReturningSensitiveData()
    {
        await using var application = new TestApplicationFactory();
        using var client = application.CreateNonRedirectingClient();

        using var forgotResponse = await client.PostAsJsonAsync("/api/user/forgot-password", new { email = "  amitai@example.com  " });
        using var resetResponse = await client.PostAsJsonAsync("/api/user/reset-password", new { token = " reset-token ", newPassword = "LongEnoughPassword!42" });

        Assert.AreEqual(HttpStatusCode.Accepted, forgotResponse.StatusCode);
        Assert.AreEqual(0, (await forgotResponse.Content.ReadAsByteArrayAsync()).Length);
        Assert.AreEqual("amitai@example.com", application.Authentication.LastResetEmail);
        Assert.AreEqual(HttpStatusCode.NoContent, resetResponse.StatusCode);
        Assert.AreEqual("reset-token", application.Authentication.LastResetToken);
        Assert.AreEqual("LongEnoughPassword!42", application.Authentication.LastNewPassword);
    }

    [TestMethod]
    [TestCategory("Integration")]
    public async Task Logout_AuthenticatedRequest_ClearsApplicationSession()
    {
        await using var application = new TestApplicationFactory();
        using var client = await application.CreateAuthenticatedClientAsync();

        using var logoutResponse = await client.PostAsync("/api/user/logout", null);
        var logout = await logoutResponse.Content.ReadFromJsonAsync<LogoutResponse>();
        using var sessionResponse = await client.GetAsync("/api/user/session");

        Assert.AreEqual(HttpStatusCode.OK, logoutResponse.StatusCode);
        Assert.IsNotNull(logout);
        StringAssert.StartsWith(logout.RedirectUri, "https://auth.example.test/logout");
        Assert.AreEqual(HttpStatusCode.Unauthorized, sessionResponse.StatusCode);
    }
}
