namespace AskARabbiLIB.Models;

/// <summary>Contains available manifest facet values and their document counts.</summary>
/// <param name="Languages">Language display names and counts.</param>
/// <param name="LanguageCodes">Language codes and counts.</param>
/// <param name="Collections">Top-level collections and counts.</param>
/// <param name="Categories">Category paths and counts.</param>
/// <param name="Titles">Document titles and counts.</param>
/// <param name="VersionTitles">Edition titles and counts.</param>
/// <param name="Licenses">Source license labels and counts.</param>
public sealed record ManifestFacetSummary(IReadOnlyDictionary<string, int> Languages, IReadOnlyDictionary<string, int> LanguageCodes, IReadOnlyDictionary<string, int> Collections, IReadOnlyDictionary<string, int> Categories, IReadOnlyDictionary<string, int> Titles, IReadOnlyDictionary<string, int> VersionTitles, IReadOnlyDictionary<string, int> Licenses);
