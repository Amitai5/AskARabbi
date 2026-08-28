using System.ComponentModel.DataAnnotations;

namespace AskARabbi.Api.Contracts.Users;

/// <summary>Requests a password-reset email without revealing whether an account exists.</summary>
public sealed record ForgotPasswordRequest
{
    /// <summary>Gets the account email address.</summary>
    [Required]
    [EmailAddress]
    [StringLength(320)]
    public required string Email { get; init; }
}
