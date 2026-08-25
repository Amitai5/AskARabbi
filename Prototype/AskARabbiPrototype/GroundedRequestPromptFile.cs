namespace AskARabbiPrototype;

internal sealed record GroundedRequestPromptFile
{
    public required string Instruction { get; init; }

    public required string EvidenceStartMarker { get; init; }

    public required string EvidenceEndMarker { get; init; }
}
