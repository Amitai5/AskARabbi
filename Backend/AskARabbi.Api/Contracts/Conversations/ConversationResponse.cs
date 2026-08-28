namespace AskARabbi.Api.Contracts.Conversations;

/// <summary>Provides canonical server-owned conversation context.</summary>
public sealed record ConversationResponse(Guid Id, string Title, IReadOnlyList<string> EnabledSourceKeys, IReadOnlyList<ConversationMessageResponse> Messages, DateTimeOffset CreatedAtUtc, DateTimeOffset UpdatedAtUtc);
