using System.ComponentModel.DataAnnotations;

namespace AskARabbi.Api.Contracts.ConversationSettings;

/// <summary>Complete reading preferences to save for the authenticated account.</summary>
public sealed record ReadingPreferencesRequest
{
    [Required, RegularExpression("^(small|default|large|extra-large)$")]
    public required string TextSize { get; init; }

    [Required, RegularExpression("^(compact|default|relaxed)$")]
    public required string LineSpacing { get; init; }

    [Required, RegularExpression("^(light|dark|system)$")]
    public required string Theme { get; init; }

    public bool FocusLongContent { get; init; }
}
