namespace AskARabbiLIB.Retrieval;

/// <summary>Defines one bounded Azure OpenAI Responses file-search request.</summary>
public sealed record AzureOpenAIVectorStoreSearchRequest
{
    /// <summary>Gets one or more semantic or exact lookup queries.</summary>
    public required IReadOnlyList<string> Queries { get; init; }

    /// <summary>Gets accepted language names or codes.</summary>
    public IReadOnlyCollection<string> Languages { get; init; } = [];

    /// <summary>Gets accepted collections.</summary>
    public IReadOnlyCollection<string> Collections { get; init; } = [];

    /// <summary>Gets accepted source categories used as retrieval hints and enforced after parsing.</summary>
    public IReadOnlyCollection<string> Categories { get; init; } = [];

    /// <summary>Gets accepted supplemental work keys.</summary>
    public IReadOnlyCollection<string> WorkKeys { get; init; } = [];

    /// <summary>Gets accepted logical source selectors.</summary>
    public IReadOnlyCollection<string> SourceKeys { get; init; } = [];

    /// <summary>Gets accepted stable document IDs.</summary>
    public IReadOnlyCollection<string> DocumentIds { get; init; } = [];

    /// <summary>Gets the maximum number of provider results.</summary>
    public int MaximumResults { get; init; } = 50;

    /// <summary>Gets the provider score threshold.</summary>
    public double ScoreThreshold { get; init; }

    /// <summary>Gets whether the retrieval model may rewrite the query.</summary>
    public bool RewriteQuery { get; init; } = true;
}
