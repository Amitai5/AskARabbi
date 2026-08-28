namespace AskARabbi.Api.Authentication;

/// <summary>Indicates that the identity provider rejected user-supplied authentication data.</summary>
public sealed class IdentityRequestRejectedException : InvalidOperationException
{
    /// <summary>Initializes the exception with a safe user-facing message.</summary>
    /// <param name="message">Safe rejection message.</param>
    /// <param name="innerException">Optional provider exception.</param>
    public IdentityRequestRejectedException(string message, Exception? innerException = null) : base(message, innerException)
    {
    }
}
