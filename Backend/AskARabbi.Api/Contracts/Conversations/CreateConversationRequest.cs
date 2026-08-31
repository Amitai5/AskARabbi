using System.ComponentModel.DataAnnotations;

namespace AskARabbi.Api.Contracts.Conversations;

/// <summary>Creates a conversation from its first user message and source selection.</summary>
public sealed record CreateConversationRequest
{
    /// <summary>Gets the client-generated first-message ID.</summary>
    public Guid MessageId { get; init; }

    /// <summary>Gets the first user message.</summary>
    [Required]
    [StringLength(8_000, MinimumLength = 1)]
    public required string Content { get; init; }

    /// <summary>Gets enabled source selectors; all approved sources are used when omitted.</summary>
    [MaxLength(10)]
    public IReadOnlyList<string>? EnabledSourceKeys { get; init; }
}
