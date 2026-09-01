namespace AskARabbiLIB.DvarTorah;

/// <summary>Contains the outcome of one scheduled weekly generation invocation.</summary>
/// <param name="Status">Idempotent generation outcome.</param>
/// <param name="Week">Reading week targeted by the invocation.</param>
/// <param name="Article">Existing or newly published article, when available.</param>
public sealed record WeeklyDvarTorahGenerationResult(WeeklyDvarTorahGenerationStatus Status, WeeklyDvarTorahWeek Week, WeeklyDvarTorahArticle? Article);
