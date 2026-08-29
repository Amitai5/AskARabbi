using AskARabbiLIB.Models;
using AskARabbiLIB.Search;

namespace AskARabbiLIB.Retrieval;

/// <summary>Retrieves trusted source segments through forced Azure Responses file-search calls.</summary>
public sealed class AzureOpenAIVectorStoreRetriever : ISourceRetriever
{
    private readonly IAzureOpenAIVectorStoreSearchClient client;
    private readonly AzureOpenAIVectorStoreRetrieverOptions options;
    private readonly AzureOpenAIVectorStoreCorpusParser parser;
    private readonly int manifestDocumentCount;
    private readonly SemaphoreSlim verificationGate = new(1, 1);
    private volatile bool isVerified;

    /// <summary>Creates a production retriever without performing network work.</summary>
    /// <param name="client">Narrow managed file-search client.</param>
    /// <param name="options">Immutable accepted store identity.</param>
    /// <param name="manifest">Bundled metadata catalog matching the published corpus.</param>
    public AzureOpenAIVectorStoreRetriever(IAzureOpenAIVectorStoreSearchClient client, AzureOpenAIVectorStoreRetrieverOptions options, DocumentManifest manifest)
    {
        ArgumentNullException.ThrowIfNull(client);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(manifest);
        options.Validate();
        if (!string.Equals(SourceIndexBuilder.ComputeCorpusFingerprint(manifest), options.ExpectedCorpusFingerprint, StringComparison.Ordinal))
        {
            throw new ArgumentException("Bundled manifest fingerprint does not match the configured managed corpus.", nameof(manifest));
        }
        this.client = client;
        this.options = options;
        parser = new AzureOpenAIVectorStoreCorpusParser(manifest);
        manifestDocumentCount = manifest.DocumentCount;
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<SourceRetrievalHit>> SearchAsync(SourceRetrievalQuery query, CancellationToken cancellationToken = default)
    {
        ValidateQuery(query);
        await EnsureVerifiedAsync(cancellationToken).ConfigureAwait(false);
        var hits = new Dictionary<string, SourceRetrievalHit>(StringComparer.Ordinal);
        if (!string.IsNullOrWhiteSpace(query.ExactCanonicalReference))
        {
            var exactRequest = CreateSearchRequest(query, [$"Canonical reference: {query.ExactCanonicalReference.Trim()}"], false);
            var exactPage = await client.SearchAsync(options.VectorStoreId, exactRequest, cancellationToken).ConfigureAwait(false);
            AddResults(exactPage, query, true, hits);
        }
        if (!string.IsNullOrWhiteSpace(query.QueryText))
        {
            var semanticRequest = CreateSearchRequest(query, [query.QueryText.Trim()], options.RewriteQuery);
            var semanticPage = await client.SearchAsync(options.VectorStoreId, semanticRequest, cancellationToken).ConfigureAwait(false);
            AddResults(semanticPage, query, false, hits);
        }

        return hits.Values
            .OrderByDescending(hit => hit.IsExactReference)
            .ThenByDescending(hit => hit.Score)
            .ThenBy(hit => hit.Segment.CanonicalReference, StringComparer.OrdinalIgnoreCase)
            .ThenBy(hit => hit.Segment.SegmentId, StringComparer.Ordinal)
            .Take(query.CandidateLimit)
            .ToArray();
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<SourceSegment>> GetContextAsync(string documentId, int documentOrdinal, int radius, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(documentId);
        if (documentOrdinal < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(documentOrdinal), "Document ordinal cannot be negative.");
        }
        if (radius is < 0 or > 10)
        {
            throw new ArgumentOutOfRangeException(nameof(radius), "Context radius must be between zero and ten.");
        }
        await EnsureVerifiedAsync(cancellationToken).ConfigureAwait(false);
        var minimum = Math.Max(0, documentOrdinal - radius);
        var maximum = checked(documentOrdinal + radius);
        var queries = Enumerable.Range(minimum, maximum - minimum + 1).Select(ordinal => AzureOpenAIVectorStoreCorpusFormatter.CreateLookupToken(documentId, ordinal)).ToArray();
        var request = new AzureOpenAIVectorStoreSearchRequest
        {
            Queries = queries,
            DocumentIds = [documentId],
            MaximumResults = 50,
            ScoreThreshold = 0,
            RewriteQuery = false,
        };
        var page = await client.SearchAsync(options.VectorStoreId, request, cancellationToken).ConfigureAwait(false);
        var segments = new Dictionary<string, SourceSegment>(StringComparer.Ordinal);
        foreach (var result in page.Results)
        {
            foreach (var segment in parser.Parse(result.Attributes, result.Content, options.ExpectedCorpusFingerprint))
            {
                if (string.Equals(segment.DocumentId, documentId, StringComparison.Ordinal) && segment.DocumentOrdinal >= minimum && segment.DocumentOrdinal <= maximum)
                {
                    segments[segment.SegmentId] = segment;
                }
            }
        }
        return segments.Values.OrderBy(segment => segment.DocumentOrdinal).ThenBy(segment => segment.ExcerptStart).ThenBy(segment => segment.SegmentId, StringComparer.Ordinal).ToArray();
    }

    private async Task EnsureVerifiedAsync(CancellationToken cancellationToken)
    {
        if (isVerified)
        {
            return;
        }
        await verificationGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (isVerified)
            {
                return;
            }
            var store = await client.GetAsync(options.VectorStoreId, cancellationToken).ConfigureAwait(false);
            ValidateStore(store);
            isVerified = true;
        }
        finally
        {
            verificationGate.Release();
        }
    }

    private void AddResults(AzureOpenAIVectorStoreSearchPage page, SourceRetrievalQuery query, bool exactReferenceRequest, IDictionary<string, SourceRetrievalHit> hits)
    {
        foreach (var result in page.Results)
        {
            foreach (var segment in parser.Parse(result.Attributes, result.Content, options.ExpectedCorpusFingerprint))
            {
                var isExact = exactReferenceRequest && string.Equals(segment.CanonicalReference, query.ExactCanonicalReference?.Trim(), StringComparison.OrdinalIgnoreCase);
                if (exactReferenceRequest && !isExact || !MatchesFilters(segment, query))
                {
                    continue;
                }
                var hit = new SourceRetrievalHit(segment, result.Score, isExact);
                if (!hits.TryGetValue(segment.SegmentId, out var existing) || hit.IsExactReference && !existing.IsExactReference || hit.IsExactReference == existing.IsExactReference && hit.Score > existing.Score)
                {
                    hits[segment.SegmentId] = hit;
                }
            }
        }
    }

    private AzureOpenAIVectorStoreSearchRequest CreateSearchRequest(SourceRetrievalQuery query, IReadOnlyList<string> queries, bool rewriteQuery) => new()
    {
        Queries = queries,
        Languages = query.Languages,
        Collections = query.Collections,
        Categories = query.Categories,
        WorkKeys = query.WorkKeys,
        SourceKeys = query.SourceKeys,
        MaximumResults = Math.Min(50, query.CandidateLimit),
        ScoreThreshold = options.ScoreThreshold,
        RewriteQuery = rewriteQuery,
    };

    private void ValidateStore(AzureOpenAIVectorStoreInfo store)
    {
        if (!string.Equals(store.Status, "completed", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException($"Azure vector store '{store.Id}' is not ready; current status is '{store.Status}'.");
        }
        if (!store.Metadata.TryGetValue(AzureOpenAIVectorStoreCorpusContract.StoreSchemaMetadata, out var schema) || !string.Equals(schema, AzureOpenAIVectorStoreCorpusContract.StoreSchemaVersion, StringComparison.Ordinal))
        {
            throw new InvalidOperationException("Azure vector-store schema is missing or stale.");
        }
        if (!store.Metadata.TryGetValue(AzureOpenAIVectorStoreCorpusContract.StoreFingerprintMetadata, out var fingerprint) || !string.Equals(fingerprint, options.ExpectedCorpusFingerprint, StringComparison.Ordinal))
        {
            throw new InvalidOperationException("Azure vector-store fingerprint does not match the configured corpus.");
        }
        if (!store.Metadata.TryGetValue(AzureOpenAIVectorStoreCorpusContract.StoreDocumentCountMetadata, out var documentCountText) || !int.TryParse(documentCountText, System.Globalization.NumberStyles.None, System.Globalization.CultureInfo.InvariantCulture, out var documentCount) || documentCount != manifestDocumentCount)
        {
            throw new InvalidOperationException("Azure vector-store logical document count does not match the bundled manifest.");
        }
        if (!store.Metadata.TryGetValue(AzureOpenAIVectorStoreCorpusContract.StoreFileCountMetadata, out var fileCountText) || !int.TryParse(fileCountText, System.Globalization.NumberStyles.None, System.Globalization.CultureInfo.InvariantCulture, out var fileCount) || fileCount < documentCount || fileCount != store.CompletedFileCount || store.FailedFileCount != 0)
        {
            throw new InvalidOperationException("Azure vector-store file counts do not match its immutable corpus metadata.");
        }
        if (!store.Metadata.TryGetValue(AzureOpenAIVectorStoreCorpusContract.StoreSourceProviderMetadata, out var sourceProvider) || !string.Equals(sourceProvider, "Sefaria", StringComparison.Ordinal))
        {
            throw new InvalidOperationException("Azure vector-store source provider is missing or unsupported.");
        }
    }

    private static bool MatchesFilters(SourceSegment segment, SourceRetrievalQuery query)
    {
        if (!MatchesAny(query.Languages, segment.Language, segment.LanguageCode) || !MatchesAny(query.Collections, segment.Collection) || !MatchesAny(query.WorkKeys, segment.WorkKey) || !MatchesAny(query.Categories, segment.Categories))
        {
            return false;
        }
        if (query.SourceKeys.Count == 0)
        {
            return true;
        }
        return query.SourceKeys.Any(sourceKey =>
        {
            if (!DocumentSourceCatalog.TryParseSourceKey(sourceKey.Trim(), out var isWork, out var value))
            {
                return false;
            }
            return isWork
                ? string.Equals(segment.WorkKey, value, StringComparison.OrdinalIgnoreCase)
                : segment.WorkKey is null && string.Equals(segment.Collection, value, StringComparison.OrdinalIgnoreCase);
        });
    }

    private static bool MatchesAny(IReadOnlyCollection<string> filters, params string?[] values) => filters.Count == 0 || filters.Any(filter => values.Any(value => value is not null && string.Equals(filter.Trim(), value, StringComparison.OrdinalIgnoreCase)));

    private static bool MatchesAny(IReadOnlyCollection<string> filters, IReadOnlyList<string> values) => filters.Count == 0 || filters.Any(filter => values.Contains(filter.Trim(), StringComparer.OrdinalIgnoreCase));

    private static void ValidateQuery(SourceRetrievalQuery query)
    {
        ArgumentNullException.ThrowIfNull(query);
        if (query.CandidateLimit is < 1 or > 50)
        {
            throw new ArgumentOutOfRangeException(nameof(query), "Managed vector-store candidate limit must be between one and fifty.");
        }
        if (string.IsNullOrWhiteSpace(query.QueryText) && string.IsNullOrWhiteSpace(query.ExactCanonicalReference))
        {
            throw new ArgumentException("A query must contain text or an exact canonical reference.", nameof(query));
        }
        if (!string.IsNullOrWhiteSpace(query.QueryText) && SearchTextNormalizer.Tokenize(query.QueryText).Length == 0 && string.IsNullOrWhiteSpace(query.ExactCanonicalReference))
        {
            throw new ArgumentException("Query text must contain at least one letter or digit.", nameof(query));
        }
        ValidateFilter(query.Languages, nameof(query.Languages));
        ValidateFilter(query.Collections, nameof(query.Collections));
        ValidateFilter(query.Categories, nameof(query.Categories));
        ValidateFilter(query.WorkKeys, nameof(query.WorkKeys));
        ValidateFilter(query.SourceKeys, nameof(query.SourceKeys));
        if (query.SourceKeys.Any(sourceKey => !DocumentSourceCatalog.TryParseSourceKey(sourceKey.Trim(), out _, out _)))
        {
            throw new ArgumentException("Source keys must start with 'work:' or 'collection:' and include a value.", nameof(query));
        }
    }

    private static void ValidateFilter(IReadOnlyCollection<string> values, string name)
    {
        if (values is null || values.Any(string.IsNullOrWhiteSpace))
        {
            throw new ArgumentException($"Filter '{name}' must contain only nonempty values.", name);
        }
    }
}
