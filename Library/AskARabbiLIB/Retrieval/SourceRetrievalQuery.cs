namespace AskARabbiLIB.Retrieval;

/// <summary>Defines a bounded segment-retrieval request.</summary>
public sealed record SourceRetrievalQuery
{
    public string? QueryText { get; init; }

    public string? ExactCanonicalReference { get; init; }

    public IReadOnlyCollection<string> Languages { get; init; } = [];

    public IReadOnlyCollection<string> Collections { get; init; } = [];

    public IReadOnlyCollection<string> Categories { get; init; } = [];

    public IReadOnlyCollection<string> WorkKeys { get; init; } = [];

    public IReadOnlyCollection<string> SourceKeys { get; init; } = [];

    public int CandidateLimit { get; init; } = 50;
}
