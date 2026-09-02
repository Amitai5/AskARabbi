namespace AskARabbiLIB.DvarTorah;

/// <summary>Reports a stable, non-sensitive stage code when weekly content generation fails closed.</summary>
public sealed class WeeklyDvarTorahGenerationException : InvalidOperationException
{
    internal WeeklyDvarTorahGenerationException(string failureCode, string message, string? diagnosticCategory = null) : base(message)
    {
        FailureCode = failureCode;
        DiagnosticCategory = diagnosticCategory;
    }

    /// <summary>Gets the bounded stage code safe for logs and persisted failure state.</summary>
    public string FailureCode { get; }

    /// <summary>Gets an optional fixed-category diagnostic that contains no model or source content.</summary>
    public string? DiagnosticCategory { get; }
}
