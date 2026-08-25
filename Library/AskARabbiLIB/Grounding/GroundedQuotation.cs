namespace AskARabbiLIB.Grounding;

/// <summary>Represents one exact, validated quotation and its role in an explanation.</summary>
/// <param name="Text">Exact contiguous source quotation.</param>
/// <param name="Role">Explanation of how the quotation supports the statement.</param>
/// <param name="Source">Trusted citation associated with the quotation.</param>
public sealed record GroundedQuotation(string Text, string Role, SourceCitation Source);
