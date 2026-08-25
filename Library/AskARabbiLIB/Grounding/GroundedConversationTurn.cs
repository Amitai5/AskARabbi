namespace AskARabbiLIB.Grounding;

/// <summary>Represents one validated in-memory conversation turn.</summary>
/// <param name="Question">Original user question.</param>
/// <param name="Answer">Validated assistant answer retained as bounded follow-up context.</param>
public sealed record GroundedConversationTurn(string Question, string Answer);
