namespace AskARabbi.Api.Contracts.Conversations;

/// <summary>Reports a grounded turn outcome and the resulting canonical context.</summary>
public sealed record ConversationTurnResponse(string Status, ConversationResponse Conversation, string? Message);
