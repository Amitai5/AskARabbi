namespace AskARabbiLIB.Grounding;

/// <summary>Controls local retrieval and prompt evidence budgets.</summary>
public sealed record GroundedAnswerOptions
{
    /// <summary>Gets the maximum candidate segments requested from retrieval.</summary>
    public int MaximumCandidates { get; init; } = 50;

    /// <summary>Gets the maximum number of source segments included in an evidence packet.</summary>
    public int MaximumEvidenceSegments { get; init; } = 24;

    /// <summary>Gets the maximum total number of characters included in an evidence packet.</summary>
    public int MaximumEvidenceCharacters { get; init; } = 48_000;

    /// <summary>Gets the character budget for one full or explicitly excerpted segment.</summary>
    public int MaximumCharactersPerSegment { get; init; } = 6_000;

    /// <summary>Gets the maximum evidence segments drawn from one document edition.</summary>
    public int MaximumSegmentsPerDocument { get; init; } = 9;

    /// <summary>Gets the maximum adjacent segments considered on each side of a hit.</summary>
    public int ContextRadius { get; init; } = 6;

    /// <summary>Gets the maximum recent conversation turns used for follow-up retrieval context.</summary>
    public int RecentConversationTurns { get; init; } = 3;

    /// <summary>Validates retrieval and evidence budget bounds.</summary>
    public void Validate()
    {
        if (MaximumCandidates is < 1 or > 200)
        {
            throw new ArgumentOutOfRangeException(nameof(MaximumCandidates), "Maximum candidates must be between 1 and 200.");
        }

        if (MaximumEvidenceSegments is < 1 or > 50)
        {
            throw new ArgumentOutOfRangeException(nameof(MaximumEvidenceSegments), "Maximum evidence segments must be between 1 and 50.");
        }

        if (MaximumEvidenceCharacters is < 1_000 or > 200_000)
        {
            throw new ArgumentOutOfRangeException(nameof(MaximumEvidenceCharacters), "Maximum evidence characters must be between 1,000 and 200,000.");
        }

        if (MaximumCharactersPerSegment < 200 || MaximumCharactersPerSegment > MaximumEvidenceCharacters)
        {
            throw new ArgumentOutOfRangeException(nameof(MaximumCharactersPerSegment), "Maximum characters per segment must be between 200 and the packet budget.");
        }

        if (MaximumSegmentsPerDocument < 1 || MaximumSegmentsPerDocument > MaximumEvidenceSegments)
        {
            throw new ArgumentOutOfRangeException(nameof(MaximumSegmentsPerDocument), "Maximum segments per document must fit within the evidence limit.");
        }

        if (ContextRadius is < 0 or > 10)
        {
            throw new ArgumentOutOfRangeException(nameof(ContextRadius), "Context radius must be between 0 and 10 segments on each side.");
        }

        if (RecentConversationTurns is < 0 or > 10)
        {
            throw new ArgumentOutOfRangeException(nameof(RecentConversationTurns), "Recent conversation turns must be between 0 and 10.");
        }
    }
}
