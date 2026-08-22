namespace AskARabbiLIB.Models;

/// <summary>Controls whether every keyword or any keyword must match.</summary>
public enum KeywordMatchMode
{
    All,
    Any,
}

/// <summary>Defines a metadata search over the in-memory Sefaria manifest.</summary>
public sealed record ManifestSearchQuery
{
    public string? Keywords { get; init; }

    public KeywordMatchMode KeywordMatchMode { get; init; } = KeywordMatchMode.All;

    public IReadOnlyCollection<string> Languages { get; init; } = Array.Empty<string>();

    public IReadOnlyCollection<string> Collections { get; init; } = Array.Empty<string>();

    public IReadOnlyCollection<string> Categories { get; init; } = Array.Empty<string>();

    public IReadOnlyCollection<string> Titles { get; init; } = Array.Empty<string>();

    public IReadOnlyCollection<string> VersionTitles { get; init; } = Array.Empty<string>();

    public IReadOnlyCollection<string> Licenses { get; init; } = Array.Empty<string>();

    public int? MinimumSegmentCount { get; init; }

    public int? MaximumSegmentCount { get; init; }

    public int Skip { get; init; }

    public int Limit { get; init; } = 25;
}

/// <summary>Represents one ranked document match.</summary>
public sealed record ManifestSearchHit(ManifestDocument Document, int Score, IReadOnlyList<string> MatchedFields);

/// <summary>Contains one page of ranked manifest search results.</summary>
public sealed record ManifestSearchResult(int TotalMatches, int Skip, int Limit, IReadOnlyList<ManifestSearchHit> Hits);

/// <summary>Contains available manifest facet values and their document counts.</summary>
public sealed record ManifestFacetSummary(
    IReadOnlyDictionary<string, int> Languages,
    IReadOnlyDictionary<string, int> LanguageCodes,
    IReadOnlyDictionary<string, int> Collections,
    IReadOnlyDictionary<string, int> Categories,
    IReadOnlyDictionary<string, int> Titles,
    IReadOnlyDictionary<string, int> VersionTitles,
    IReadOnlyDictionary<string, int> Licenses);
