using System.Globalization;
using AskARabbiLIB.Models;

namespace AskARabbiLIB.Retrieval;

/// <summary>Builds the complete set of independently selectable sources from a validated manifest.</summary>
public sealed class DocumentSourceCatalog
{
    private const string CollectionPrefix = "collection:";
    private const string WorkPrefix = "work:";

    private static readonly IReadOnlyDictionary<string, string> WorkDisplayNames = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
    {
        ["rif"] = "Rif",
        ["mishneh_torah"] = "Mishneh Torah",
        ["shulchan_arukh_with_rema"] = "Shulchan Arukh with Rema",
        ["zohar"] = "Zohar",
        ["zohar_chadash"] = "Zohar Chadash",
        ["mesillat_yesharim"] = "Mesillat Yesharim",
    };

    private static readonly IReadOnlyDictionary<string, int> SourceSortOrder = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
    {
        ["collection:Torah"] = 0,
        ["collection:Tanakh"] = 1,
        ["collection:Mishnah"] = 2,
        ["collection:Talmud"] = 3,
        ["work:rif"] = 4,
        ["work:mishneh_torah"] = 5,
        ["work:shulchan_arukh_with_rema"] = 6,
        ["work:zohar"] = 7,
        ["work:zohar_chadash"] = 8,
        ["work:mesillat_yesharim"] = 9,
    };

    private DocumentSourceCatalog(IReadOnlyList<DocumentSourceSummary> sources)
    {
        Sources = sources;
        DocumentCount = sources.Sum(source => source.DocumentCount);
        SegmentCount = sources.Sum(source => source.SegmentCount);
    }

    /// <summary>Gets all logical sources in display order.</summary>
    public IReadOnlyList<DocumentSourceSummary> Sources { get; }

    /// <summary>Gets the number of logical sources.</summary>
    public int SourceCount => Sources.Count;

    /// <summary>Gets the total number of document editions represented by the catalog.</summary>
    public int DocumentCount { get; }

    /// <summary>Gets the total number of citation-addressable passages represented by the catalog.</summary>
    public long SegmentCount { get; }

    /// <summary>Creates a complete logical-source catalog from manifest documents.</summary>
    /// <param name="manifest">Validated corpus manifest.</param>
    /// <returns>A catalog whose source groups partition every manifest document exactly once.</returns>
    public static DocumentSourceCatalog Create(DocumentManifest manifest)
    {
        ArgumentNullException.ThrowIfNull(manifest);
        if (manifest.Documents is null || manifest.DocumentCount != manifest.Documents.Count)
        {
            throw new ArgumentException("The manifest document count is inconsistent.", nameof(manifest));
        }

        var sources = manifest.Documents
            .GroupBy(GetSourceKey, StringComparer.OrdinalIgnoreCase)
            .Select(group => CreateSummary(group.Key, group))
            .OrderBy(source => GetSortOrder(source.Key))
            .ThenBy(source => source.DisplayName, StringComparer.OrdinalIgnoreCase)
            .ToArray();
        return new DocumentSourceCatalog(sources);
    }

    internal static string GetSourceKey(ManifestDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);
        return document.WorkKey is null ? $"{CollectionPrefix}{document.Collection}" : $"{WorkPrefix}{document.WorkKey}";
    }

    internal static bool TryParseSourceKey(string key, out bool isWork, out string value)
    {
        if (key.StartsWith(WorkPrefix, StringComparison.OrdinalIgnoreCase) && key.Length > WorkPrefix.Length)
        {
            isWork = true;
            value = key[WorkPrefix.Length..];
            return true;
        }
        if (key.StartsWith(CollectionPrefix, StringComparison.OrdinalIgnoreCase) && key.Length > CollectionPrefix.Length)
        {
            isWork = false;
            value = key[CollectionPrefix.Length..];
            return true;
        }

        isWork = false;
        value = string.Empty;
        return false;
    }

    private static DocumentSourceSummary CreateSummary(string key, IEnumerable<ManifestDocument> documents)
    {
        var editions = documents.ToArray();
        var first = editions[0];
        return new DocumentSourceSummary
        {
            Key = key,
            DisplayName = GetDisplayName(first),
            DocumentCount = editions.Length,
            SegmentCount = editions.Sum(document => (long)document.SegmentCount),
            Languages = editions.Select(document => document.FileLanguage).Distinct(StringComparer.OrdinalIgnoreCase).Order(StringComparer.OrdinalIgnoreCase).ToArray(),
        };
    }

    private static string GetDisplayName(ManifestDocument document)
    {
        if (document.WorkKey is null)
        {
            return document.Collection;
        }
        if (WorkDisplayNames.TryGetValue(document.WorkKey, out var displayName))
        {
            return displayName;
        }
        return CultureInfo.InvariantCulture.TextInfo.ToTitleCase(document.WorkKey.Replace('_', ' '));
    }

    private static int GetSortOrder(string key) => SourceSortOrder.TryGetValue(key, out var order) ? order : int.MaxValue;
}
