using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using AskARabbi.Api.Authentication;
using AskARabbi.Api.Contracts.Users;
using AskARabbiLIB.Accounts;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.WebUtilities;

namespace AskARabbi.Api.Controllers;

/// <summary>Handles WorkOS-hosted authentication and AskRabbi account sessions.</summary>
[ApiController]
[Route("api/user")]
public sealed class UserController : ControllerBase
{
    private const string AuthStateCookieName = "AskRabbi.AuthState";
    private const string PkceVerifierCookieName = "AskRabbi.PkceVerifier";
    private readonly IUserAuthenticationService authenticationService;
    private readonly IUserAccountStore userAccounts;
    private readonly ICurrentUser currentUser;
    private readonly TimeProvider timeProvider;
    private readonly WorkOsAuthenticationOptions options;
    private readonly IHostEnvironment environment;

    /// <summary>Initializes the user API.</summary>
    /// <param name="authenticationService">Identity-provider boundary.</param>
    /// <param name="userAccounts">User-account store.</param>
    /// <param name="currentUser">Current authenticated user accessor.</param>
    /// <param name="timeProvider">UTC time source.</param>
    /// <param name="options">WorkOS configuration.</param>
    /// <param name="environment">Current host environment.</param>
    public UserController(IUserAuthenticationService authenticationService, IUserAccountStore userAccounts, ICurrentUser currentUser, TimeProvider timeProvider, WorkOsAuthenticationOptions options, IHostEnvironment environment)
    {
        this.authenticationService = authenticationService ?? throw new ArgumentNullException(nameof(authenticationService));
        this.userAccounts = userAccounts ?? throw new ArgumentNullException(nameof(userAccounts));
        this.currentUser = currentUser ?? throw new ArgumentNullException(nameof(currentUser));
        this.timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
        this.options = options ?? throw new ArgumentNullException(nameof(options));
        this.environment = environment ?? throw new ArgumentNullException(nameof(environment));
    }

    /// <summary>Begins login through the WorkOS-hosted AuthKit interface.</summary>
    /// <param name="email">Optional email hint for hosted authentication.</param>
    /// <param name="provider">Optional direct provider: google, apple, or microsoft.</param>
    /// <param name="screen">Optional hosted screen: sign-in or sign-up.</param>
    /// <returns>A redirect to WorkOS AuthKit.</returns>
    [HttpGet("login")]
    [AllowAnonymous]
    public IActionResult Login([FromQuery] string? email = null, [FromQuery] string? provider = null, [FromQuery] string? screen = null)
    {
        var loginHint = ValidateLoginHint(email);
        var selectedProvider = ParseProvider(provider);
        var isSignUp = ParseSignUpScreen(screen);
        var state = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
        var verifier = WebEncoders.Base64UrlEncode(RandomNumberGenerator.GetBytes(32));
        var challenge = WebEncoders.Base64UrlEncode(SHA256.HashData(Encoding.ASCII.GetBytes(verifier)));
        Response.Cookies.Append(AuthStateCookieName, state, CreateTransientCookieOptions());
        Response.Cookies.Append(PkceVerifierCookieName, verifier, CreateTransientCookieOptions());
        return Redirect(authenticationService.GetAuthorizationUri(new AuthorizationRequest
        {
            State = state,
            CodeChallenge = challenge,
            LoginHint = loginHint,
            Provider = selectedProvider,
            IsSignUp = isSignUp,
        }).ToString());
    }

    /// <summary>Completes the WorkOS authorization-code flow and creates an AskRabbi session.</summary>
    /// <param name="code">Single-use WorkOS authorization code.</param>
    /// <param name="state">Anti-forgery state returned by WorkOS.</param>
    /// <param name="cancellationToken">Token that can cancel the operation.</param>
    /// <returns>A redirect to the configured frontend.</returns>
    [HttpGet("callback")]
    [AllowAnonymous]
    public async Task<IActionResult> Callback([FromQuery] string code, [FromQuery] string state, CancellationToken cancellationToken)
    {
        Request.Cookies.TryGetValue(AuthStateCookieName, out var expectedState);
        Request.Cookies.TryGetValue(PkceVerifierCookieName, out var codeVerifier);
        Response.Cookies.Delete(AuthStateCookieName);
        Response.Cookies.Delete(PkceVerifierCookieName);
        if (!StateMatches(expectedState, state))
        {
            return BadRequest(new ProblemDetails
            {
                Status = StatusCodes.Status400BadRequest,
                Title = "Invalid authentication state",
                Detail = "Start sign-in again before returning to this callback.",
            });
        }
        if (string.IsNullOrWhiteSpace(codeVerifier))
        {
            return BadRequest(new ProblemDetails
            {
                Status = StatusCodes.Status400BadRequest,
                Title = "Invalid authentication verifier",
                Detail = "Start sign-in again before returning to this callback.",
            });
        }

        var authenticated = await authenticationService.AuthenticateAsync(code, codeVerifier, cancellationToken).ConfigureAwait(false);
        var account = await userAccounts.UpsertAsync(authenticated.User, timeProvider.GetUtcNow(), cancellationToken).ConfigureAwait(false);
        var properties = new AuthenticationProperties
        {
            AllowRefresh = true,
            IsPersistent = false,
        };
        WorkOsCookieAuthenticationEvents.StoreSessionTokens(properties, authenticated);
        await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, ApplicationPrincipalFactory.Create(account, authenticated.SessionId), properties).ConfigureAwait(false);
        return Redirect(options.FrontendUri);
    }

    /// <summary>Gets the safe account projection for the authenticated browser session.</summary>
    /// <param name="cancellationToken">Token that can cancel the operation.</param>
    /// <returns>The current user session.</returns>
    [HttpGet("session")]
    [Authorize]
    [ProducesResponseType<UserSessionResponse>(StatusCodes.Status200OK)]
    public async Task<ActionResult<UserSessionResponse>> GetSession(CancellationToken cancellationToken)
    {
        var account = await userAccounts.GetByIdAsync(currentUser.UserId, cancellationToken).ConfigureAwait(false);
        if (account is null)
        {
            await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme).ConfigureAwait(false);
            return Unauthorized();
        }

        return Ok(new UserSessionResponse(account.Id, account.DisplayName, account.Email, account.IsEmailVerified, account.ProfileImageUrl));
    }

    /// <summary>Requests a reset email while returning the same response for all valid addresses.</summary>
    /// <param name="request">Password reset request.</param>
    /// <param name="cancellationToken">Token that can cancel the operation.</param>
    /// <returns>An accepted response independent of account existence.</returns>
    [HttpPost("forgot-password")]
    [AllowAnonymous]
    [ProducesResponseType(StatusCodes.Status202Accepted)]
    public async Task<IActionResult> ForgotPassword(ForgotPasswordRequest request, CancellationToken cancellationToken)
    {
        await authenticationService.RequestPasswordResetAsync(request.Email.Trim(), cancellationToken).ConfigureAwait(false);
        return Accepted();
    }

    /// <summary>Confirms a WorkOS password reset and revokes provider sessions.</summary>
    /// <param name="request">Password-reset confirmation.</param>
    /// <param name="cancellationToken">Token that can cancel the operation.</param>
    /// <returns>An empty success response.</returns>
    [HttpPost("reset-password")]
    [AllowAnonymous]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> ResetPassword(ConfirmPasswordResetRequest request, CancellationToken cancellationToken)
    {
        await authenticationService.ConfirmPasswordResetAsync(request.Token.Trim(), request.NewPassword, cancellationToken).ConfigureAwait(false);
        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme).ConfigureAwait(false);
        return NoContent();
    }

    /// <summary>Clears the local session and returns the WorkOS logout destination.</summary>
    /// <returns>The logout redirect URI.</returns>
    [HttpPost("logout")]
    [Authorize]
    [ProducesResponseType<LogoutResponse>(StatusCodes.Status200OK)]
    public async Task<ActionResult<LogoutResponse>> Logout()
    {
        var sessionId = User.FindFirstValue(ApplicationClaimTypes.WorkOsSessionId);
        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme).ConfigureAwait(false);
        var redirectUri = !string.IsNullOrWhiteSpace(sessionId) ? authenticationService.GetLogoutUri(sessionId).ToString() : options.FrontendUri;
        return Ok(new LogoutResponse(redirectUri));
    }

    private static bool StateMatches(string? expectedState, string? actualState)
    {
        if (string.IsNullOrWhiteSpace(expectedState) || string.IsNullOrWhiteSpace(actualState) || expectedState.Length != actualState.Length)
        {
            return false;
        }

        return CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(expectedState), Encoding.UTF8.GetBytes(actualState));
    }

    private static string? ValidateLoginHint(string? email)
    {
        if (string.IsNullOrWhiteSpace(email))
        {
            return null;
        }

        var value = email.Trim();
        if (value.Length > 320 || !new EmailAddressAttribute().IsValid(value))
        {
            throw new ArgumentException("The email login hint is invalid.", nameof(email));
        }
        return value;
    }

    private static ExternalAuthenticationProvider? ParseProvider(string? provider) => provider?.Trim().ToLowerInvariant() switch
    {
        null or "" => null,
        "google" => ExternalAuthenticationProvider.Google,
        "apple" => ExternalAuthenticationProvider.Apple,
        "microsoft" => ExternalAuthenticationProvider.Microsoft,
        _ => throw new ArgumentException("Provider must be google, apple, or microsoft.", nameof(provider)),
    };

    private static bool ParseSignUpScreen(string? screen) => screen?.Trim().ToLowerInvariant() switch
    {
        null or "" or "sign-in" => false,
        "sign-up" => true,
        _ => throw new ArgumentException("Screen must be sign-in or sign-up.", nameof(screen)),
    };

    private CookieOptions CreateTransientCookieOptions() => new()
    {
        HttpOnly = true,
        IsEssential = true,
        MaxAge = TimeSpan.FromMinutes(10),
        SameSite = SameSiteMode.Lax,
        Secure = !environment.IsDevelopment() || Request.IsHttps,
    };
}
