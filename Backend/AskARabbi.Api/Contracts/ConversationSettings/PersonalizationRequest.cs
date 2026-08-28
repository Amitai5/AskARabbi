using System.ComponentModel.DataAnnotations;

namespace AskARabbi.Api.Contracts.ConversationSettings;

/// <summary>Updates the personalization context owned by the authenticated user.</summary>
public sealed record PersonalizationRequest
{
    /// <summary>Gets the user's full name.</summary>
    [Required]
    [StringLength(120)]
    public required string FullName { get; init; }

    /// <summary>Gets the user's local birth date and time without an offset.</summary>
    public DateTime BirthDateTime { get; init; }

    /// <summary>Gets the IANA time-zone identifier for the birthplace.</summary>
    [Required]
    [StringLength(100)]
    public required string BirthTimeZone { get; init; }

    /// <summary>Gets the preferred response language.</summary>
    [Required]
    [StringLength(40)]
    public required string ConversationLanguage { get; init; }

    /// <summary>Gets the preferred quotation language.</summary>
    [Required]
    [StringLength(40)]
    public required string QuotationLanguage { get; init; }

    /// <summary>Gets the self-described religious movement or practice.</summary>
    [Required]
    [StringLength(120)]
    public required string ReligiousMovement { get; init; }

    /// <summary>Gets the self-described Jewish heritage or community.</summary>
    [Required]
    [StringLength(120)]
    public required string JewishHeritage { get; init; }

    /// <summary>Gets optional additional user context.</summary>
    [StringLength(2_000)]
    public string? AdditionalContext { get; init; }
}
