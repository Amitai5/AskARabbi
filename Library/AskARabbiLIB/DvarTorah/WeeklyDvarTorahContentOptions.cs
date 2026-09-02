namespace AskARabbiLIB.DvarTorah;

/// <summary>Bounds weekly research, evidence, drafting, and validation.</summary>
public sealed record WeeklyDvarTorahContentOptions
{
    /// <summary>Gets the lookback window for current-events research.</summary>
    public int ResearchWindowDays { get; init; } = 7;

    /// <summary>Gets the maximum news candidates supplied to research selection.</summary>
    public int MaximumNewsCandidates { get; init; } = 80;

    /// <summary>Gets the minimum independent publishers required for the selected current event.</summary>
    public int MinimumNewsPublishers { get; init; } = 2;

    /// <summary>Gets the maximum news sources retained for one article.</summary>
    public int MaximumNewsSources { get; init; } = 4;

    /// <summary>Gets the minimum Torah passages required before drafting.</summary>
    public int MinimumTorahEvidenceItems { get; init; } = 8;

    /// <summary>Gets the maximum Torah passages supplied to drafting.</summary>
    public int MaximumTorahEvidenceItems { get; init; } = 14;

    /// <summary>Gets the minimum percentage of substantive source claims that must be Torah teachings.</summary>
    public int MinimumTorahGroundingPercent { get; init; } = 80;

    /// <summary>Gets the minimum article body size required for a substantial teaching.</summary>
    public int MinimumBodyCharacters { get; init; } = 2_500;

    /// <summary>Gets the maximum article body size allowed by generation validation.</summary>
    public int MaximumBodyCharacters { get; init; } = 15_000;

    /// <summary>Gets the maximum end-to-end content-research duration.</summary>
    public TimeSpan OverallTimeout { get; init; } = TimeSpan.FromMinutes(25);

    /// <summary>Gets the auditable generator and prompt contract version.</summary>
    public string GeneratorVersion { get; init; } = "weekly-dvar-torah-v2";

    /// <summary>Validates research and generation bounds.</summary>
    public void Validate()
    {
        if (ResearchWindowDays is < 1 or > 14)
        {
            throw new ArgumentOutOfRangeException(nameof(ResearchWindowDays), "The research window must be between one and fourteen days.");
        }
        if (MaximumNewsCandidates is < 10 or > 200)
        {
            throw new ArgumentOutOfRangeException(nameof(MaximumNewsCandidates), "Maximum news candidates must be between ten and two hundred.");
        }
        if (MinimumNewsPublishers is < 2 or > 4 || MaximumNewsSources < MinimumNewsPublishers || MaximumNewsSources > 8)
        {
            throw new ArgumentOutOfRangeException(nameof(MaximumNewsSources), "News-source bounds must require two to four publishers and allow no more than eight sources.");
        }
        if (MinimumTorahEvidenceItems is < 4 or > 20 || MaximumTorahEvidenceItems < MinimumTorahEvidenceItems || MaximumTorahEvidenceItems > 30)
        {
            throw new ArgumentOutOfRangeException(nameof(MaximumTorahEvidenceItems), "Torah evidence bounds must require at least four and allow no more than thirty passages.");
        }
        if (MinimumTorahGroundingPercent is < 80 or > 100)
        {
            throw new ArgumentOutOfRangeException(nameof(MinimumTorahGroundingPercent), "Torah grounding must remain between eighty and one hundred percent.");
        }
        if (MinimumBodyCharacters is < 1_000 or > 10_000 || MaximumBodyCharacters < MinimumBodyCharacters || MaximumBodyCharacters > WeeklyDvarTorahDraft.MaximumBodyCharacters)
        {
            throw new ArgumentOutOfRangeException(nameof(MaximumBodyCharacters), "Article body bounds are invalid.");
        }
        if (OverallTimeout < TimeSpan.FromMinutes(1) || OverallTimeout > TimeSpan.FromMinutes(30))
        {
            throw new ArgumentOutOfRangeException(nameof(OverallTimeout), "Overall generation timeout must be between one and thirty minutes.");
        }
        if (string.IsNullOrWhiteSpace(GeneratorVersion) || GeneratorVersion.Trim().Length > 120)
        {
            throw new ArgumentException("A generator version of at most one hundred twenty characters is required.", nameof(GeneratorVersion));
        }
    }
}
