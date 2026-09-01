namespace AskARabbiLIB.AI.Tools;

/// <summary>Represents a successful or failed local tool execution.</summary>
public sealed record AIToolExecutionResult
{
    /// <summary>Gets whether the calculation completed successfully.</summary>
    public required bool IsSuccess { get; init; }

    /// <summary>Gets structured result data safe to expose to the model.</summary>
    public object? Data { get; init; }

    /// <summary>Gets exact calculated evidence to add to the trusted request packet.</summary>
    public AIToolEvidence? Evidence { get; init; }

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
