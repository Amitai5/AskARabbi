using System.Text.Json.Serialization;

namespace AskARabbiLIB.Grounding;

internal sealed record GroundedSupportValidationDraft
{
    [JsonPropertyName("isResponsive")]
    public required bool IsResponsive { get; init; }

    [JsonPropertyName("overallExplanation")]
    public required string OverallExplanation { get; init; }

    [JsonPropertyName("evaluations")]
    public required IReadOnlyList<GroundedSupportEvaluationDraft> Evaluations { get; init; }
}
