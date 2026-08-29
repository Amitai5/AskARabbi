using AskARabbiLIB.Conversations;

namespace AskARabbi.Api.Conversations;

/// <summary>Reports one stored and optionally answered production conversation turn.</summary>
/// <param name="Status">Stable turn outcome.</param>
/// <param name="Conversation">Canonical context when the conversation exists.</param>
/// <param name="Message">Safe user-facing failure detail when no answer was persisted.</param>
public sealed record GroundedConversationTurnResult(string Status, Conversation? Conversation, string? Message);
