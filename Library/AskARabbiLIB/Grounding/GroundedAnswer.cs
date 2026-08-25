namespace AskARabbiLIB.Grounding;

/// <summary>Represents a validated source-backed educational answer.</summary>
public sealed record GroundedAnswer(IReadOnlyList<GroundedClaim> Claims, IReadOnlyList<GroundedDisagreement> Disagreements, IReadOnlyList<string> Limitations, string? ClarifyingQuestion, bool HumanGuidanceRecommended, IReadOnlyList<SourceCitation> Citations)
{
    /// <summary>Gets the application-controlled notice appended after the grounded answer.</summary>
    public required string InterpretiveNotice { get; init; }
}
