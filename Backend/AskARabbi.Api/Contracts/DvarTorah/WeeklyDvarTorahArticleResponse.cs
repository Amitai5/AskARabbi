namespace AskARabbi.Api.Contracts.DvarTorah;

/// <summary>Provides one immutable published weekly Dvar Torah.</summary>
public sealed record WeeklyDvarTorahArticleResponse(WeeklyDvarTorahWeekResponse Week, string Title, string Body, string? CentralTeaching, IReadOnlyList<string> Tags, IReadOnlyList<WeeklyDvarTorahSourceResponse> Sources, int? TorahGroundingPercent, DateTimeOffset GeneratedAtUtc, DateTimeOffset PublishedAtUtc, WeeklyDvarTorahAudioResponse? Audio = null);
