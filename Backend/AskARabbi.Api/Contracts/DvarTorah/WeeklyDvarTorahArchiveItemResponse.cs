namespace AskARabbi.Api.Contracts.DvarTorah;

/// <summary>Contains display metadata for one past weekly Dvar Torah.</summary>
public sealed record WeeklyDvarTorahArchiveItemResponse(WeeklyDvarTorahWeekResponse Week, string Title, IReadOnlyList<string> Tags, DateTimeOffset PublishedAtUtc);
