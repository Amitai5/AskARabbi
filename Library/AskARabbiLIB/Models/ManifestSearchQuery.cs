namespace AskARabbiLIB.Models;

/// <summary>Defines a metadata search over the in-memory Sefaria manifest.</summary>
public sealed record ManifestSearchQuery
{
    public string? Keywords { get; init; }

    public KeywordMatchMode KeywordMatchMode { get; init; } = KeywordMatchMode.All;

    public IReadOnlyCollection<string> Languages { get; init; } = [];

    public IReadOnlyCollection<string> Collections { get; init; } = [];

    public IReadOnlyCollection<string> Categories { get; init; } = [];

    public IReadOnlyCollection<string> Titles { get; init; } = [];

    public IReadOnlyCollection<string> VersionTitles { get; init; } = [];

    public IReadOnlyCollection<string> Licenses { get; init; } = [];

    public int? MinimumSegmentCount { get; init; }

    public int? MaximumSegmentCount { get; init; }

    public int Skip { get; init; }

    public int Limit { get; init; } = 25;
}
