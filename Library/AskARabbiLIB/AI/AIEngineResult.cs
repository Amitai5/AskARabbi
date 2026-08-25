namespace AskARabbiLIB.AI;

/// <summary>Represents a typed AI success or failure without nullable response semantics.</summary>
/// <typeparam name="T">Structured response type.</typeparam>
public sealed record AIEngineResult<T>
{
    /// <summary>Gets the explicit request outcome.</summary>
    public required AIEngineStatus Status { get; init; }

    /// <summary>Gets the structured response for a successful result.</summary>
    public T? Value { get; init; }

    /// <summary>Gets the safe failure message for an unsuccessful result.</summary>
    public string? ErrorMessage { get; init; }

    /// <summary>Gets diagnostics for every result.</summary>
    public required AIResponseDiagnostics Diagnostics { get; init; }

    /// <summary>Gets whether the provider returned a successful result.</summary>
    public bool IsSuccess => Status == AIEngineStatus.Success;

    /// <summary>Creates a successful typed result.</summary>
    /// <param name="value">Validated structured value.</param>
    /// <param name="diagnostics">Provider diagnostics.</param>
    /// <returns>A successful result.</returns>
    public static AIEngineResult<T> Success(T value, AIResponseDiagnostics diagnostics)
    {
        ArgumentNullException.ThrowIfNull(value);
        ArgumentNullException.ThrowIfNull(diagnostics);
        return new AIEngineResult<T> { Status = AIEngineStatus.Success, Value = value, Diagnostics = diagnostics };
    }

    /// <summary>Creates a typed failure result.</summary>
    /// <param name="status">Non-success failure status.</param>
    /// <param name="message">Safe diagnostic message.</param>
    /// <param name="diagnostics">Provider diagnostics available for the failed call.</param>
    /// <returns>A failed result.</returns>
    public static AIEngineResult<T> Failure(AIEngineStatus status, string message, AIResponseDiagnostics diagnostics)
    {
        if (status == AIEngineStatus.Success)
        {
            throw new ArgumentException("A failure result cannot use the success status.", nameof(status));
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(message);
        ArgumentNullException.ThrowIfNull(diagnostics);
        return new AIEngineResult<T> { Status = status, ErrorMessage = message, Diagnostics = diagnostics };
    }
}
