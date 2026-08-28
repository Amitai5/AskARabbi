using System.ComponentModel.DataAnnotations;

namespace AskARabbi.Api.Contracts.Conversations;

/// <summary>Creates an empty conversation with an optional title and source selection.</summary>
public sealed record CreateConversationRequest
{
    /// <summary>Gets the optional initial title.</summary>
    [StringLength(80)]
    public string? Title { get; init; }

    /// <summary>Gets enabled source selectors; all approved sources are used when omitted.</summary>
    [MaxLength(10)]
    public IReadOnlyList<string>? EnabledSourceKeys { get; init; }
}
