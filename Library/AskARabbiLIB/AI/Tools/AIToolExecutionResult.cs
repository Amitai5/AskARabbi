using AskARabbiLIB.Retrieval;

namespace AskARabbiLIB.AI.Tools;

/// <summary>Represents a successful or failed local tool execution.</summary>
public sealed record AIToolExecutionResult
{
    /// <summary>Gets whether the local operation completed successfully.</summary>
    public required bool IsSuccess { get; init; }

    /// <summary>Gets structured result data safe to expose to the model.</summary>
    public object? Data { get; init; }

    /// <summary>Gets exact calculated evidence to add to the trusted request packet.</summary>
    public AIToolEvidence? Evidence { get; init; }

    /// <summary>Gets independently citable source excerpts returned by a read-only research capability.</summary>
    public IReadOnlyList<SourceSegment> Sources { get; init; } = [];

    /// <summary>Creates a successful research result while retaining each original source's identity.</summary>
    /// <param name="data">Bounded model-facing search metadata.</param>
    /// <param name="sources">One to five original excerpts, with at most 8,000 characters in total.</param>
    /// <returns>A source-backed tool result.</returns>
    public static AIToolExecutionResult FromSources(object data, IReadOnlyList<SourceSegment> sources)
    {
        ArgumentNullException.ThrowIfNull(data);
        ArgumentNullException.ThrowIfNull(sources);
        if (sources.Count is < 1 or > 5 || sources.Any(source => string.IsNullOrWhiteSpace(source.Text)) || sources.Sum(source => (long)source.Text.Length) > 8_000)
        {
            throw new ArgumentException("Research results require one to five nonempty, bounded source excerpts.", nameof(sources));
        }
        return new AIToolExecutionResult { IsSuccess = true, Data = data, Sources = sources };
    }

    /// <summary>Gets a bounded failure explanation when execution did not succeed.</summary>
    public string? ErrorMessage { get; init; }

    /// <summary>Creates a successful tool result.</summary>
    /// <param name="data">Structured model-facing data.</param>
    /// <param name="evidence">Exact calculated evidence.</param>
    /// <returns>A successful tool execution result.</returns>
    public static AIToolExecutionResult Success(object data, AIToolEvidence evidence)
    {
        ArgumentNullException.ThrowIfNull(data);
        ArgumentNullException.ThrowIfNull(evidence);
        return new AIToolExecutionResult { IsSuccess = true, Data = data, Evidence = evidence };
    }

    /// <summary>Creates a failed tool result without evidence.</summary>
    /// <param name="errorMessage">Safe model-facing failure explanation.</param>
    /// <returns>A failed tool execution result.</returns>
    public static AIToolExecutionResult Failure(string errorMessage)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(errorMessage);
        return new AIToolExecutionResult { IsSuccess = false, ErrorMessage = errorMessage.Trim() };
    }
}
