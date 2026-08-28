namespace AskARabbi.Api.Authentication;

/// <summary>Fails authentication explicitly when WorkOS is not configured.</summary>
public sealed class UnavailableUserAuthenticationService : IUserAuthenticationService
{
    /// <inheritdoc/>
    public Uri GetAuthorizationUri(AuthorizationRequest request) => throw new IdentityProviderUnavailableException();

    /// <inheritdoc/>
    public Task<AuthenticatedIdentity> AuthenticateAsync(string code, string codeVerifier, CancellationToken cancellationToken = default) => Task.FromException<AuthenticatedIdentity>(new IdentityProviderUnavailableException());

    /// <inheritdoc/>
    public Task<AuthenticatedIdentity> RefreshSessionAsync(string refreshToken, CancellationToken cancellationToken = default) => Task.FromException<AuthenticatedIdentity>(new IdentityProviderUnavailableException());

    /// <inheritdoc/>
    public Task RequestPasswordResetAsync(string email, CancellationToken cancellationToken = default) => Task.FromException(new IdentityProviderUnavailableException());

    /// <inheritdoc/>
    public Task ConfirmPasswordResetAsync(string token, string newPassword, CancellationToken cancellationToken = default) => Task.FromException(new IdentityProviderUnavailableException());

    /// <inheritdoc/>
    public Uri GetLogoutUri(string sessionId) => throw new IdentityProviderUnavailableException();
}
