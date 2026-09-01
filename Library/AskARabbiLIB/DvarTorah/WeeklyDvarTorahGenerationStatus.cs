namespace AskARabbiLIB.DvarTorah;

/// <summary>Describes the outcome of one idempotent scheduled generation invocation.</summary>
public enum WeeklyDvarTorahGenerationStatus
{
    /// <summary>A new article was generated and published.</summary>
    Published,

    /// <summary>The week already had a published article.</summary>
    AlreadyPublished,

    /// <summary>Another invocation currently owns the generation lease.</summary>
    GenerationInProgress,
}
