namespace AskARabbiLIB.Accounts;

/// <summary>Represents the verified identity fields received from the configured identity provider.</summary>
public sealed record ExternalUserIdentity
{
    /// <summary>Gets the immutable identity-provider user ID.</summary>
    public required string ProviderUserId { get; init; }

    /// <summary>Gets the user's primary email address.</summary>
    public required string Email { get; init; }

    /// <summary>Gets whether the identity provider has verified the email address.</summary>
    public bool IsEmailVerified { get; init; }

    /// <summary>Gets the user's optional first name.</summary>
    public string? FirstName { get; init; }

    /// <summary>Gets the user's optional last name.</summary>
    public string? LastName { get; init; }

    /// <summary>Gets the user's optional profile-image URL.</summary>
    public string? ProfileImageUrl { get; init; }
}
