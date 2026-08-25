using System.Text.Json.Serialization;

namespace AskARabbiLIB.Grounding;

internal sealed record GroundedQuotationDraft
{
    [JsonPropertyName("evidenceId")]
    public required string EvidenceId { get; init; }

    [JsonPropertyName("text")]
    public required string Text { get; init; }

    [JsonPropertyName("role")]
    public required string Role { get; init; }
}
