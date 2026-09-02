using System.Text.Json.Serialization;

namespace AskARabbiLIB.DvarTorah;

internal sealed record WeeklyDvarTorahResearchDraft
{
    [JsonPropertyName("theme")]
    public required string Theme { get; init; }

    [JsonPropertyName("moralQuestion")]
    public required string MoralQuestion { get; init; }

    [JsonPropertyName("selectedNewsEvidenceIds")]
    public required IReadOnlyList<string> SelectedNewsEvidenceIds { get; init; }

    [JsonPropertyName("torahSearchQueries")]
    public required IReadOnlyList<string> TorahSearchQueries { get; init; }

    [JsonPropertyName("suggestedTags")]
    public required IReadOnlyList<string> SuggestedTags { get; init; }
}

internal sealed record WeeklyDvarTorahArticleDraft
{
    [JsonPropertyName("title")]
    public required string Title { get; init; }

    [JsonPropertyName("body")]
    public required string Body { get; init; }

    [JsonPropertyName("featuredTorahEvidenceIds")]
    public required IReadOnlyList<string> FeaturedTorahEvidenceIds { get; init; }

    [JsonPropertyName("centralTeaching")]
    public required string CentralTeaching { get; init; }

    [JsonPropertyName("tags")]
    public required IReadOnlyList<string> Tags { get; init; }

    [JsonPropertyName("practicalActions")]
    public required IReadOnlyList<string> PracticalActions { get; init; }

    [JsonPropertyName("torahTeachings")]
    public required IReadOnlyList<WeeklyDvarTorahSourcedStatementDraft> TorahTeachings { get; init; }

    [JsonPropertyName("currentEventFacts")]
    public required IReadOnlyList<WeeklyDvarTorahSourcedStatementDraft> CurrentEventFacts { get; init; }

    [JsonPropertyName("connections")]
    public required IReadOnlyList<WeeklyDvarTorahSourcedStatementDraft> Connections { get; init; }
}

internal sealed record WeeklyDvarTorahSourcedStatementDraft
{
    [JsonPropertyName("text")]
    public required string Text { get; init; }

    [JsonPropertyName("evidenceIds")]
    public required IReadOnlyList<string> EvidenceIds { get; init; }
}

internal sealed record WeeklyDvarTorahReviewDraft
{
    [JsonPropertyName("allClaimsSupported")]
    public required bool AllClaimsSupported { get; init; }

    [JsonPropertyName("torahInterpretationResponsible")]
    public required bool TorahInterpretationResponsible { get; init; }

    [JsonPropertyName("torahRemainsCentral")]
    public required bool TorahRemainsCentral { get; init; }

    [JsonPropertyName("currentEventsNeutral")]
    public required bool CurrentEventsNeutral { get; init; }

    [JsonPropertyName("newsSourcesDescribeSameEvent")]
    public required bool NewsSourcesDescribeSameEvent { get; init; }

    [JsonPropertyName("currentEventHasUsImpact")]
    public required bool CurrentEventHasUsImpact { get; init; }

    [JsonPropertyName("deepMoralTeachingPresent")]
    public required bool DeepMoralTeachingPresent { get; init; }

    [JsonPropertyName("doesNotEncourageViolence")]
    public required bool DoesNotEncourageViolence { get; init; }

    [JsonPropertyName("doesNotGlorifyOrGraphicallyDescribeViolence")]
    public required bool DoesNotGlorifyOrGraphicallyDescribeViolence { get; init; }

    [JsonPropertyName("doesNotContainHateOrDehumanization")]
    public required bool DoesNotContainHateOrDehumanization { get; init; }

    [JsonPropertyName("doesNotContainRacism")]
    public required bool DoesNotContainRacism { get; init; }

    [JsonPropertyName("doesNotContainSexism")]
    public required bool DoesNotContainSexism { get; init; }

    [JsonPropertyName("doesNotTargetProtectedGroups")]
    public required bool DoesNotTargetProtectedGroups { get; init; }

    [JsonPropertyName("doesNotScapegoatOrAlienateGroups")]
    public required bool DoesNotScapegoatOrAlienateGroups { get; init; }

    [JsonPropertyName("doesNotUsePartisanPersuasion")]
    public required bool DoesNotUsePartisanPersuasion { get; init; }

    [JsonPropertyName("doesNotExploitSuffering")]
    public required bool DoesNotExploitSuffering { get; init; }

    [JsonPropertyName("doesNotClaimDivinePunishment")]
    public required bool DoesNotClaimDivinePunishment { get; init; }

    [JsonPropertyName("respectfulAndInclusive")]
    public required bool RespectfulAndInclusive { get; init; }

    [JsonPropertyName("safeToPublish")]
    public required bool SafeToPublish { get; init; }

    [JsonPropertyName("concerns")]
    public required IReadOnlyList<string> Concerns { get; init; }
}
