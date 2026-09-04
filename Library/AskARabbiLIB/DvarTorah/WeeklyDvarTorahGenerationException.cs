using AskARabbiLIB.AI;

namespace AskARabbiLIB.DvarTorah;

/// <summary>Reports a stable, non-sensitive stage code when weekly content generation fails closed.</summary>
public sealed class WeeklyDvarTorahGenerationException : InvalidOperationException
{
    internal WeeklyDvarTorahGenerationException(string failureCode, string message, string? diagnosticCategory = null, IReadOnlyList<string>? failedChecks = null, AIResponseDiagnostics? providerDiagnostics = null) : base(message)
    {
        FailureCode = failureCode;
        DiagnosticCategory = diagnosticCategory;
        FailedChecks = failedChecks?.ToArray() ?? [];
        ProviderDiagnostics = providerDiagnostics;
    }

    /// <summary>Gets the bounded stage code safe for logs and persisted failure state.</summary>
    public string FailureCode { get; }

    /// <summary>Gets an optional fixed-category diagnostic that contains no model or source content.</summary>
    public string? DiagnosticCategory { get; }

    /// <summary>Gets fixed review check names without article text or model-generated concerns.</summary>
    public IReadOnlyList<string> FailedChecks { get; }

    /// <summary>Gets response identifiers, status, and usage without prompts or completion text.</summary>
    public AIResponseDiagnostics? ProviderDiagnostics { get; }
}
