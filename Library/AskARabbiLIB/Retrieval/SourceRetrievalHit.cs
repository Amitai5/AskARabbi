namespace AskARabbiLIB.Retrieval;

/// <summary>Represents a ranked segment-retrieval result.</summary>
/// <param name="Segment">Retrieved segment with trusted provenance.</param>
/// <param name="Score">Retriever-specific relevance score.</param>
/// <param name="IsExactReference">Whether the hit matched an exact canonical reference.</param>
public sealed record SourceRetrievalHit(SourceSegment Segment, double Score, bool IsExactReference);
