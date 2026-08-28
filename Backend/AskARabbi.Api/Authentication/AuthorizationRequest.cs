namespace AskARabbi.Api.Authentication;

/// <summary>Describes a WorkOS authorization request created by the backend.</summary>
public sealed record AuthorizationRequest
{
    /// <summary>Gets the anti-forgery state value.</summary>
    public required string State { get; init; }

    /// <summary>Gets the S256 PKCE challenge.</summary>
    public required string CodeChallenge { get; init; }

    /// <summary>Gets the optional email hint shown by hosted authentication.</summary>
    public string? LoginHint { get; init; }

    /// <summary>Gets the optional direct social provider.</summary>
    public ExternalAuthenticationProvider? Provider { get; init; }

    /// <summary>Gets whether hosted authentication should begin on account creation.</summary>
    public bool IsSignUp { get; init; }
}
