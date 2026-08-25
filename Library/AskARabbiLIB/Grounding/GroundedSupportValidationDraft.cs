using System.Text.Json.Serialization;

namespace AskARabbiLIB.Grounding;

internal sealed record GroundedSupportValidationDraft
{
    [JsonPropertyName("evaluations")]
    public required IReadOnlyList<GroundedSupportEvaluationDraft> Evaluations { get; init; }
}
