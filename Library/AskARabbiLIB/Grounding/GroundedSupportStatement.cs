namespace AskARabbiLIB.Grounding;

internal sealed record GroundedSupportStatement(string StatementId, string StatementType, string Text, string? Attribution, IReadOnlyList<string> EvidenceIds, IReadOnlyList<GroundedQuotationDraft> Quotations);
