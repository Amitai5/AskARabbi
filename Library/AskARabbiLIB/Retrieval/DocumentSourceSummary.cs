namespace AskARabbiLIB.Retrieval;

/// <summary>Summarizes one independently selectable logical source in the approved corpus.</summary>
public sealed record DocumentSourceSummary
{
    /// <summary>Gets the stable source-selection key.</summary>
    public required string Key { get; init; }

    /// <summary>Gets the human-readable source name.</summary>
    public required string DisplayName { get; init; }

    /// <summary>Gets the number of document editions assigned to the source.</summary>
    public required int DocumentCount { get; init; }

    /// <summary>Gets the number of citation-addressable passages assigned to the source.</summary>
    public required long SegmentCount { get; init; }

    /// <summary>Gets the available edition languages.</summary>
    public required IReadOnlyList<string> Languages { get; init; }
}
