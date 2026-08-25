namespace AskARabbiLIB.Grounding;

/// <summary>Represents a grounded answer success or fail-closed outcome.</summary>
public sealed record GroundedAnswerResult
{
    public required GroundedAnswerStatus Status { get; init; }

    public GroundedAnswer? Answer { get; init; }

    public EvidencePacket? Evidence { get; init; }

    public required GroundedAnswerTrace Trace { get; init; }

    public string? ErrorMessage { get; init; }

    /// <summary>Gets whether a validated grounded answer is available.</summary>
    public bool IsSuccess => Status == GroundedAnswerStatus.Success;
}
