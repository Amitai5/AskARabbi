using AskARabbiLIB.Accounts;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;

namespace AskARabbi.Api.Authentication;

internal sealed class WorkOsCookieAuthenticationEvents : CookieAuthenticationEvents
{
    private const string RefreshTokenName = "workos_refresh_token";
    private const string AccessTokenExpiresAtName = "workos_access_token_expires_at";
    private static readonly TimeSpan RefreshWindow = TimeSpan.FromMinutes(5);
    private readonly IUserAuthenticationService authenticationService;
    private readonly IUserAccountStore userAccounts;
    private readonly TimeProvider timeProvider;
    private readonly ILogger<WorkOsCookieAuthenticationEvents> logger;

    /// <summary>Initializes cookie-session validation against WorkOS.</summary>
    /// <param name="authenticationService">WorkOS authentication boundary.</param>
    /// <param name="userAccounts">Application account store.</param>
    /// <param name="timeProvider">UTC time source.</param>
    /// <param name="logger">Structured logger.</param>
    public WorkOsCookieAuthenticationEvents(IUserAuthenticationService authenticationService, IUserAccountStore userAccounts, TimeProvider timeProvider, ILogger<WorkOsCookieAuthenticationEvents> logger)
    {
        this.authenticationService = authenticationService ?? throw new ArgumentNullException(nameof(authenticationService));
        this.userAccounts = userAccounts ?? throw new ArgumentNullException(nameof(userAccounts));
        this.timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
        this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc/>
    public override async Task ValidatePrincipal(CookieValidatePrincipalContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        var refreshToken = context.Properties.GetTokenValue(RefreshTokenName);
        var expiresAtValue = context.Properties.GetTokenValue(AccessTokenExpiresAtName);
        if (string.IsNullOrWhiteSpace(refreshToken) || !DateTimeOffset.TryParse(expiresAtValue, null, System.Globalization.DateTimeStyles.RoundtripKind, out var expiresAtUtc))
        {
            return;
        }

        var now = timeProvider.GetUtcNow();
        if (expiresAtUtc > now.Add(RefreshWindow))
        {
            return;
        }

        try
        {
            var refreshed = await authenticationService.RefreshSessionAsync(refreshToken, context.HttpContext.RequestAborted).ConfigureAwait(false);
            if (string.IsNullOrWhiteSpace(refreshed.RefreshToken) || refreshed.AccessTokenExpiresAtUtc is null)
            {
                await RejectSessionAsync(context).ConfigureAwait(false);
                return;
            }

            var account = await userAccounts.UpsertAsync(refreshed.User, now, context.HttpContext.RequestAborted).ConfigureAwait(false);
            context.ReplacePrincipal(ApplicationPrincipalFactory.Create(account, refreshed.SessionId));
            StoreSessionTokens(context.Properties, refreshed);
            context.ShouldRenew = true;
        }
        catch (IdentityProviderUnavailableException) when (expiresAtUtc > now)
        {
            logger.LogWarning("WorkOS session refresh is temporarily unavailable; retaining the unexpired application session.");
        }
        catch (IdentityProviderUnavailableException)
        {
            logger.LogWarning("WorkOS session refresh failed after provider token expiration; rejecting the application session.");
            await RejectSessionAsync(context).ConfigureAwait(false);
        }
        catch (IdentityRequestRejectedException)
        {
            logger.LogInformation("WorkOS rejected a session refresh; rejecting the application session.");
            await RejectSessionAsync(context).ConfigureAwait(false);
        }
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
        context.RejectPrincipal();
        await context.HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme).ConfigureAwait(false);
    }
}
