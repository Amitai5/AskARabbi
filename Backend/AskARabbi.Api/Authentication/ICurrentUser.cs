namespace AskARabbi.Api.Authentication;

/// <summary>Provides the authenticated AskRabbi user ID for the current request.</summary>
public interface ICurrentUser
{
    /// <summary>Gets the authenticated AskRabbi user ID.</summary>
    Guid UserId { get; }
}
