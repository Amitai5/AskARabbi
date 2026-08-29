using AskARabbiLIB.Grounding;

namespace AskARabbi.Api.Conversations;

internal sealed class UnavailableGroundedAnswerService : IGroundedAnswerService
{
    /// <inheritdoc/>
    public Task<GroundedAnswerResult> AnswerAsync(GroundedQuestion question, IReadOnlyList<GroundedConversationTurn> recentConversation, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(question);
        ArgumentNullException.ThrowIfNull(recentConversation);
        cancellationToken.ThrowIfCancellationRequested();

        return Task.FromResult(new GroundedAnswerResult
        {
            Status = GroundedAnswerStatus.AIUnavailable,
            Trace = new GroundedAnswerTrace(TimeSpan.Zero, TimeSpan.Zero, 0, 0, 0, null, GroundedValidationStatus.NotRun, false, null, string.Empty),
            ErrorMessage = "Grounded chat is not configured on this API instance. No model was called.",
        });
    }
}
