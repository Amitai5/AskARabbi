namespace AskARabbi.Api.Contracts.Users;

/// <summary>Provides the provider logout destination after the local session is cleared.</summary>
public sealed record LogoutResponse(string RedirectUri);
