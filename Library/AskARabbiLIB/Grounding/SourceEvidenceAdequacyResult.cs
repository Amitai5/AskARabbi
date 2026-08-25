using AskARabbiLIB.Retrieval;

namespace AskARabbiLIB.Grounding;

internal sealed record SourceEvidenceAdequacyResult(bool IsAdequate, string? ErrorMessage, IReadOnlyList<SourceRetrievalHit> OrderedHits)
{
    internal static SourceEvidenceAdequacyResult Adequate(IReadOnlyList<SourceRetrievalHit> orderedHits) => new(true, null, orderedHits);

    internal static SourceEvidenceAdequacyResult Insufficient(string errorMessage, IReadOnlyList<SourceRetrievalHit> orderedHits) => new(false, errorMessage, orderedHits);
}
