using System.Text.Json.Serialization;

namespace AskARabbiLIB.DvarTorah;

internal sealed record WeeklyDvarTorahReviewConcern
{
    [JsonPropertyName("check")]
    [JsonConverter(typeof(JsonStringEnumConverter<WeeklyDvarTorahReviewCheck>))]
    public required WeeklyDvarTorahReviewCheck Check { get; init; }

    [JsonPropertyName("evidenceIds")]
    public required IReadOnlyList<string> EvidenceIds { get; init; }

    [JsonPropertyName("paragraphIndex")]
    public required int ParagraphIndex { get; init; }
}
