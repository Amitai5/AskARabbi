namespace AskARabbi.Api.Authentication;

/// <summary>Indicates that a request lacks a valid AskRabbi account claim.</summary>
public sealed class UnauthenticatedRequestException : InvalidOperationException
{
    /// <summary>Initializes the exception.</summary>
    public UnauthenticatedRequestException() : base("Authentication is required.")
    {
    }
}
