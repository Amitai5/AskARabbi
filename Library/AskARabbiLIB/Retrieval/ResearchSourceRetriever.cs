using AskARabbiLIB.Grounding;

namespace AskARabbiLIB.Retrieval;

/// <summary>Combines semantic and original-text keyword search so broad topic matches cannot hide specific evidence.</summary>
public sealed class ResearchSourceRetriever : ISourceRetriever
{
    private readonly ISourceRetriever semantic;
    private readonly ISourceRetriever keywords;

    /// <summary>Combines managed semantic search with a deployment-local original-text index.</summary>
    /// <param name="semantic">Existing managed search with its normal metering.</param>
    /// <param name="keywords">Verified read-only keyword index of approved original sources.</param>
    public ResearchSourceRetriever(ISourceRetriever semantic, ISourceRetriever keywords)
    {
        this.semantic = semantic ?? throw new ArgumentNullException(nameof(semantic));
        this.keywords = keywords ?? throw new ArgumentNullException(nameof(keywords));
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<SourceRetrievalHit>> SearchAsync(SourceRetrievalQuery query, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);
        var hits = await semantic.SearchAsync(query, cancellationToken).ConfigureAwait(false);
        if (hits.Any(hit => hit.IsExactReference) || string.IsNullOrWhiteSpace(query.QueryText))
        {
            return hits;
        }
        // The same trusted scope is passed unchanged. Synonyms guide lookup only; no
        // model-generated text or guessed source reference can become evidence here.
        var keywordHits = await keywords.SearchAsync(query, cancellationToken).ConfigureAwait(false);
        if (!SourceEvidenceAdequacyEvaluator.Evaluate(query.QueryText, keywordHits).IsAdequate)
        {
            return hits;
        }

        // Topical adequacy is not completeness: a passage naming a custom can omit
        // its explanation. Fuse both rankings rather than stopping at that passage.
        // Reciprocal ranks avoid comparing unrelated semantic and BM25 score scales.
        var combined = new Dictionary<string, SourceRetrievalHit>(StringComparer.Ordinal);
        AddRankedHits(combined, hits);
        AddRankedHits(combined, keywordHits);
        var ranked = combined.Values.OrderByDescending(hit => hit.Score).ThenBy(hit => hit.Segment.SegmentId, StringComparer.Ordinal).ToArray();
        return SourceEvidenceAdequacyEvaluator.Evaluate(query.QueryText, ranked).OrderedHits.Take(query.CandidateLimit).ToArray();
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<SourceSegment>> GetContextAsync(string documentId, int documentOrdinal, int radius, CancellationToken cancellationToken = default)
    {
        var local = await keywords.GetContextAsync(documentId, documentOrdinal, radius, cancellationToken).ConfigureAwait(false);
        return local.Count > 0 ? local : await semantic.GetContextAsync(documentId, documentOrdinal, radius, cancellationToken).ConfigureAwait(false);
    }

    private static void AddRankedHits(IDictionary<string, SourceRetrievalHit> combined, IReadOnlyList<SourceRetrievalHit> hits)
    {
        const int RankSmoothing = 60;
        for (var index = 0; index < hits.Count; index++)
        {
            var hit = hits[index];
            var score = 1d / (RankSmoothing + index + 1);
            if (combined.TryGetValue(hit.Segment.SegmentId, out var existing))
            {
                combined[hit.Segment.SegmentId] = existing with { Score = existing.Score + score };
            }
            else
            {
                combined.Add(hit.Segment.SegmentId, hit with { Score = score });
            }
        }
    }
}
