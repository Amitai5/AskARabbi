namespace AskARabbiLIB.Retrieval;

/// <summary>Reads complete, checksum-verified canonical passages from the approved corpus.</summary>
public interface ICanonicalSourceReader
{
    /// <summary>Reads an exact reference or inclusive range while respecting source and language filters.</summary>
    /// <param name="reference">Canonical chapter, segment, or range.</param>
    /// <param name="filters">The user's enabled sources and language preferences.</param>
    /// <param name="cancellationToken">Request cancellation.</param>
    /// <returns>Complete segments in canonical order from the first available preferred language.</returns>
    Task<IReadOnlyList<SourceSegment>> ReadAsync(string reference, SourceRetrievalQuery filters, CancellationToken cancellationToken = default);
}
