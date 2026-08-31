namespace AskARabbi.Api.Contracts.Conversations;

/// <summary>Reports a grounded turn using bounded metadata and only the messages involved in that turn.</summary>
/// <param name="Status">Stable turn outcome.</param>
/// <param name="Conversation">Updated conversation navigation metadata.</param>
/// <param name="Messages">User and assistant messages involved in this turn.</param>
/// <param name="CreatedAtUtc">UTC creation instant needed when the client has no prior conversation context.</param>
/// <param name="Message">Safe user-facing failure detail when no answer was generated.</param>
public sealed record ConversationTurnDeltaResponse(string Status, ConversationSummaryResponse Conversation, IReadOnlyList<ConversationMessageResponse> Messages, DateTimeOffset CreatedAtUtc, string? Message);
