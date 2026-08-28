using System.Text;
using System.Text.Json;
using AskARabbiLIB.Accounts;
using WorkOS;

namespace AskARabbi.Api.Authentication;

/// <summary>Uses the official WorkOS SDK for AuthKit login and password recovery.</summary>
public sealed class WorkOsUserAuthenticationService : IUserAuthenticationService
{
    private readonly WorkOSClient client;
    private readonly WorkOsAuthenticationOptions options;

    /// <summary>Initializes a WorkOS authentication adapter.</summary>
    /// <param name="client">Configured WorkOS client.</param>
    /// <param name="options">Validated authentication options.</param>
    public WorkOsUserAuthenticationService(WorkOSClient client, WorkOsAuthenticationOptions options)
    {
        this.client = client ?? throw new ArgumentNullException(nameof(client));
        this.options = options ?? throw new ArgumentNullException(nameof(options));
        this.options.Validate();
    }

    /// <inheritdoc/>
    public Uri GetAuthorizationUri(AuthorizationRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.State);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.CodeChallenge);
        var value = client.UserManagement.GetAuthorizationUrl(new UserManagementGetAuthorizationUrlOptions
        {
            CodeChallenge = request.CodeChallenge,
            CodeChallengeMethod = "S256",
            LoginHint = request.LoginHint,
            RedirectUri = options.RedirectUri,
            Provider = ToWorkOsProvider(request.Provider),
            ScreenHint = request.IsSignUp ? RadarStandaloneAssessRequestAction.SignUp : null,
            State = request.State,
        });
        return new Uri(value, UriKind.Absolute);
    }

    /// <inheritdoc/>
    public async Task<AuthenticatedIdentity> AuthenticateAsync(string code, string codeVerifier, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(code);
        ArgumentException.ThrowIfNullOrWhiteSpace(codeVerifier);
        try
        {
            var response = await client.UserManagement.AuthenticateWithCodeAsync(new AuthenticateWithCodeOptions
            {
                Code = code,
                CodeVerifier = codeVerifier,
            }, cancellationToken: cancellationToken).ConfigureAwait(false);
            return ToIdentity(response);
        }
        catch (ApiException exception) when (IsRejectedRequest(exception))
        {
            throw new IdentityRequestRejectedException("The sign-in request was rejected or expired. Start sign-in again.", exception);
        }
        catch (ApiException exception)
        {
            throw new IdentityProviderUnavailableException(exception);
        }
        catch (HttpRequestException exception)
        {
            throw new IdentityProviderUnavailableException(exception);
        }
    }

    /// <inheritdoc/>
    public async Task<AuthenticatedIdentity> RefreshSessionAsync(string refreshToken, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(refreshToken);
        try
        {
            var response = await client.UserManagement.AuthenticateWithRefreshTokenAsync(new AuthenticateWithRefreshTokenOptions
            {
                RefreshToken = refreshToken,
            }, cancellationToken: cancellationToken).ConfigureAwait(false);
            return ToIdentity(response);
        }
        catch (ApiException exception) when (IsRejectedRequest(exception))
        {
            throw new IdentityRequestRejectedException("The WorkOS session is no longer active. Sign in again.", exception);
        }
        catch (ApiException exception)
        {
            throw new IdentityProviderUnavailableException(exception);
        }
        catch (HttpRequestException exception)
        {
            throw new IdentityProviderUnavailableException(exception);
        }
    }

    /// <inheritdoc/>
    public async Task RequestPasswordResetAsync(string email, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(email);
        try
        {
            await client.UserManagement.ResetPasswordAsync(new UserManagementResetPasswordOptions
            {
                Email = email,
            }, cancellationToken: cancellationToken).ConfigureAwait(false);
        }
        catch (ApiException exception) when ((int)exception.StatusCode is 400 or 404 or 422)
        {
            // A password-reset request must not reveal whether the supplied address has an account.
        }
        catch (ApiException exception)
        {
            throw new IdentityProviderUnavailableException(exception);
        }
        catch (HttpRequestException exception)
        {
            throw new IdentityProviderUnavailableException(exception);
        }
    }

    /// <inheritdoc/>
    public async Task ConfirmPasswordResetAsync(string token, string newPassword, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(token);
        ArgumentException.ThrowIfNullOrWhiteSpace(newPassword);
        try
        {
            await client.UserManagement.ConfirmPasswordResetAsync(new UserManagementConfirmPasswordResetOptions
            {
                Token = token,
                NewPassword = newPassword,
            }, cancellationToken: cancellationToken).ConfigureAwait(false);
        }
        catch (ApiException exception) when (IsRejectedRequest(exception))
        {
            throw new IdentityRequestRejectedException("The password reset link is invalid or expired.", exception);
        }
        catch (ApiException exception)
        {
            throw new IdentityProviderUnavailableException(exception);
        }
        catch (HttpRequestException exception)
        {
            throw new IdentityProviderUnavailableException(exception);
        }
    }

    /// <inheritdoc/>
    public Uri GetLogoutUri(string sessionId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sessionId);
        var value = client.UserManagement.GetLogoutUrl(new UserManagementGetLogoutUrlOptions
        {
            SessionId = sessionId,
            ReturnTo = options.FrontendUri,
        });
        return new Uri(value, UriKind.Absolute);
    }

    private static AuthenticatedIdentity ToIdentity(AuthenticateResponse response)
    {
        var user = response.User;
        var (sessionId, expiresAtUtc) = TryReadSessionClaims(response.AccessToken);
        return new AuthenticatedIdentity(new ExternalUserIdentity
        {
            ProviderUserId = user.Id,
            Email = user.Email,
            IsEmailVerified = user.EmailVerified,
            FirstName = user.FirstName,
            LastName = user.LastName,
            ProfileImageUrl = user.ProfilePictureUrl,
        }, sessionId, response.RefreshToken, expiresAtUtc);
    }

    private static UserManagementAuthenticationProvider ToWorkOsProvider(ExternalAuthenticationProvider? provider) => provider switch
    {
        ExternalAuthenticationProvider.Google => UserManagementAuthenticationProvider.GoogleOAuth,
        ExternalAuthenticationProvider.Apple => UserManagementAuthenticationProvider.AppleOAuth,
        ExternalAuthenticationProvider.Microsoft => UserManagementAuthenticationProvider.MicrosoftOAuth,
        _ => UserManagementAuthenticationProvider.Authkit,
    };

    private static bool IsRejectedRequest(ApiException exception) => (int)exception.StatusCode is 400 or 404 or 409 or 422;

    private static (string? SessionId, DateTimeOffset? ExpiresAtUtc) TryReadSessionClaims(string? accessToken)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(accessToken))
            {
                return (null, null);
            }

            var parts = accessToken.Split('.');
            if (parts.Length != 3)
            {
                return (null, null);
            }

            var payload = parts[1].Replace('-', '+').Replace('_', '/');
            payload = payload.PadRight(payload.Length + ((4 - payload.Length % 4) % 4), '=');
            using var document = JsonDocument.Parse(Encoding.UTF8.GetString(Convert.FromBase64String(payload)));
            var root = document.RootElement;
            var sessionId = root.TryGetProperty("sid", out var sessionIdProperty) ? sessionIdProperty.GetString() : null;
            DateTimeOffset? expiresAtUtc = root.TryGetProperty("exp", out var expirationProperty) && expirationProperty.TryGetInt64(out var expiration)
                ? DateTimeOffset.FromUnixTimeSeconds(expiration)
                : null;
            return (sessionId, expiresAtUtc);
        }
        catch (FormatException)
        {
            return (null, null);
        }
        catch (JsonException)
        {
            return (null, null);
        }
    }
}
