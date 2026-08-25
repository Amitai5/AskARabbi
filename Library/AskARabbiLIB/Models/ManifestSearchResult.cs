namespace AskARabbiLIB.Models;

/// <summary>Contains one page of ranked manifest search results.</summary>
/// <param name="TotalMatches">Total number of matching documents before pagination.</param>
/// <param name="Skip">Number of matching documents skipped.</param>
/// <param name="Limit">Requested maximum page size.</param>
/// <param name="Hits">Current page of ranked hits.</param>
public sealed record ManifestSearchResult(int TotalMatches, int Skip, int Limit, IReadOnlyList<ManifestSearchHit> Hits);
