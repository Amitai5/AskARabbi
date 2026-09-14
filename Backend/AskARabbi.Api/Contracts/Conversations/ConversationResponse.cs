namespace AskARabbi.Api.Contracts.Conversations;

/// <summary>Provides canonical server-owned conversation context.</summary>
public sealed record ConversationResponse(Guid Id, string Title, IReadOnlyList<string> EnabledSourceKeys, IReadOnlyList<ConversationMessageResponse> Messages, DateTimeOffset CreatedAtUtc, DateTimeOffset UpdatedAtUtc)
{
    /// <summary>Gets the optional teaching attached to this conversation.</summary>
    public ConversationTeachingResponse? TeachingContext { get; init; }
}
