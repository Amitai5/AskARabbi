namespace AskARabbiLIB.Conversations;

/// <summary>Provides the lightweight conversation information needed by navigation.</summary>
public sealed record ConversationSummary(Guid Id, string Title, IReadOnlyList<string> EnabledSourceKeys, DateTimeOffset UpdatedAtUtc);
