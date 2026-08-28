namespace AskARabbi.Api.Authentication;

/// <summary>Indicates that identity-provider configuration or service availability prevents an operation.</summary>
public sealed class IdentityProviderUnavailableException : InvalidOperationException
{
    /// <summary>Initializes the exception with a safe message.</summary>
    public IdentityProviderUnavailableException() : base("Authentication is unavailable. Configure WorkOS:ApiKey and WorkOS:ClientId, or try again later.")
    {
    }

    /// <summary>Initializes the exception with a safe message and provider exception.</summary>
    /// <param name="innerException">Underlying provider failure.</param>
    public IdentityProviderUnavailableException(Exception innerException) : base("Authentication is temporarily unavailable.", innerException)
    {
    }
}
