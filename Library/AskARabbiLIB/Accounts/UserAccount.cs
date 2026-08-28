namespace AskARabbiLIB.Accounts;

/// <summary>Represents an AskRabbi account linked to an external authentication identity.</summary>
public sealed record UserAccount
{
    /// <summary>Gets the immutable AskRabbi user ID.</summary>
    public required Guid Id { get; init; }

    /// <summary>Gets the immutable identity-provider user ID.</summary>
    public required string ProviderUserId { get; init; }

    /// <summary>Gets the user's primary email address.</summary>
    public required string Email { get; init; }

    /// <summary>Gets whether the primary email has been verified by the identity provider.</summary>
    public bool IsEmailVerified { get; init; }

    /// <summary>Gets the user's optional first name.</summary>
    public string? FirstName { get; init; }

    /// <summary>Gets the user's optional last name.</summary>
    public string? LastName { get; init; }

    /// <summary>Gets the user's optional profile-image URL.</summary>
    public string? ProfileImageUrl { get; init; }

    /// <summary>Gets when the account was created in UTC.</summary>
    public DateTimeOffset CreatedAtUtc { get; init; }

    /// <summary>Gets when the account identity projection was last updated in UTC.</summary>
    public DateTimeOffset UpdatedAtUtc { get; init; }

    /// <summary>Gets a human-readable display name without requiring one from the identity provider.</summary>
    public string DisplayName
    {
        get
        {
            var name = string.Join(' ', new[] { FirstName, LastName }.Where(value => !string.IsNullOrWhiteSpace(value)));
            return string.IsNullOrWhiteSpace(name) ? Email : name;
        }
    }
}
