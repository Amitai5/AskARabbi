using System.Text.Json.Serialization;

namespace AskARabbiLIB.Grounding;

internal sealed record GroundedSupportEvaluationDraft
{
    [JsonPropertyName("statementId")]
    public required string StatementId { get; init; }

    [JsonPropertyName("isRelevant")]
    public required bool IsRelevant { get; init; }

    [JsonPropertyName("isSupported")]
    public required bool IsSupported { get; init; }

    [JsonPropertyName("explanation")]
    public required string Explanation { get; init; }
}
