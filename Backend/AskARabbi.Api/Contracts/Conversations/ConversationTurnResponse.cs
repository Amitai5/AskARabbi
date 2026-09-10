namespace AskARabbi.Api.Contracts.Conversations;

/// <summary>Reports a grounded turn outcome and the resulting canonical context.</summary>
public sealed record ConversationTurnResponse(string Status, ConversationResponse Conversation, string? Message)
{
    /// <summary>Gets updated token usage, including unsuccessful generation attempts.</summary>
    public ConversationSettings.UsageResponse? Usage { get; init; }
}
