namespace AskARabbiLIB.DvarTorah;

internal static class WeeklyDvarTorahReviewValidator
{
    internal static IReadOnlyList<string> Validate(WeeklyDvarTorahReviewDraft review, ICollection<string>? failureCodes = null, IReadOnlyList<string>? allowedEvidenceIds = null)
    {
        ArgumentNullException.ThrowIfNull(review);
        var errors = new List<string>();
        AddFailure(review.AllClaimsSupported, "Independent review found an unsupported claim.", errors, failureCodes, nameof(review.AllClaimsSupported));
        AddFailure(review.TorahInterpretationResponsible, "Independent review found an irresponsible Torah interpretation.", errors, failureCodes, nameof(review.TorahInterpretationResponsible));
        AddFailure(review.TorahRemainsCentral, "Independent review found that Torah was not the central subject.", errors, failureCodes, nameof(review.TorahRemainsCentral));
        AddFailure(review.CurrentEventsNeutral, "Independent review found politically slanted or unfair current-events framing.", errors, failureCodes, nameof(review.CurrentEventsNeutral));
        AddFailure(review.NewsSourcesDescribeSameEvent, "Independent review found that the news sources did not corroborate the same event.", errors, failureCodes, nameof(review.NewsSourcesDescribeSameEvent));
        AddFailure(review.CurrentEventHasUsImpact, "Independent review found no meaningful United States impact.", errors, failureCodes, nameof(review.CurrentEventHasUsImpact));
        AddFailure(review.DeepMoralTeachingPresent, "Independent review found no sufficiently deep moral teaching.", errors, failureCodes, nameof(review.DeepMoralTeachingPresent));
        AddFailure(review.StoryContextClear, "Independent review found insufficient story context for a reader unfamiliar with the Torah portion.", errors, failureCodes, nameof(review.StoryContextClear));
        AddFailure(review.ArgumentHasBeginningMiddleEnd, "Independent review found no coherent beginning, middle, and end developing one central insight.", errors, failureCodes, nameof(review.ArgumentHasBeginningMiddleEnd));
        AddFailure(review.ConclusionReturnsToOpening, "Independent review found no conclusion returning to the opening question or image.", errors, failureCodes, nameof(review.ConclusionReturnsToOpening));
        AddFailure(review.DoesNotEncourageViolence, "Independent review found encouragement, instruction, or approval of violence.", errors, failureCodes, nameof(review.DoesNotEncourageViolence));
        AddFailure(review.DoesNotGlorifyOrGraphicallyDescribeViolence, "Independent review found glorified or graphic violence.", errors, failureCodes, nameof(review.DoesNotGlorifyOrGraphicallyDescribeViolence));
        AddFailure(review.DoesNotContainHateOrDehumanization, "Independent review found hateful or dehumanizing language.", errors, failureCodes, nameof(review.DoesNotContainHateOrDehumanization));
        AddFailure(review.DoesNotContainRacism, "Independent review found racist content.", errors, failureCodes, nameof(review.DoesNotContainRacism));
        AddFailure(review.DoesNotContainSexism, "Independent review found sexist content.", errors, failureCodes, nameof(review.DoesNotContainSexism));
        AddFailure(review.DoesNotTargetProtectedGroups, "Independent review found harmful targeting of a protected or minority group.", errors, failureCodes, nameof(review.DoesNotTargetProtectedGroups));
        AddFailure(review.DoesNotScapegoatOrAlienateGroups, "Independent review found scapegoating, isolation, or alienation of a group.", errors, failureCodes, nameof(review.DoesNotScapegoatOrAlienateGroups));
        AddFailure(review.DoesNotUsePartisanPersuasion, "Independent review found partisan persuasion.", errors, failureCodes, nameof(review.DoesNotUsePartisanPersuasion));
        AddFailure(review.DoesNotExploitSuffering, "Independent review found exploitative treatment of suffering.", errors, failureCodes, nameof(review.DoesNotExploitSuffering));
        AddFailure(review.DoesNotClaimDivinePunishment, "Independent review found an unsupported claim that suffering was divine punishment.", errors, failureCodes, nameof(review.DoesNotClaimDivinePunishment));
        AddFailure(review.RespectfulAndInclusive, "Independent review found the draft disrespectful or exclusionary.", errors, failureCodes, nameof(review.RespectfulAndInclusive));
        AddFailure(review.SafeToPublish, "Independent review did not approve publication.", errors, failureCodes, nameof(review.SafeToPublish));
        var allowedIds = (allowedEvidenceIds ?? []).ToHashSet(StringComparer.Ordinal);
        if (review.Concerns is null || review.Concerns.Any(concern => !IsValidConcern(concern, allowedIds)))
        {
            errors.Add("Independent review returned invalid concern metadata.");
            failureCodes?.Add("InvalidConcerns");
        }
        else if (review.Concerns.Count > 0)
        {
            failureCodes?.Add("Concerns");
            foreach (var concern in review.Concerns)
            {
                var location = concern.ParagraphIndex == 0 ? "overall article or metadata" : $"paragraph {concern.ParagraphIndex}";
                var evidence = concern.EvidenceIds.Count == 0 ? "no specific source" : string.Join(", ", concern.EvidenceIds);
                errors.Add($"Independent review check {concern.Check} failed at {location}; recheck against {evidence}.");
            }
        }

        return errors;
    }

    private static void AddFailure(bool passed, string message, ICollection<string> errors, ICollection<string>? failureCodes, string code)
    {
        if (!passed)
        {
            errors.Add(message);
            failureCodes?.Add(code);
        }
    }

    private static bool IsValidConcern(WeeklyDvarTorahReviewConcern? concern, IReadOnlySet<string> allowedIds) => concern is not null && Enum.IsDefined(concern.Check) && concern.ParagraphIndex is >= 0 and <= 1_000 && concern.EvidenceIds is not null && concern.EvidenceIds.Count <= 40 && concern.EvidenceIds.All(id => id is not null && allowedIds.Contains(id));
}
