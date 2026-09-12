using AskARabbiLIB.Accounts;
using System.Globalization;
using System.Security.Claims;
using System.Security.Cryptography;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.DataProtection;

namespace AskARabbi.Api.Authentication;

internal sealed class WorkOsCookieAuthenticationEvents : CookieAuthenticationEvents
{
    private const string RefreshTokenName = "workos_refresh_token";
    private const string AccessTokenExpiresAtName = "workos_access_token_expires_at";
    private const string SessionStartedAtKey = "AskRabbi.SessionStartedAtUtc";
    private const string SessionNonceKey = "AskRabbi.SessionNonce";
    private const string ActivityCookieName = "AskRabbi.SessionActivity";
    private static readonly TimeSpan RefreshWindow = TimeSpan.FromMinutes(5);
    private readonly IUserAuthenticationService authenticationService;
    private readonly IUserAccountStore userAccounts;
    private readonly TimeProvider timeProvider;
    private readonly SessionLifetimeOptions sessionLifetime;
    private readonly IDataProtector activityProtector;
    private readonly ILogger<WorkOsCookieAuthenticationEvents> logger;

    /// <summary>Initializes cookie-session validation against WorkOS.</summary>
    /// <param name="authenticationService">WorkOS authentication boundary.</param>
    /// <param name="userAccounts">Application account store.</param>
    /// <param name="timeProvider">UTC time source.</param>
    /// <param name="logger">Structured logger.</param>
    /// <param name="sessionLifetime">Maximum and inactivity limits for browser sessions.</param>
    /// <param name="dataProtectionProvider">Shared encryption keys for browser activity cookies.</param>
    public WorkOsCookieAuthenticationEvents(IUserAuthenticationService authenticationService, IUserAccountStore userAccounts, TimeProvider timeProvider, ILogger<WorkOsCookieAuthenticationEvents> logger, SessionLifetimeOptions sessionLifetime, IDataProtectionProvider dataProtectionProvider)
    {
        this.authenticationService = authenticationService ?? throw new ArgumentNullException(nameof(authenticationService));
        this.userAccounts = userAccounts ?? throw new ArgumentNullException(nameof(userAccounts));
        this.timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
        this.sessionLifetime = sessionLifetime ?? throw new ArgumentNullException(nameof(sessionLifetime));
        this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
        ArgumentNullException.ThrowIfNull(dataProtectionProvider);
        activityProtector = dataProtectionProvider.CreateProtector("AskARabbi.SessionActivity.v1");
    }

    /// <inheritdoc/>
    public override Task SigningIn(CookieSigningInContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        var now = timeProvider.GetUtcNow();
        context.Properties.IsPersistent = true;
        context.Properties.AllowRefresh = true;
        context.Properties.IssuedUtc = now;
        context.Properties.ExpiresUtc = now.AddDays(sessionLifetime.MaximumLifetimeDays);
        context.Properties.Items[SessionStartedAtKey] = now.ToString("O", CultureInfo.InvariantCulture);
        var sessionNonce = Guid.NewGuid().ToString("N");
        context.Properties.Items[SessionNonceKey] = sessionNonce;
        WriteActivityCookie(context.HttpContext, context.Options, sessionNonce, now, now);
        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public override Task SigningOut(CookieSigningOutContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        context.Response.Cookies.Delete(ActivityCookieName, context.Options.Cookie.Build(context.HttpContext));
        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public override async Task ValidatePrincipal(CookieValidatePrincipalContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        var now = timeProvider.GetUtcNow();
        if (!TryReadSessionLifetime(context, now, out var startedAtUtc, out var sessionNonce))
        {
            await RejectSessionAsync(context).ConfigureAwait(false);
            return;
        }
        if (!Guid.TryParse(context.Principal?.FindFirstValue(ClaimTypes.NameIdentifier), out var userId))
        {
            await RejectSessionAsync(context).ConfigureAwait(false);
            return;
        }
        var account = await userAccounts.GetByIdAsync(userId, context.HttpContext.RequestAborted).ConfigureAwait(false);
        if (account is null || account.IsDeletionPending)
        {
            await RejectSessionAsync(context).ConfigureAwait(false);
            return;
        }

        if (!await ValidateProviderSessionAsync(context, account, now).ConfigureAwait(false))
        {
            await RejectSessionAsync(context).ConfigureAwait(false);
            return;
        }

        // Activity is separate from credentials: a slow response must not overwrite a newer rotated refresh token.
        WriteActivityCookie(context.HttpContext, context.Options, sessionNonce, startedAtUtc, now);
        if (context.ShouldRenew)
        {
            context.Properties.IssuedUtc = now;
            context.Properties.ExpiresUtc = startedAtUtc.AddDays(sessionLifetime.MaximumLifetimeDays);
        }
    }

    private async Task<bool> ValidateProviderSessionAsync(CookieValidatePrincipalContext context, UserAccount account, DateTimeOffset now)
    {
        var refreshToken = context.Properties.GetTokenValue(RefreshTokenName);
        var expiresAtValue = context.Properties.GetTokenValue(AccessTokenExpiresAtName);
        if (string.IsNullOrWhiteSpace(refreshToken) || !DateTimeOffset.TryParse(expiresAtValue, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var expiresAtUtc))
        {
            return true;
        }

        if (expiresAtUtc > now.Add(RefreshWindow))
        {
            return true;
        }

        try
        {
            var refreshed = await authenticationService.RefreshSessionAsync(refreshToken, context.HttpContext.RequestAborted).ConfigureAwait(false);
            if (string.IsNullOrWhiteSpace(refreshed.RefreshToken) || refreshed.AccessTokenExpiresAtUtc is null)
            {
                return false;
            }

            // Refresh is not registration: never recreate an erased account from an older cookie.
            if (refreshed.User.ProviderUserId != account.ProviderUserId)
            {
                return false;
            }
            context.ReplacePrincipal(ApplicationPrincipalFactory.Create(account, refreshed.SessionId));
            StoreSessionTokens(context.Properties, refreshed);
            context.ShouldRenew = true;
            return true;
        }
        catch (IdentityProviderUnavailableException) when (expiresAtUtc > now)
        {
            logger.LogWarning("WorkOS session refresh is temporarily unavailable; retaining the unexpired application session.");
            return true;
        }
        catch (IdentityProviderUnavailableException)
        {
            logger.LogWarning("WorkOS session refresh failed after provider token expiration; rejecting the application session.");
            return false;
        }
        catch (IdentityRequestRejectedException)
        {
            logger.LogInformation("WorkOS rejected a session refresh; rejecting the application session.");
            return false;
        }
    }

    private bool TryReadSessionLifetime(CookieValidatePrincipalContext context, DateTimeOffset now, out DateTimeOffset startedAtUtc, out string sessionNonce)
    {
        startedAtUtc = default;
        sessionNonce = string.Empty;
        // Old cookies have no trustworthy original sign-in time. Require one fresh sign-in instead of extending them.
        if (!context.Properties.Items.TryGetValue(SessionStartedAtKey, out var startedAtValue) ||
            !DateTimeOffset.TryParseExact(startedAtValue, "O", CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out startedAtUtc) ||
            !context.Properties.Items.TryGetValue(SessionNonceKey, out var nonceValue) || string.IsNullOrWhiteSpace(nonceValue) || !Guid.TryParseExact(nonceValue, "N", out _) ||
            !context.Request.Cookies.TryGetValue(ActivityCookieName, out var activityCookie) || string.IsNullOrWhiteSpace(activityCookie))
        {
            return false;
        }

        string activity;
        try
        {
            activity = activityProtector.Unprotect(activityCookie);
        }
        catch (CryptographicException)
        {
            return false;
        }

        var prefix = nonceValue + "|";
        if (!activity.StartsWith(prefix, StringComparison.Ordinal) ||
            !DateTimeOffset.TryParseExact(activity.AsSpan(prefix.Length), "O", CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var lastActivityAtUtc))
        {
            return false;
        }
        sessionNonce = nonceValue;
        return startedAtUtc <= lastActivityAtUtc && lastActivityAtUtc <= now &&
            now - startedAtUtc < TimeSpan.FromDays(sessionLifetime.MaximumLifetimeDays) &&
            now - lastActivityAtUtc < TimeSpan.FromDays(sessionLifetime.InactivityTimeoutDays);
    }

    private void WriteActivityCookie(HttpContext context, CookieAuthenticationOptions options, string sessionNonce, DateTimeOffset startedAtUtc, DateTimeOffset now)
    {
        var cookieOptions = options.Cookie.Build(context);
        var maximumExpiry = startedAtUtc.AddDays(sessionLifetime.MaximumLifetimeDays);
        var inactivityExpiry = now.AddDays(sessionLifetime.InactivityTimeoutDays);
        cookieOptions.Expires = inactivityExpiry < maximumExpiry ? inactivityExpiry : maximumExpiry;
        context.Response.Cookies.Append(ActivityCookieName, activityProtector.Protect(sessionNonce + "|" + now.ToString("O", CultureInfo.InvariantCulture)), cookieOptions);
    }

    /// <inheritdoc/>
    public override Task RedirectToLogin(RedirectContext<CookieAuthenticationOptions> context)
    {
        context.Response.StatusCode = StatusCodes.Status401Unauthorized;
        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public override Task RedirectToAccessDenied(RedirectContext<CookieAuthenticationOptions> context)
    {
        context.Response.StatusCode = StatusCodes.Status403Forbidden;
        return Task.CompletedTask;
    }

    internal static void StoreSessionTokens(AuthenticationProperties properties, AuthenticatedIdentity identity)
    {
        ArgumentNullException.ThrowIfNull(properties);
        ArgumentNullException.ThrowIfNull(identity);
        var tokens = new List<AuthenticationToken>();
        if (!string.IsNullOrWhiteSpace(identity.RefreshToken))
        {
            tokens.Add(new AuthenticationToken { Name = RefreshTokenName, Value = identity.RefreshToken });
        }
        if (identity.AccessTokenExpiresAtUtc is not null)
        {
            tokens.Add(new AuthenticationToken { Name = AccessTokenExpiresAtName, Value = identity.AccessTokenExpiresAtUtc.Value.ToString("O") });
        }
        properties.StoreTokens(tokens);
    }

    private static async Task RejectSessionAsync(CookieValidatePrincipalContext context)
    {
        context.ShouldRenew = false;
        context.RejectPrincipal();
        await context.HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme).ConfigureAwait(false);
    }
}
