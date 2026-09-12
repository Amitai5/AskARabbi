using System.Globalization;
using System.Net;
using AskARabbi.Api.Authentication;
using AskARabbiLIB.Accounts;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.Net.Http.Headers;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace AskARabbi.Api.Tests;

[TestClass]
[TestCategory("Regression")]
public sealed class PersistentSessionTests
{
    private const string CookieName = "AskRabbi.Session";
    private const string SessionStartedAtKey = "AskRabbi.SessionStartedAtUtc";
    private const string ActivityCookieName = "AskRabbi.SessionActivity";
    private const string SessionNonceKey = "AskRabbi.SessionNonce";
    private static readonly DateTimeOffset SignInTime = new(2026, 10, 1, 12, 0, 0, TimeSpan.Zero);

    [TestMethod]
    public async Task Callback_ValidLogin_IssuesSecurePersistentCookieWithThirtyDayMaximum()
    {
        var clock = new AdjustableTimeProvider(SignInTime);
        await using var application = new TestApplicationFactory { Clock = clock };

        var cookie = await SignInAsync(application);
        var ticket = ReadTicket(application, cookie);

        Assert.IsTrue(cookie.Authentication.Secure);
        Assert.IsTrue(cookie.Authentication.HttpOnly);
        Assert.IsTrue(cookie.Activity.Secure);
        Assert.IsTrue(cookie.Activity.HttpOnly);
        Assert.AreEqual("strict", cookie.Authentication.SameSite.ToString().ToLowerInvariant());
        Assert.AreEqual("strict", cookie.Activity.SameSite.ToString().ToLowerInvariant());
        Assert.AreEqual(SignInTime.AddDays(30), cookie.Authentication.Expires);
        Assert.AreEqual(SignInTime.AddDays(7), cookie.Activity.Expires);
        Assert.IsTrue(ticket.Properties.IsPersistent);
        Assert.AreEqual(SignInTime.AddDays(30), ticket.Properties.ExpiresUtc);
        Assert.AreEqual(SignInTime, ReadTimestamp(ticket, SessionStartedAtKey));
        Assert.AreEqual(SignInTime, ReadActivityTimestamp(application, cookie));
    }

    [TestMethod]
    public async Task Session_BrowserReopensOnNewApiInstanceWithinSevenDays_KeepsOriginalLogin()
    {
        var clock = new AdjustableTimeProvider(SignInTime);
        var original = new TestApplicationFactory { Clock = clock };
        BrowserSession cookie;
        try
        {
            cookie = await SignInAsync(original);
        }
        finally
        {
            await original.DisposeAsync();
        }
        clock.UtcNow = SignInTime.AddDays(6);
        await using var restarted = new TestApplicationFactory { Clock = clock, Store = original.Store, SessionKeys = original.SessionKeys };
        using var reopenedBrowser = CreateBrowser(restarted, cookie);

        using var response = await reopenedBrowser.GetAsync("/api/user/session");

        Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
        Assert.AreEqual(0, restarted.Authentication.AuthenticateCallCount);
        Assert.IsTrue(response.Headers.CacheControl?.NoStore);
        Assert.IsTrue(response.Headers.CacheControl?.NoCache);
        var renewedCookies = ReadSessionCookies(response, cookie);
        var renewed = ReadTicket(restarted, renewedCookies);
        Assert.AreEqual(SignInTime, ReadTimestamp(renewed, SessionStartedAtKey));
        Assert.AreEqual(clock.UtcNow, ReadActivityTimestamp(restarted, renewedCookies));
        Assert.AreEqual(SignInTime.AddDays(30), renewed.Properties.ExpiresUtc);
    }

    [TestMethod]
    [DataRow(-1, HttpStatusCode.OK)]
    [DataRow(0, HttpStatusCode.Unauthorized)]
    [DataRow(1, HttpStatusCode.Unauthorized)]
    public async Task Session_SevenDayInactivityBoundary_EnforcesCutoff(int secondsFromCutoff, HttpStatusCode expectedStatus)
    {
        var clock = new AdjustableTimeProvider(SignInTime);
        await using var application = new TestApplicationFactory { Clock = clock };
        var cookie = await SignInAsync(application);
        clock.UtcNow = SignInTime.AddDays(7).AddSeconds(secondsFromCutoff);
        using var browser = CreateBrowser(application, cookie);

        using var response = await browser.GetAsync("/api/user/session");

        Assert.AreEqual(expectedStatus, response.StatusCode);
        Assert.AreEqual(0, application.Authentication.RefreshCallCount);
        if (expectedStatus == HttpStatusCode.Unauthorized)
        {
            var cleared = ReadSessionCookies(response);
            Assert.IsTrue(cleared.Authentication.Expires < clock.UtcNow);
            Assert.IsTrue(cleared.Activity.Expires < clock.UtcNow);
        }
    }

    [TestMethod]
    public async Task Session_ActivityBeforeDefaultSlidingRenewalThreshold_ResetsInactivityClock()
    {
        var clock = new AdjustableTimeProvider(SignInTime);
        await using var application = new TestApplicationFactory { Clock = clock };
        var cookie = await SignInAsync(application);
        clock.UtcNow = SignInTime.AddDays(1);
        using var firstBrowser = CreateBrowser(application, cookie);
        using var activity = await firstBrowser.GetAsync("/api/user/session");
        Assert.AreEqual(HttpStatusCode.OK, activity.StatusCode);
        cookie = ReadSessionCookies(activity, cookie);
        clock.UtcNow = SignInTime.AddDays(8).AddSeconds(-1);
        using var returningBrowser = CreateBrowser(application, cookie);

        using var response = await returningBrowser.GetAsync("/api/user/session");

        Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
        Assert.AreEqual(SignInTime.AddDays(30), ReadSessionCookies(response, cookie).Authentication.Expires);
    }

    [TestMethod]
    [DataRow(-1, HttpStatusCode.OK)]
    [DataRow(0, HttpStatusCode.Unauthorized)]
    [DataRow(1, HttpStatusCode.Unauthorized)]
    public async Task Session_RegularActivityThroughThirtyDayBoundary_DoesNotExtendMaximum(int secondsFromCutoff, HttpStatusCode expectedStatus)
    {
        var clock = new AdjustableTimeProvider(SignInTime);
        await using var application = new TestApplicationFactory { Clock = clock };
        var cookie = await SignInAsync(application);
        foreach (var day in new[] { 6, 12, 18, 24, 29 })
        {
            clock.UtcNow = SignInTime.AddDays(day);
            using var activeBrowser = CreateBrowser(application, cookie);
            using var activity = await activeBrowser.GetAsync("/api/user/session");
            Assert.AreEqual(HttpStatusCode.OK, activity.StatusCode);
            cookie = ReadSessionCookies(activity, cookie);
            Assert.AreEqual(SignInTime.AddDays(30), cookie.Authentication.Expires);
            Assert.IsTrue(cookie.Activity.Expires <= SignInTime.AddDays(30));
        }
        clock.UtcNow = SignInTime.AddDays(30).AddSeconds(secondsFromCutoff);
        using var browser = CreateBrowser(application, cookie);

        using var response = await browser.GetAsync("/api/user/session");

        Assert.AreEqual(expectedStatus, response.StatusCode);
    }

    [TestMethod]
    public async Task Session_ProviderRefresh_DoesNotRestartThirtyDayClock()
    {
        var clock = new AdjustableTimeProvider(SignInTime);
        await using var application = new TestApplicationFactory { Clock = clock };
        application.Authentication.InitialRefreshToken = "initial-refresh-token";
        application.Authentication.InitialAccessTokenExpiresAtUtc = SignInTime.AddHours(1);
        application.Authentication.RefreshedIdentity = new AuthenticatedIdentity(new ExternalUserIdentity
        {
            ProviderUserId = "user_workos_01",
            Email = "amitai@example.com",
            IsEmailVerified = true,
        }, "session_workos_01", "rotated-refresh-token", SignInTime.AddDays(6).AddHours(1));
        var cookie = await SignInAsync(application);
        clock.UtcNow = SignInTime.AddDays(6);
        using var browser = CreateBrowser(application, cookie);

        using var response = await browser.GetAsync("/api/user/session");

        Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
        Assert.AreEqual(1, application.Authentication.RefreshCallCount);
        var renewed = ReadTicket(application, ReadSessionCookies(response, cookie));
        Assert.AreEqual(SignInTime, ReadTimestamp(renewed, SessionStartedAtKey));
        Assert.AreEqual(SignInTime.AddDays(30), renewed.Properties.ExpiresUtc);
        Assert.AreEqual("rotated-refresh-token", renewed.Properties.GetTokenValue("workos_refresh_token"));
    }

    [TestMethod]
    public async Task Session_ProviderTokenStillValid_OnlyUpdatesActivityWithoutReissuingCredentials()
    {
        var clock = new AdjustableTimeProvider(SignInTime);
        await using var application = new TestApplicationFactory { Clock = clock };
        application.Authentication.InitialRefreshToken = "initial-refresh-token";
        application.Authentication.InitialAccessTokenExpiresAtUtc = SignInTime.AddHours(1);
        var cookie = await SignInAsync(application);
        clock.UtcNow = SignInTime.AddMinutes(15);
        using var browser = CreateBrowser(application, cookie);

        using var response = await browser.GetAsync("/api/user/session");

        Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
        Assert.AreEqual(0, application.Authentication.RefreshCallCount);
        var headers = SetCookieHeaderValue.ParseList(response.Headers.GetValues("Set-Cookie").ToList());
        Assert.IsFalse(headers.Any(header => header.Name == CookieName), "Non-refresh responses must not overwrite newer provider credentials from another request.");
        var renewed = ReadSessionCookies(response, cookie);
        Assert.AreEqual(clock.UtcNow, ReadActivityTimestamp(application, renewed));
        Assert.AreEqual(clock.UtcNow.AddDays(7), renewed.Activity.Expires);
    }

    [TestMethod]
    [DataRow("revoked", HttpStatusCode.Unauthorized)]
    [DataRow("unavailable-expired", HttpStatusCode.Unauthorized)]
    [DataRow("unavailable-unexpired", HttpStatusCode.OK)]
    [DataRow("wrong-user", HttpStatusCode.Unauthorized)]
    public async Task Session_ProviderRefreshFailure_PreservesExistingRevocationRules(string scenario, HttpStatusCode expectedStatus)
    {
        var clock = new AdjustableTimeProvider(SignInTime);
        await using var application = new TestApplicationFactory { Clock = clock };
        application.Authentication.InitialRefreshToken = "initial-refresh-token";
        application.Authentication.InitialAccessTokenExpiresAtUtc = SignInTime.AddMinutes(scenario == "unavailable-unexpired" ? 2 : -1);
        if (scenario == "wrong-user")
        {
            application.Authentication.RefreshedIdentity = new AuthenticatedIdentity(new ExternalUserIdentity
            {
                ProviderUserId = "different-user",
                Email = "different@example.test",
                IsEmailVerified = true,
            }, "different-session", "rotated-refresh-token", SignInTime.AddHours(1));
        }
        else
        {
            application.Authentication.RefreshFailure = scenario == "revoked" ? new IdentityRequestRejectedException("Session revoked.") : new IdentityProviderUnavailableException();
        }
        var cookie = await SignInAsync(application);
        using var browser = CreateBrowser(application, cookie);

        using var response = await browser.GetAsync("/api/user/session");

        Assert.AreEqual(expectedStatus, response.StatusCode);
        Assert.AreEqual(1, application.Authentication.RefreshCallCount);
        if (expectedStatus == HttpStatusCode.Unauthorized)
        {
            var cleared = ReadSessionCookies(response);
            Assert.IsTrue(cleared.Authentication.Expires < clock.UtcNow);
            Assert.IsTrue(cleared.Activity.Expires < clock.UtcNow);
        }
    }

    [TestMethod]
    public async Task Session_InactiveWithExpiredProviderToken_RejectsBeforeRefreshingProvider()
    {
        var clock = new AdjustableTimeProvider(SignInTime);
        await using var application = new TestApplicationFactory { Clock = clock };
        application.Authentication.InitialRefreshToken = "initial-refresh-token";
        application.Authentication.InitialAccessTokenExpiresAtUtc = SignInTime.AddHours(1);
        var cookie = await SignInAsync(application);
        clock.UtcNow = SignInTime.AddDays(7);
        using var browser = CreateBrowser(application, cookie);

        using var response = await browser.GetAsync("/api/user/session");

        Assert.AreEqual(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.AreEqual(0, application.Authentication.RefreshCallCount);
    }

    [TestMethod]
    [DataRow("tampered")]
    [DataRow("different-session")]
    public async Task Session_UntrustedActivityCookie_RequiresFreshSignIn(string scenario)
    {
        var clock = new AdjustableTimeProvider(SignInTime);
        await using var application = new TestApplicationFactory { Clock = clock };
        var cookie = await SignInAsync(application);
        if (scenario == "different-session")
        {
            var otherSession = await SignInAsync(application);
            cookie.Activity.Value = otherSession.Activity.Value;
        }
        else
        {
            cookie.Activity.Value = "not-a-protected-activity-cookie";
        }
        using var browser = CreateBrowser(application, cookie);

        using var response = await browser.GetAsync("/api/user/session");

        Assert.AreEqual(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [TestMethod]
    [DataRow(2000)]
    [DataRow(2090)]
    public async Task Session_PersistentBrowserCookie_UsesInjectedClockInsteadOfRealDate(int year)
    {
        var clock = new AdjustableTimeProvider(new DateTimeOffset(year, 1, 1, 12, 0, 0, TimeSpan.Zero));
        await using var application = new TestApplicationFactory { Clock = clock };
        using var browser = await application.CreateAuthenticatedClientAsync();

        using var response = await browser.GetAsync("/api/user/session");

        Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
    }

    [TestMethod]
    public async Task Logout_PersistentSession_ClearsBothBrowserCookies()
    {
        var clock = new AdjustableTimeProvider(SignInTime);
        await using var application = new TestApplicationFactory { Clock = clock };
        using var browser = await application.CreateAuthenticatedClientAsync();

        using var logout = await browser.PostAsync("/api/user/logout", null);
        using var session = await browser.GetAsync("/api/user/session");

        Assert.AreEqual(HttpStatusCode.OK, logout.StatusCode);
        Assert.AreEqual(HttpStatusCode.Unauthorized, session.StatusCode);
        var cleared = ReadSessionCookies(logout);
        Assert.IsTrue(cleared.Authentication.Expires < clock.UtcNow);
        Assert.IsTrue(cleared.Activity.Expires < clock.UtcNow);
    }

    [TestMethod]
    [DataRow("missing-start")]
    [DataRow("missing-activity")]
    [DataRow("invalid-start")]
    [DataRow("future-start")]
    [DataRow("future-activity")]
    [DataRow("activity-before-start")]
    public async Task Session_InvalidOrLegacyLifetime_RequiresFreshSignIn(string scenario)
    {
        var clock = new AdjustableTimeProvider(SignInTime.AddDays(1));
        await using var application = new TestApplicationFactory { Clock = clock };
        var cookie = await SignInAsync(application);
        var ticket = ReadTicket(application, cookie);
        ticket.Properties.Items[SessionStartedAtKey] = SignInTime.ToString("O", CultureInfo.InvariantCulture);
        var activityAt = SignInTime;
        switch (scenario)
        {
            case "missing-start":
                ticket.Properties.Items.Remove(SessionStartedAtKey);
                break;
            case "missing-activity":
                cookie.Activity.Value = string.Empty;
                break;
            case "invalid-start":
                ticket.Properties.Items[SessionStartedAtKey] = "not-a-date";
                break;
            case "future-start":
                ticket.Properties.Items[SessionStartedAtKey] = clock.UtcNow.AddDays(1).ToString("O", CultureInfo.InvariantCulture);
                break;
            case "future-activity":
                activityAt = clock.UtcNow.AddDays(1);
                break;
            case "activity-before-start":
                activityAt = SignInTime.AddDays(-1);
                break;
        }
        cookie.Authentication.Value = CookieOptions(application).TicketDataFormat.Protect(ticket);
        if (scenario != "missing-activity")
        {
            cookie.Activity.Value = ActivityProtector(application).Protect(ticket.Properties.Items[SessionNonceKey] + "|" + activityAt.ToString("O", CultureInfo.InvariantCulture));
        }
        using var browser = CreateBrowser(application, cookie);

        using var response = await browser.GetAsync("/api/user/session");

        Assert.AreEqual(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.AreEqual(0, application.Authentication.RefreshCallCount);
    }

    private static async Task<BrowserSession> SignInAsync(TestApplicationFactory application)
    {
        using var browser = application.CreateNonRedirectingClient();
        using var login = await browser.GetAsync("/api/user/login");
        Assert.IsNotNull(login.Headers.Location);
        var state = QueryHelpers.ParseQuery(login.Headers.Location.Query)["state"].ToString();
        using var callback = await browser.GetAsync($"/api/user/callback?code=test-code&state={Uri.EscapeDataString(state)}");
        Assert.AreEqual(HttpStatusCode.Redirect, callback.StatusCode);
        return ReadSessionCookies(callback);
    }

    private static HttpClient CreateBrowser(TestApplicationFactory application, BrowserSession cookie)
    {
        var browser = application.CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://localhost"),
            AllowAutoRedirect = false,
            HandleCookies = false,
        });
        browser.DefaultRequestHeaders.Add("Cookie", $"{cookie.Authentication.Name}={cookie.Authentication.Value}; {cookie.Activity.Name}={cookie.Activity.Value}");
        return browser;
    }

    private static BrowserSession ReadSessionCookies(HttpResponseMessage response, BrowserSession? previous = null)
    {
        var cookies = SetCookieHeaderValue.ParseList(response.Headers.GetValues("Set-Cookie").ToList());
        var authentication = cookies.SingleOrDefault(cookie => cookie.Name == CookieName) ?? previous?.Authentication;
        var activity = cookies.SingleOrDefault(cookie => cookie.Name == ActivityCookieName) ?? previous?.Activity;
        Assert.IsNotNull(authentication);
        Assert.IsNotNull(activity);
        return new BrowserSession(authentication, activity);
    }

    private static IDataProtector ActivityProtector(TestApplicationFactory application) => application.Services.GetRequiredService<IDataProtectionProvider>().CreateProtector("AskARabbi.SessionActivity.v1");

    private static DateTimeOffset ReadActivityTimestamp(TestApplicationFactory application, BrowserSession cookie)
    {
        var activity = ActivityProtector(application).Unprotect(cookie.Activity.Value.ToString());
        return DateTimeOffset.Parse(activity[(activity.IndexOf('|') + 1)..], CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind);
    }

    private static CookieAuthenticationOptions CookieOptions(TestApplicationFactory application) => application.Services.GetRequiredService<IOptionsMonitor<CookieAuthenticationOptions>>().Get(CookieAuthenticationDefaults.AuthenticationScheme);

    private static AuthenticationTicket ReadTicket(TestApplicationFactory application, BrowserSession cookie)
    {
        var ticket = CookieOptions(application).TicketDataFormat.Unprotect(cookie.Authentication.Value.ToString());
        Assert.IsNotNull(ticket);
        return ticket;
    }

    private sealed record BrowserSession(SetCookieHeaderValue Authentication, SetCookieHeaderValue Activity);

    private static DateTimeOffset ReadTimestamp(AuthenticationTicket ticket, string key)
    {
        Assert.IsTrue(ticket.Properties.Items.TryGetValue(key, out var value));
        Assert.IsNotNull(value);
        return DateTimeOffset.Parse(value, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind);
    }

    private sealed class AdjustableTimeProvider(DateTimeOffset utcNow) : TimeProvider
    {
        internal DateTimeOffset UtcNow { get; set; } = utcNow;

        public override DateTimeOffset GetUtcNow() => UtcNow;
    }
}
