using AskARabbi.Api.Authentication;
using AskARabbiLIB.Accounts;

namespace AskARabbi.Api.Tests;

internal sealed class FakeUserAuthenticationService : IUserAuthenticationService
{
    internal int AuthenticateCallCount { get; private set; }
    internal string? LastResetEmail { get; private set; }
    internal string? LastResetToken { get; private set; }
    internal string? LastNewPassword { get; private set; }
    internal string? LastCodeChallenge { get; private set; }
    internal string? LastCodeVerifier { get; private set; }
    internal string? InitialRefreshToken { get; set; }
    internal DateTimeOffset? InitialAccessTokenExpiresAtUtc { get; set; }
    internal AuthenticatedIdentity? RefreshedIdentity { get; set; }
    internal int RefreshCallCount { get; private set; }
    internal string? LastRefreshToken { get; private set; }

    internal AuthorizationRequest? LastAuthorizationRequest { get; private set; }

    public Uri GetAuthorizationUri(AuthorizationRequest request)
    {
        LastAuthorizationRequest = request;
        LastCodeChallenge = request.CodeChallenge;
        return new Uri($"https://auth.example.test/login?state={Uri.EscapeDataString(request.State)}");
    }

    public Task<AuthenticatedIdentity> AuthenticateAsync(string code, string codeVerifier, CancellationToken cancellationToken = default)
    {
        AuthenticateCallCount++;
        LastCodeVerifier = codeVerifier;
        return Task.FromResult(new AuthenticatedIdentity(new ExternalUserIdentity
        {
            ProviderUserId = "user_workos_01",
            Email = "amitai@example.com",
            IsEmailVerified = true,
            FirstName = "Amitai",
            LastName = "Erfanian",
            ProfileImageUrl = "https://images.example.test/amitai.png",
        }, "session_workos_01", InitialRefreshToken, InitialAccessTokenExpiresAtUtc));
    }

    public Task<AuthenticatedIdentity> RefreshSessionAsync(string refreshToken, CancellationToken cancellationToken = default)
    {
        RefreshCallCount++;
        LastRefreshToken = refreshToken;
        return Task.FromResult(RefreshedIdentity ?? throw new InvalidOperationException("No refreshed identity was configured for this test."));
    }

    public Task RequestPasswordResetAsync(string email, CancellationToken cancellationToken = default)
    {
        LastResetEmail = email;
        return Task.CompletedTask;
    }

    public Task ConfirmPasswordResetAsync(string token, string newPassword, CancellationToken cancellationToken = default)
    {
        LastResetToken = token;
        LastNewPassword = newPassword;
        return Task.CompletedTask;
    }

    public Uri GetLogoutUri(string sessionId) => new($"https://auth.example.test/logout?session_id={Uri.EscapeDataString(sessionId)}");
}
