namespace AskARabbiLIB.DvarTorah;

internal sealed record WeeklyDvarTorahCandidateValidation(IReadOnlyList<string> Errors, int TorahGroundingPercent, IReadOnlyList<string> UsedEvidenceIds)
{
    internal bool IsValid => Errors.Count == 0;
}
