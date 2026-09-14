namespace AskARabbi.Api.Contracts.Conversations;

/// <summary>Identifies attached reading context without retransmitting the full teaching on every turn.</summary>
public sealed record ConversationTeachingResponse(string WeekKey, string Title, string? SelectedText);
