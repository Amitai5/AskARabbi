using System.ComponentModel.DataAnnotations;

namespace AskARabbi.Api.Contracts.Conversations;

/// <summary>Renames a saved conversation.</summary>
public sealed record RenameConversationRequest
{
    /// <summary>Gets the new conversation title.</summary>
    [Required]
    [StringLength(80, MinimumLength = 1)]
    public required string Title { get; init; }
}
