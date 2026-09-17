using System.Text.Json.Serialization;

namespace AskARabbiLIB.Grounding;

internal sealed record GroundedClaimDraft
{
    [JsonPropertyName("kind")]
    public GroundedClaimKind Kind { get; init; } = GroundedClaimKind.Source;

    [JsonPropertyName("text")]
    public required string Text { get; init; }

    [JsonPropertyName("evidenceIds")]
    public required IReadOnlyList<string> EvidenceIds { get; init; }

    [JsonPropertyName("attribution")]
    public string? Attribution { get; init; }

    [JsonPropertyName("quotations")]
    public required IReadOnlyList<GroundedQuotationDraft> Quotations { get; init; }
}
