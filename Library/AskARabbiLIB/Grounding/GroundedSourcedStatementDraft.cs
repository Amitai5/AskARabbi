using System.Text.Json.Serialization;

namespace AskARabbiLIB.Grounding;

internal sealed record GroundedSourcedStatementDraft
{
    [JsonPropertyName("text")]
    public required string Text { get; init; }

    [JsonPropertyName("evidenceIds")]
    public required IReadOnlyList<string> EvidenceIds { get; init; }

    [JsonPropertyName("attribution")]
    public string? Attribution { get; init; }

    [JsonPropertyName("quotations")]
    public required IReadOnlyList<GroundedQuotationDraft> Quotations { get; init; }
}
