namespace AskARabbi.Api.Contracts.DvarTorah;

/// <summary>Provides the current reading week and its current or latest available publication.</summary>
public sealed record WeeklyDvarTorahResponse(WeeklyDvarTorahWeekResponse CurrentWeek, WeeklyDvarTorahArticleResponse? DvarTorah, bool IsCurrentWeek);
