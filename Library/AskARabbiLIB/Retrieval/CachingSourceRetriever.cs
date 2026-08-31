using System.Collections.Concurrent;
using AskARabbiLIB.Search;

namespace AskARabbiLIB.Retrieval;

/// <summary>Reuses bounded, corpus-only search results while delegating source-context reads to the underlying retriever.</summary>
public sealed class CachingSourceRetriever : ISourceRetriever
{
    private readonly ISourceRetriever inner;
    private readonly SourceRetrieverCacheOptions options;
    private readonly TimeProvider timeProvider;
    private readonly ConcurrentDictionary<SearchCacheKey, CacheEntry> searchEntries = new();

    /// <summary>Creates a bounded process-local retrieval cache.</summary>
    /// <param name="inner">Approved-corpus retriever whose successful searches are cached.</param>
    /// <param name="options">Cache duration and capacity.</param>
    /// <param name="timeProvider">Optional deterministic source of UTC time.</param>
    public CachingSourceRetriever(ISourceRetriever inner, SourceRetrieverCacheOptions? options = null, TimeProvider? timeProvider = null)
    {
        ArgumentNullException.ThrowIfNull(inner);
        this.options = options ?? new SourceRetrieverCacheOptions();
        this.options.Validate();
        this.inner = inner;
        this.timeProvider = timeProvider ?? TimeProvider.System;
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<SourceRetrievalHit>> SearchAsync(SourceRetrievalQuery query, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);
        cancellationToken.ThrowIfCancellationRequested();
        var key = SearchCacheKey.Create(query);
        var now = timeProvider.GetUtcNow();
        if (searchEntries.TryGetValue(key, out var cached) && cached.ExpiresAtUtc > now)
        {
            return cached.Hits;
        }

        searchEntries.TryRemove(key, out _);
        var retrieved = await inner.SearchAsync(query, cancellationToken).ConfigureAwait(false);
        cancellationToken.ThrowIfCancellationRequested();
        var snapshot = retrieved.ToArray();
        MakeRoom(now);
        searchEntries[key] = new CacheEntry(snapshot, now, now + options.Duration);
        return snapshot;
    }

    /// <inheritdoc/>
    public Task<IReadOnlyList<SourceSegment>> GetContextAsync(string documentId, int documentOrdinal, int radius, CancellationToken cancellationToken = default) => inner.GetContextAsync(documentId, documentOrdinal, radius, cancellationToken);

    private void MakeRoom(DateTimeOffset now)
    {
        foreach (var pair in searchEntries)
        {
            if (pair.Value.ExpiresAtUtc <= now)
            {
                searchEntries.TryRemove(pair.Key, out _);
            }
        }

        var overflow = searchEntries.Count - options.MaximumEntries + 1;
        if (overflow <= 0)
        {
            return;
        }

        foreach (var pair in searchEntries.OrderBy(pair => pair.Value.StoredAtUtc).Take(overflow))
        {
            searchEntries.TryRemove(pair.Key, out _);
        }
    }

    private sealed record CacheEntry(IReadOnlyList<SourceRetrievalHit> Hits, DateTimeOffset StoredAtUtc, DateTimeOffset ExpiresAtUtc);

    private readonly record struct SearchCacheKey(string QueryText, string ExactCanonicalReference, string Languages, string Collections, string Categories, string WorkKeys, string SourceKeys, int CandidateLimit)
    {
        internal static SearchCacheKey Create(SourceRetrievalQuery query) => new(
            SearchTextNormalizer.Normalize(query.QueryText),
            SearchTextNormalizer.Normalize(query.ExactCanonicalReference),
            Normalize(query.Languages),
            Normalize(query.Collections),
            Normalize(query.Categories),
            Normalize(query.WorkKeys),
            Normalize(query.SourceKeys),
            query.CandidateLimit);

        private static string Normalize(IEnumerable<string> values) => string.Join('\u001e', values.Select(SearchTextNormalizer.Normalize).Where(value => value.Length > 0).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal));
    }
}
