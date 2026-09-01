namespace AskARabbiLIB.DvarTorah;

/// <summary>Contains the current reading week and its current or fallback publication.</summary>
/// <param name="CurrentWeek">Reading week requested by the application.</param>
/// <param name="Article">Current or most recent earlier publication, when one exists.</param>
/// <param name="IsCurrentWeek">Whether the publication belongs to <paramref name="CurrentWeek"/>.</param>
public sealed record WeeklyDvarTorahPublication(WeeklyDvarTorahWeek CurrentWeek, WeeklyDvarTorahArticle? Article, bool IsCurrentWeek);
