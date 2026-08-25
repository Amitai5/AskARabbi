using AskARabbiLIB.AI;

namespace AskARabbiLIB.Grounding;

internal sealed record ClaimEvidenceValidationResult(ClaimEvidenceValidationStatus Status, string? ErrorMessage, AIEngineStatus? EngineStatus, AIResponseDiagnostics? Diagnostics)
{
    internal static ClaimEvidenceValidationResult Supported(AIResponseDiagnostics? diagnostics = null) => new(ClaimEvidenceValidationStatus.Supported, null, null, diagnostics);

    internal static ClaimEvidenceValidationResult Unsupported(string errorMessage, AIResponseDiagnostics? diagnostics = null) => new(ClaimEvidenceValidationStatus.Unsupported, errorMessage, null, diagnostics);

    internal static ClaimEvidenceValidationResult ProviderFailure(AIEngineStatus engineStatus, string errorMessage, AIResponseDiagnostics diagnostics) => new(ClaimEvidenceValidationStatus.ProviderFailure, errorMessage, engineStatus, diagnostics);
}
