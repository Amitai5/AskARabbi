using AskARabbiLIB.Accounts;

namespace AskARabbi.Api.Authentication;

/// <summary>Contains a verified external identity and protected WorkOS session material.</summary>
/// <param name="User">Verified WorkOS user.</param>
/// <param name="SessionId">WorkOS session identifier used for logout.</param>
/// <param name="RefreshToken">Rotating refresh token kept only in the protected application ticket.</param>
/// <param name="AccessTokenExpiresAtUtc">Expiration parsed from the provider-returned access token.</param>
public sealed record AuthenticatedIdentity(ExternalUserIdentity User, string? SessionId, string? RefreshToken = null, DateTimeOffset? AccessTokenExpiresAtUtc = null);
