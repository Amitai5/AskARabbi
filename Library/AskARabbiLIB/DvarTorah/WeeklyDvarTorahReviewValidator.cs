namespace AskARabbiLIB.DvarTorah;

internal static class WeeklyDvarTorahReviewValidator
{
    internal static IReadOnlyList<string> Validate(WeeklyDvarTorahReviewDraft review)
    {
        ArgumentNullException.ThrowIfNull(review);
        var errors = new List<string>();
        AddFailure(review.AllClaimsSupported, "Independent review found an unsupported claim.", errors);
        AddFailure(review.TorahInterpretationResponsible, "Independent review found an irresponsible Torah interpretation.", errors);
        AddFailure(review.TorahRemainsCentral, "Independent review found that Torah was not the central subject.", errors);
        AddFailure(review.CurrentEventsNeutral, "Independent review found politically slanted or unfair current-events framing.", errors);
        AddFailure(review.NewsSourcesDescribeSameEvent, "Independent review found that the news sources did not corroborate the same event.", errors);
        AddFailure(review.CurrentEventHasUsImpact, "Independent review found no meaningful United States impact.", errors);
        AddFailure(review.DeepMoralTeachingPresent, "Independent review found no sufficiently deep moral teaching.", errors);
        AddFailure(review.DoesNotEncourageViolence, "Independent review found encouragement, instruction, or approval of violence.", errors);
        AddFailure(review.DoesNotGlorifyOrGraphicallyDescribeViolence, "Independent review found glorified or graphic violence.", errors);
        AddFailure(review.DoesNotContainHateOrDehumanization, "Independent review found hateful or dehumanizing language.", errors);
        AddFailure(review.DoesNotContainRacism, "Independent review found racist content.", errors);
        AddFailure(review.DoesNotContainSexism, "Independent review found sexist content.", errors);
        AddFailure(review.DoesNotTargetProtectedGroups, "Independent review found harmful targeting of a protected or minority group.", errors);
        AddFailure(review.DoesNotScapegoatOrAlienateGroups, "Independent review found scapegoating, isolation, or alienation of a group.", errors);
        AddFailure(review.DoesNotUsePartisanPersuasion, "Independent review found partisan persuasion.", errors);
        AddFailure(review.DoesNotExploitSuffering, "Independent review found exploitative treatment of suffering.", errors);
        AddFailure(review.DoesNotClaimDivinePunishment, "Independent review found an unsupported claim that suffering was divine punishment.", errors);
        AddFailure(review.RespectfulAndInclusive, "Independent review found the draft disrespectful or exclusionary.", errors);
        AddFailure(review.SafeToPublish, "Independent review did not approve publication.", errors);
        if (review.Concerns is null || review.Concerns.Any(string.IsNullOrWhiteSpace))
        {
            errors.Add("Independent review returned invalid concern metadata.");
        }
        else if (review.Concerns.Count > 0)
        {
            errors.Add($"Independent review concerns: {string.Join("; ", review.Concerns.Select(concern => Bound(concern, 300)))}");
        }

        return errors;
    }

    private static void AddFailure(bool passed, string message, ICollection<string> errors)
    {
        if (!passed)
        {
            errors.Add(message);
        }
    }

    private static string Bound(string value, int maximumCharacters) => value.Length <= maximumCharacters ? value : value[..maximumCharacters].TrimEnd();
}
