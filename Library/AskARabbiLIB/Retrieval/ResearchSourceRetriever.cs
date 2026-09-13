using AskARabbiLIB.Grounding;

namespace AskARabbiLIB.Retrieval;

/// <summary>Falls back to original-text keyword search when semantic retrieval misses the question's subject.</summary>
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
        if (hits.Any(hit => hit.IsExactReference) || string.IsNullOrWhiteSpace(query.QueryText) || SourceEvidenceAdequacyEvaluator.Evaluate(query.QueryText, hits).IsAdequate)
        {
            return hits;
        }
        // The same trusted scope is passed unchanged. Synonyms guide lookup only; no
        // model-generated text or guessed source reference can become evidence here.
        var fallback = await keywords.SearchAsync(query, cancellationToken).ConfigureAwait(false);
        var adequacy = SourceEvidenceAdequacyEvaluator.Evaluate(query.QueryText, fallback);
        return adequacy.IsAdequate ? adequacy.OrderedHits.Take(query.CandidateLimit).ToArray() : hits;
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<SourceSegment>> GetContextAsync(string documentId, int documentOrdinal, int radius, CancellationToken cancellationToken = default)
    {
        var local = await keywords.GetContextAsync(documentId, documentOrdinal, radius, cancellationToken).ConfigureAwait(false);
        return local.Count > 0 ? local : await semantic.GetContextAsync(documentId, documentOrdinal, radius, cancellationToken).ConfigureAwait(false);
    }
}
