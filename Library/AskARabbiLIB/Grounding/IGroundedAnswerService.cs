namespace AskARabbiLIB.Grounding;

/// <summary>Retrieves, generates, validates, and materializes source-backed answers.</summary>
public interface IGroundedAnswerService
{
    /// <summary>Answers one question using only retrieved approved-corpus evidence.</summary>
    /// <param name="question">Question and source preferences.</param>
    /// <param name="recentConversation">Limited validated in-memory conversation context.</param>
    /// <param name="cancellationToken">Token propagated through retrieval and generation.</param>
    /// <returns>A validated answer or explicit fail-closed result.</returns>
    Task<GroundedAnswerResult> AnswerAsync(GroundedQuestion question, IReadOnlyList<GroundedConversationTurn> recentConversation, CancellationToken cancellationToken = default);
}
