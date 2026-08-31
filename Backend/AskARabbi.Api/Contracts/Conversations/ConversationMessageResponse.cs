using AskARabbiLIB.Conversations;

namespace AskARabbi.Api.Contracts.Conversations;

/// <summary>Provides one persisted conversation message.</summary>
public sealed record ConversationMessageResponse(Guid Id, ConversationMessageRole Role, string Content, DateTimeOffset CreatedAtUtc)
{
    /// <summary>Gets trusted sources for an assistant response.</summary>
    public IReadOnlyList<ConversationSourceResponse> Sources { get; init; } = [];
}
