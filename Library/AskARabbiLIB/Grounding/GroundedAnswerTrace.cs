using AskARabbiLIB.AI;

namespace AskARabbiLIB.Grounding;

/// <summary>Reports non-persistent timing, volume, usage, and validation diagnostics.</summary>
public sealed record GroundedAnswerTrace(TimeSpan RetrievalLatency, TimeSpan ModelLatency, int CandidateCount, int EvidenceCount, int EvidenceCharacterCount, AIUsage? Usage, GroundedValidationStatus ValidationStatus, bool RepairAttempted, string? ResponseId, string Model)
{
    /// <summary>Gets the final typed status from the model stage that determined the result.</summary>
    public AIEngineStatus ProviderStatus { get; init; } = AIEngineStatus.Success;

    /// <summary>Gets a safe provider completion or failure category when available.</summary>
    public string? CompletionReason { get; init; }

    /// <summary>Gets the combined number of provider attempts across drafting, audit, and repair.</summary>
    public int ProviderAttempts { get; init; }
}
