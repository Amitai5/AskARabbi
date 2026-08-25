using AskARabbiLIB.AI;

namespace AskARabbiLIB.Grounding;

/// <summary>Reports non-persistent timing, volume, usage, and validation diagnostics.</summary>
public sealed record GroundedAnswerTrace(TimeSpan RetrievalLatency, TimeSpan ModelLatency, int CandidateCount, int EvidenceCount, int EvidenceCharacterCount, AIUsage? Usage, GroundedValidationStatus ValidationStatus, bool RepairAttempted, string? ResponseId, string Model);
