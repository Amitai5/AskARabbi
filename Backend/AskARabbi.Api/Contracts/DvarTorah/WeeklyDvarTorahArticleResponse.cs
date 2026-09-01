namespace AskARabbi.Api.Contracts.DvarTorah;

/// <summary>Provides one immutable published weekly Dvar Torah.</summary>
public sealed record WeeklyDvarTorahArticleResponse(WeeklyDvarTorahWeekResponse Week, string Title, string Body, DateTimeOffset GeneratedAtUtc, DateTimeOffset PublishedAtUtc);
