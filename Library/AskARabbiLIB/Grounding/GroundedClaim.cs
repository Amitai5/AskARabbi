namespace AskARabbiLIB.Grounding;

/// <summary>Represents one answer claim and its validated source citations.</summary>
public sealed record GroundedClaim(string Text, IReadOnlyList<SourceCitation> Citations, string? DirectQuotation, SourceCitation? QuotationSource)
{
    public string? Attribution { get; init; }

    public IReadOnlyList<GroundedQuotation> Quotations { get; init; } = [];
}
