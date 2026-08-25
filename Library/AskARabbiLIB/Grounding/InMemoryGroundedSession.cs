namespace AskARabbiLIB.Grounding;

/// <summary>Maintains process-only validated conversation context with no persistence boundary.</summary>
public sealed class InMemoryGroundedSession
{
    private readonly List<GroundedConversationTurn> turns = [];

    /// <summary>Returns a caller-owned snapshot of all current process-memory turns.</summary>
    /// <returns>The current conversation turns.</returns>
    public IReadOnlyList<GroundedConversationTurn> GetTurns() => turns.ToArray();

    /// <summary>Adds one validated successful answer to process memory.</summary>
    /// <param name="question">Original user question.</param>
    /// <param name="answer">Validated grounded answer.</param>
    public void Add(string question, GroundedAnswer answer)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(question);
        ArgumentNullException.ThrowIfNull(answer);
        var answerText = string.Join("\n\n", answer.Claims.Select(claim => claim.Text));
        turns.Add(new GroundedConversationTurn(question.Trim(), answerText));
    }

    /// <summary>Removes every in-memory conversation turn.</summary>
    public void Clear() => turns.Clear();
}
