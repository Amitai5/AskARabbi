using System.ComponentModel.DataAnnotations;

namespace AskARabbi.Api.Contracts.Conversations;

/// <summary>Replaces the approved source selection for a conversation.</summary>
public sealed record UpdateConversationSourcesRequest
{
    /// <summary>Gets the enabled source selectors.</summary>
    [MinLength(1)]
    [MaxLength(10)]
    public required IReadOnlyList<string> EnabledSourceKeys { get; init; }
}
