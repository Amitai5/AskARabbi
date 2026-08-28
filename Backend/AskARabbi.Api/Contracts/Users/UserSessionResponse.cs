namespace AskARabbi.Api.Contracts.Users;

/// <summary>Provides the safe browser projection of the current account.</summary>
public sealed record UserSessionResponse(Guid Id, string DisplayName, string Email, bool IsEmailVerified, string? ProfileImageUrl);
