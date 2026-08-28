using AskARabbi.Api.Authentication;
using AskARabbiLIB.Accounts;
using Microsoft.AspNetCore.WebUtilities;

namespace AskARabbi.Api.Development;

/// <summary>Provides a deterministic local identity flow only when explicitly enabled in Development.</summary>
public sealed class LocalDevelopmentAuthenticationService : IUserAuthenticationService
{
    private const string AuthorizationCode = "local-development";
    private readonly WorkOsAuthenticationOptions options;

    /// <summary>Initializes the local development identity service.</summary>
    /// <param name="options">Redirect configuration shared with the production identity boundary.</param>
    public LocalDevelopmentAuthenticationService(WorkOsAuthenticationOptions options)
    {
        this.options = options ?? throw new ArgumentNullException(nameof(options));
    }

    /// <inheritdoc/>
    public Uri GetAuthorizationUri(AuthorizationRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (string.IsNullOrWhiteSpace(request.State) || string.IsNullOrWhiteSpace(request.CodeChallenge))
        {
            throw new ArgumentException("State and PKCE challenge are required for local development login.");
        }

        var callback = QueryHelpers.AddQueryString(options.RedirectUri, new Dictionary<string, string?>
        {
            ["code"] = AuthorizationCode,
            ["state"] = request.State,
        });
        return new Uri(callback, UriKind.Absolute);
    }

    /// <inheritdoc/>
    public Task<AuthenticatedIdentity> AuthenticateAsync(string code, string codeVerifier, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!string.Equals(code, AuthorizationCode, StringComparison.Ordinal) || string.IsNullOrWhiteSpace(codeVerifier))
        {
            throw new IdentityRequestRejectedException("The local development authorization response was invalid.");
        }

        var identity = new ExternalUserIdentity
        {
            ProviderUserId = "local-development-user",
            Email = "amitai.local@example.com",
            IsEmailVerified = true,
            FirstName = "Amitai",
            LastName = "Erfanian",
        };
        return Task.FromResult(new AuthenticatedIdentity(identity, null));
    }

    /// <inheritdoc/>
    public Task<AuthenticatedIdentity> RefreshSessionAsync(string refreshToken, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        throw new IdentityRequestRejectedException("Local development sessions do not use WorkOS refresh tokens.");
    }

    /// <inheritdoc/>
    public Task RequestPasswordResetAsync(string email, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public Task ConfirmPasswordResetAsync(string token, string newPassword, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public Uri GetLogoutUri(string sessionId) => new(options.FrontendUri, UriKind.Absolute);
}
