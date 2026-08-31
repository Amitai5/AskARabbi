namespace AskARabbiLIB.Conversations;

/// <summary>Represents the canonical server-owned context for a saved conversation.</summary>
public sealed record Conversation
{
    /// <summary>Gets the canonical title used until the first grounded answer supplies a descriptive title.</summary>
    public const string DefaultTitle = "New Conversation";

    /// <summary>Gets the conversation ID.</summary>
    public required Guid Id { get; init; }

    /// <summary>Gets the owning AskRabbi user ID.</summary>
    public required Guid UserId { get; init; }

    /// <summary>Gets the user-visible title.</summary>
    public required string Title { get; init; }

    /// <summary>Gets the source collections enabled for this conversation.</summary>
    public required IReadOnlyList<string> EnabledSourceKeys { get; init; }

    /// <summary>Gets the ordered canonical message history.</summary>
    public required IReadOnlyList<ConversationMessage> Messages { get; init; }

    /// <summary>Gets when the conversation was created in UTC.</summary>
    public DateTimeOffset CreatedAtUtc { get; init; }

    /// <summary>Gets when the conversation was last changed in UTC.</summary>
    public DateTimeOffset UpdatedAtUtc { get; init; }
}
