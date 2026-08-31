using AskARabbiLIB.Conversations;
using AskARabbiLIB.Grounding;

namespace AskARabbi.Api.Conversations;

/// <summary>Reports one stored and optionally answered production conversation turn.</summary>
/// <param name="Status">Stable turn outcome.</param>
/// <param name="Conversation">Canonical context when the conversation exists.</param>
/// <param name="Message">Safe user-facing failure detail when no answer was persisted.</param>
/// <param name="Trace">Non-persistent retrieval and model diagnostics when grounding ran.</param>
/// <param name="ProcessingLatency">End-to-end warm request processing time measured by the turn service.</param>
public sealed record GroundedConversationTurnResult(string Status, Conversation? Conversation, string? Message, GroundedAnswerTrace? Trace = null, TimeSpan? ProcessingLatency = null);
