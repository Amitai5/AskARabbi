namespace AskARabbiLIB.Retrieval;

/// <summary>Retrieves approved source segments without exposing a provider-specific index.</summary>
public interface ISourceRetriever
{
    /// <summary>Finds ranked source segments matching text, reference, and provenance filters.</summary>
    /// <param name="query">Bounded retrieval criteria.</param>
    /// <param name="cancellationToken">Token used to cancel retrieval.</param>
    /// <returns>Ranked source hits containing trusted provenance.</returns>
    Task<IReadOnlyList<SourceRetrievalHit>> SearchAsync(SourceRetrievalQuery query, CancellationToken cancellationToken = default);

    /// <summary>Loads neighboring segments from the same document in document order.</summary>
    /// <param name="documentId">Stable document identifier.</param>
    /// <param name="documentOrdinal">Zero-based ordinal of the center segment.</param>
    /// <param name="radius">Number of segments to include on each side.</param>
    /// <param name="cancellationToken">Token used to cancel retrieval.</param>
    /// <returns>Available neighboring segments, including the center segment.</returns>
    Task<IReadOnlyList<SourceSegment>> GetContextAsync(string documentId, int documentOrdinal, int radius, CancellationToken cancellationToken = default);
}
