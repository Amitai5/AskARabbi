namespace AskARabbi.Api.Authentication;

/// <summary>Provides the identity-provider operations used by the user API.</summary>
public interface IUserAuthenticationService
{
    /// <summary>Builds a hosted authorization URL with the supplied anti-forgery state.</summary>
    /// <param name="request">Authorization state, PKCE, and optional hosted-interface hints.</param>
    /// <returns>The hosted authorization URL.</returns>
    Uri GetAuthorizationUri(AuthorizationRequest request);

    /// <summary>Exchanges an authorization code for a verified user identity.</summary>
    /// <param name="code">Single-use authorization code.</param>
    /// <param name="codeVerifier">PKCE verifier associated with the authorization request.</param>
    /// <param name="cancellationToken">Token that can cancel the operation.</param>
    /// <returns>The authenticated identity.</returns>
    Task<AuthenticatedIdentity> AuthenticateAsync(string code, string codeVerifier, CancellationToken cancellationToken = default);

    /// <summary>Rotates an active WorkOS session using its refresh token.</summary>
    /// <param name="refreshToken">Current provider refresh token.</param>
    /// <param name="cancellationToken">Token that can cancel the operation.</param>
    /// <returns>The refreshed authenticated identity and rotated session material.</returns>
    Task<AuthenticatedIdentity> RefreshSessionAsync(string refreshToken, CancellationToken cancellationToken = default);

    /// <summary>Requests a password reset without disclosing account existence.</summary>
    /// <param name="email">Email address supplied by the user.</param>
    /// <param name="cancellationToken">Token that can cancel the operation.</param>
    /// <returns>A task representing the operation.</returns>
    Task RequestPasswordResetAsync(string email, CancellationToken cancellationToken = default);

    /// <summary>Confirms a password reset using a WorkOS one-time token.</summary>
    /// <param name="token">One-time password reset token.</param>
    /// <param name="newPassword">New password.</param>
    /// <param name="cancellationToken">Token that can cancel the operation.</param>
    /// <returns>A task representing the operation.</returns>
    Task ConfirmPasswordResetAsync(string token, string newPassword, CancellationToken cancellationToken = default);

    /// <summary>Builds the provider logout URL for an active session.</summary>
    /// <param name="sessionId">WorkOS session ID.</param>
    /// <returns>The provider logout URL.</returns>
    Uri GetLogoutUri(string sessionId);
}
