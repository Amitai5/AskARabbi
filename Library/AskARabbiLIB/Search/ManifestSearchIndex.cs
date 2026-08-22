using System.Collections.Frozen;
using AskARabbiLIB.Models;

namespace AskARabbiLIB.Search;

/// <summary>Provides deterministic, indexed searches over an in-memory document manifest.</summary>
public sealed class ManifestSearchIndex
{
    public const int MaximumResultLimit = 200;

    private readonly IndexedDocument[] indexedDocuments;
    private readonly FrozenDictionary<string, int[]> tokenPostings;
    private readonly string[] sortedTokens;
    private readonly FrozenDictionary<string, int[]> languagePostings;
    private readonly FrozenDictionary<string, int[]> collectionPostings;
    private readonly FrozenDictionary<string, int[]> categoryPostings;
    private readonly FrozenDictionary<string, int[]> titlePostings;
    private readonly FrozenDictionary<string, int[]> versionTitlePostings;
    private readonly FrozenDictionary<string, int[]> licensePostings;
    private readonly ManifestFacetSummary facets;

    private ManifestSearchIndex(DocumentManifest manifest)
    {
        indexedDocuments = manifest.Documents.Select((document, index) => CreateIndexedDocument(index, document)).ToArray();
        tokenPostings = BuildTokenPostings(indexedDocuments);
        sortedTokens = tokenPostings.Keys.Order(StringComparer.Ordinal).ToArray();
        languagePostings = BuildPostings(indexedDocuments, value => new[] { value.Document.FileLanguage, value.Document.FileLanguageCode });
        collectionPostings = BuildPostings(indexedDocuments, value => new[] { value.Document.Collection });
        categoryPostings = BuildPostings(indexedDocuments, value => value.Document.Categories.Append(string.Join(" > ", value.Document.Categories)));
        titlePostings = BuildPostings(indexedDocuments, value => new[] { value.Document.FileTitle, value.Document.HebrewTitle });
        versionTitlePostings = BuildPostings(indexedDocuments, value => new[] { value.Document.VersionTitle });
        licensePostings = BuildPostings(indexedDocuments, value => new[] { value.Document.License });
        facets = CreateFacets(manifest.Documents);
    }

    /// <summary>Builds immutable search indexes for a validated manifest.</summary>
    /// <param name="manifest">Validated manifest to index.</param>
    /// <returns>An immutable in-memory search index.</returns>
    public static ManifestSearchIndex Create(DocumentManifest manifest)
    {
        ArgumentNullException.ThrowIfNull(manifest);
        if (manifest.Documents is null)
        {
            throw new ArgumentException("The manifest must contain a documents collection.", nameof(manifest));
        }

        return new ManifestSearchIndex(manifest);
    }

    /// <summary>Searches indexed document metadata using keyword, facet, range, and pagination criteria.</summary>
    /// <param name="query">Search criteria.</param>
    /// <returns>A deterministic page of ranked search results.</returns>
    public ManifestSearchResult Search(ManifestSearchQuery query)
    {
        ArgumentNullException.ThrowIfNull(query);
        ValidateQuery(query);

        HashSet<int>? candidates = null;
        ApplyFacetFilter(ref candidates, languagePostings, query.Languages, nameof(query.Languages));
        ApplyFacetFilter(ref candidates, collectionPostings, query.Collections, nameof(query.Collections));
        ApplyFacetFilter(ref candidates, categoryPostings, query.Categories, nameof(query.Categories));
        ApplyFacetFilter(ref candidates, titlePostings, query.Titles, nameof(query.Titles));
        ApplyFacetFilter(ref candidates, versionTitlePostings, query.VersionTitles, nameof(query.VersionTitles));
        ApplyFacetFilter(ref candidates, licensePostings, query.Licenses, nameof(query.Licenses));

        var keywordTokens = SearchTextNormalizer.Tokenize(query.Keywords);
        if (!string.IsNullOrWhiteSpace(query.Keywords) && keywordTokens.Length == 0)
        {
            throw new ArgumentException("Keywords must contain at least one letter or digit.", nameof(query));
        }
        ApplyKeywordFilter(ref candidates, keywordTokens, query.KeywordMatchMode);

        candidates ??= Enumerable.Range(0, indexedDocuments.Length).ToHashSet();
        if (query.MinimumSegmentCount.HasValue)
        {
            candidates.RemoveWhere(index => indexedDocuments[index].Document.SegmentCount < query.MinimumSegmentCount.Value);
        }
        if (query.MaximumSegmentCount.HasValue)
        {
            candidates.RemoveWhere(index => indexedDocuments[index].Document.SegmentCount > query.MaximumSegmentCount.Value);
        }

        var normalizedPhrase = SearchTextNormalizer.Normalize(query.Keywords);
        var hits = candidates
            .Select(index => CreateHit(indexedDocuments[index], keywordTokens, normalizedPhrase))
            .OrderByDescending(hit => hit.Score)
            .ThenBy(hit => hit.Document.FileTitle, StringComparer.OrdinalIgnoreCase)
            .ThenBy(hit => hit.Document.FileLanguage, StringComparer.OrdinalIgnoreCase)
            .ThenBy(hit => hit.Document.VersionTitle, StringComparer.OrdinalIgnoreCase)
            .ThenBy(hit => hit.Document.FilePath, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        var page = hits.Skip(query.Skip).Take(query.Limit).ToArray();
        return new ManifestSearchResult(hits.Length, query.Skip, query.Limit, page);
    }

    /// <summary>Returns available facet values and document counts.</summary>
    /// <returns>A read-only facet summary.</returns>
    public ManifestFacetSummary GetFacets() => facets;

    private static IndexedDocument CreateIndexedDocument(int id, ManifestDocument document)
    {
        var fields = new[]
        {
            CreateField("fileTitle", 100, document.FileTitle),
            CreateField("hebrewTitle", 100, document.HebrewTitle),
            CreateField("firstReference", 80, document.FirstReference),
            CreateField("lastReference", 80, document.LastReference),
            CreateField("collection", 60, document.Collection),
            CreateField("categories", 60, string.Join(" ", document.Categories)),
            CreateField("fileLanguage", 40, document.FileLanguage),
            CreateField("fileLanguageCode", 40, document.FileLanguageCode),
            CreateField("versionTitle", 40, document.VersionTitle),
            CreateField("license", 40, document.License),
            CreateField("fileDescription", 20, document.FileDescription),
            CreateField("filePath", 10, document.FilePath),
            CreateField("rawFilePath", 10, document.RawFilePath),
        };
        return new IndexedDocument(id, document, fields);
    }

    private static SearchField CreateField(string name, int weight, string? value)
    {
        var normalized = SearchTextNormalizer.Normalize(value);
        var tokens = SearchTextNormalizer.Tokenize(value).ToFrozenSet(StringComparer.Ordinal);
        return new SearchField(name, weight, normalized, tokens);
    }

    private static FrozenDictionary<string, int[]> BuildTokenPostings(IEnumerable<IndexedDocument> documents)
    {
        var postings = new Dictionary<string, HashSet<int>>(StringComparer.Ordinal);
        foreach (var document in documents)
        {
            foreach (var token in document.Fields.SelectMany(field => field.Tokens).Distinct(StringComparer.Ordinal))
            {
                AddPosting(postings, token, document.Id);
            }
        }

        return FreezePostings(postings);
    }

    private static FrozenDictionary<string, int[]> BuildPostings(IEnumerable<IndexedDocument> documents, Func<IndexedDocument, IEnumerable<string?>> valueSelector)
    {
        var postings = new Dictionary<string, HashSet<int>>(StringComparer.Ordinal);
        foreach (var document in documents)
        {
            foreach (var value in valueSelector(document))
            {
                var normalized = SearchTextNormalizer.Normalize(value);
                if (normalized.Length > 0)
                {
                    AddPosting(postings, normalized, document.Id);
                }
            }
        }

        return FreezePostings(postings);
    }

    private static void AddPosting(Dictionary<string, HashSet<int>> postings, string value, int documentId)
    {
        if (!postings.TryGetValue(value, out var documentIds))
        {
            documentIds = new HashSet<int>();
            postings.Add(value, documentIds);
        }
        documentIds.Add(documentId);
    }

    private static FrozenDictionary<string, int[]> FreezePostings(Dictionary<string, HashSet<int>> postings) => postings.ToFrozenDictionary(pair => pair.Key, pair => pair.Value.Order().ToArray(), StringComparer.Ordinal);

    private static void ApplyFacetFilter(ref HashSet<int>? candidates, FrozenDictionary<string, int[]> postings, IReadOnlyCollection<string>? values, string parameterName)
    {
        if (values is null || values.Count == 0)
        {
            return;
        }

        var groupMatches = new HashSet<int>();
        foreach (var value in values)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                throw new ArgumentException("Filter values cannot be null, empty, or whitespace.", parameterName);
            }
            var normalized = SearchTextNormalizer.Normalize(value);
            if (postings.TryGetValue(normalized, out var documentIds))
            {
                groupMatches.UnionWith(documentIds);
            }
        }

        IntersectCandidates(ref candidates, groupMatches);
    }

    private void ApplyKeywordFilter(ref HashSet<int>? candidates, IReadOnlyList<string> tokens, KeywordMatchMode matchMode)
    {
        if (tokens.Count == 0)
        {
            return;
        }

        if (matchMode == KeywordMatchMode.All)
        {
            foreach (var token in tokens)
            {
                IntersectCandidates(ref candidates, GetTokenMatches(token));
            }
            return;
        }

        var anyMatches = new HashSet<int>();
        foreach (var token in tokens)
        {
            anyMatches.UnionWith(GetTokenMatches(token));
        }
        IntersectCandidates(ref candidates, anyMatches);
    }

    private HashSet<int> GetTokenMatches(string token)
    {
        var matches = new HashSet<int>();
        if (token.Length < 2)
        {
            if (tokenPostings.TryGetValue(token, out var exactMatches))
            {
                matches.UnionWith(exactMatches);
            }
            return matches;
        }

        var startIndex = FindFirstTokenAtOrAfter(token);
        for (var index = startIndex; index < sortedTokens.Length && sortedTokens[index].StartsWith(token, StringComparison.Ordinal); index++)
        {
            matches.UnionWith(tokenPostings[sortedTokens[index]]);
        }
        return matches;
    }

    private int FindFirstTokenAtOrAfter(string token)
    {
        var lower = 0;
        var upper = sortedTokens.Length;
        while (lower < upper)
        {
            var middle = lower + ((upper - lower) / 2);
            if (StringComparer.Ordinal.Compare(sortedTokens[middle], token) < 0)
            {
                lower = middle + 1;
            }
            else
            {
                upper = middle;
            }
        }
        return lower;
    }

    private static void IntersectCandidates(ref HashSet<int>? candidates, IEnumerable<int> matches)
    {
        if (candidates is null)
        {
            candidates = matches.ToHashSet();
            return;
        }
        candidates.IntersectWith(matches);
    }

    private static ManifestSearchHit CreateHit(IndexedDocument document, IReadOnlyList<string> keywordTokens, string normalizedPhrase)
    {
        if (keywordTokens.Count == 0)
        {
            return new ManifestSearchHit(document.Document, 0, Array.Empty<string>());
        }

        var score = 0;
        var matchedFields = new HashSet<string>(StringComparer.Ordinal);
        foreach (var token in keywordTokens)
        {
            var bestWeight = 0;
            foreach (var field in document.Fields)
            {
                var exactMatch = field.Tokens.Contains(token);
                var prefixMatch = !exactMatch && token.Length >= 2 && field.Tokens.Any(fieldToken => fieldToken.StartsWith(token, StringComparison.Ordinal));
                if (!exactMatch && !prefixMatch)
                {
                    continue;
                }

                matchedFields.Add(field.Name);
                var fieldScore = exactMatch ? field.Weight : field.Weight / 2;
                bestWeight = Math.Max(bestWeight, fieldScore);
            }
            score += bestWeight;
        }

        var phraseBonus = document.Fields
            .Where(field => string.Equals(field.Normalized, normalizedPhrase, StringComparison.Ordinal) || (keywordTokens.Count > 1 && field.Normalized.Contains(normalizedPhrase, StringComparison.Ordinal)))
            .Select(field => field.Weight)
            .DefaultIfEmpty(0)
            .Max();
        score += phraseBonus;

        return new ManifestSearchHit(document.Document, score, matchedFields.Order(StringComparer.Ordinal).ToArray());
    }

    private static ManifestFacetSummary CreateFacets(IReadOnlyList<ManifestDocument> documents)
    {
        return new ManifestFacetSummary(
            CreateFacet(documents.Select(document => document.FileLanguage)),
            CreateFacet(documents.Select(document => document.FileLanguageCode)),
            CreateFacet(documents.Select(document => document.Collection)),
            CreateFacet(documents.SelectMany(document => document.Categories.Append(string.Join(" > ", document.Categories)))),
            CreateFacet(documents.Select(document => document.FileTitle)),
            CreateFacet(documents.Select(document => document.VersionTitle)),
            CreateFacet(documents.Select(document => document.License)));
    }

    private static IReadOnlyDictionary<string, int> CreateFacet(IEnumerable<string?> values)
    {
        var counts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        foreach (var value in values)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                continue;
            }
            counts[value] = counts.GetValueOrDefault(value) + 1;
        }
        return counts.ToFrozenDictionary(StringComparer.OrdinalIgnoreCase);
    }

    private static void ValidateQuery(ManifestSearchQuery query)
    {
        if (!Enum.IsDefined(query.KeywordMatchMode))
        {
            throw new ArgumentOutOfRangeException(nameof(query), "KeywordMatchMode is invalid.");
        }
        if (query.Skip < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(query), "Skip cannot be negative.");
        }
        if (query.Limit < 1 || query.Limit > MaximumResultLimit)
        {
            throw new ArgumentOutOfRangeException(nameof(query), $"Limit must be between 1 and {MaximumResultLimit}.");
        }
        if (query.MinimumSegmentCount < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(query), "MinimumSegmentCount cannot be negative.");
        }
        if (query.MaximumSegmentCount < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(query), "MaximumSegmentCount cannot be negative.");
        }
        if (query.MinimumSegmentCount.HasValue && query.MaximumSegmentCount.HasValue && query.MinimumSegmentCount > query.MaximumSegmentCount)
        {
            throw new ArgumentException("MinimumSegmentCount cannot exceed MaximumSegmentCount.", nameof(query));
        }
    }

    private sealed record IndexedDocument(int Id, ManifestDocument Document, IReadOnlyList<SearchField> Fields);

    private sealed record SearchField(string Name, int Weight, string Normalized, FrozenSet<string> Tokens);
}
