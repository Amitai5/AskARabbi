using System.ComponentModel.DataAnnotations;

namespace AskARabbi.Api.Contracts.Users;

/// <summary>Confirms a password reset with a one-time token.</summary>
public sealed record ConfirmPasswordResetRequest
{
    /// <summary>Gets the one-time WorkOS password-reset token.</summary>
    [Required]
    [StringLength(2_048)]
    public required string Token { get; init; }

    /// <summary>Gets the replacement password.</summary>
    [Required]
    [StringLength(128, MinimumLength = 12)]
    public required string NewPassword { get; init; }
}
