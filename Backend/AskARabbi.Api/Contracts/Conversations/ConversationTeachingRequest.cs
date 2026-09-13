using System.ComponentModel.DataAnnotations;
using AskARabbiLIB.Conversations;

namespace AskARabbi.Api.Contracts.Conversations;

/// <summary>Identifies a published teaching without accepting client-supplied article content.</summary>
public sealed record ConversationTeachingRequest
{
    /// <summary>Gets the published teaching's stable week key.</summary>
    [Required, StringLength(40)]
    public required string WeekKey { get; init; }

    /// <summary>Gets an optional highlighted passage, verified against the publication.</summary>
    [StringLength(ConversationTeachingContext.MaximumSelectionLength, MinimumLength = 1)]
    public string? SelectedText { get; init; }
}
