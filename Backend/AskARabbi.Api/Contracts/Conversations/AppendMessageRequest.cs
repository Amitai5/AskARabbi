using System.ComponentModel.DataAnnotations;

namespace AskARabbi.Api.Contracts.Conversations;

/// <summary>Appends one new user message without allowing the browser to replace prior context.</summary>
public sealed record AppendMessageRequest
{
    /// <summary>Gets the client-generated idempotency ID.</summary>
    public required Guid MessageId { get; init; }

    /// <summary>Gets the user message content.</summary>
    [Required]
    [StringLength(8_000, MinimumLength = 1)]
    public required string Content { get; init; }
}
