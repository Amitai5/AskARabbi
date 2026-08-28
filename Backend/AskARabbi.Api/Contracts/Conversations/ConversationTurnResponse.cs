namespace AskARabbi.Api.Contracts.Conversations;

/// <summary>Reports a stored user turn and the resulting canonical context.</summary>
public sealed record ConversationTurnResponse(string Status, ConversationResponse Conversation);
