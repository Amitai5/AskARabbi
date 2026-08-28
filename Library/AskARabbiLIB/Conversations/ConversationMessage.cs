namespace AskARabbiLIB.Conversations;

/// <summary>Represents one immutable message in a saved conversation.</summary>
public sealed record ConversationMessage
{
    /// <summary>Gets the client- or server-generated idempotency ID.</summary>
    public required Guid Id { get; init; }

    /// <summary>Gets the message author role.</summary>
    public required ConversationMessageRole Role { get; init; }

    /// <summary>Gets the plain message content.</summary>
    public required string Content { get; init; }

    /// <summary>Gets when the message was accepted in UTC.</summary>
    public DateTimeOffset CreatedAtUtc { get; init; }
}
