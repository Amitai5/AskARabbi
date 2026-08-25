namespace AskARabbiLIB.Grounding;

/// <summary>Represents a sourced disagreement that should remain visible to the user.</summary>
public sealed record GroundedDisagreement(string Text, IReadOnlyList<SourceCitation> Citations)
{
    public string? Attribution { get; init; }

    public IReadOnlyList<GroundedQuotation> Quotations { get; init; } = [];
}
