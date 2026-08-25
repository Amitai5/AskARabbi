namespace AskARabbiLIB.Grounding;

/// <summary>Contains the exact bounded evidence supplied to one model request.</summary>
/// <param name="Items">Ordered evidence items.</param>
/// <param name="CharacterCount">Total number of presented evidence characters.</param>
public sealed record EvidencePacket(IReadOnlyList<EvidenceItem> Items, int CharacterCount);
