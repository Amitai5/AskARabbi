using System.Text.Json.Serialization;

namespace AskARabbiLIB.Grounding;

internal sealed record GroundedAnswerDraft
{
    [JsonPropertyName("conversationTitle")]
    public string? ConversationTitle { get; init; }

    [JsonPropertyName("claims")]
    public required IReadOnlyList<GroundedClaimDraft> Claims { get; init; }

    [JsonPropertyName("disagreements")]
    public required IReadOnlyList<GroundedSourcedStatementDraft> Disagreements { get; init; }

    [JsonPropertyName("limitations")]
    public required IReadOnlyList<string> Limitations { get; init; }

    [JsonPropertyName("clarifyingQuestion")]
    public string? ClarifyingQuestion { get; init; }

    [JsonPropertyName("humanGuidanceRecommended")]
    public required bool HumanGuidanceRecommended { get; init; }
}
