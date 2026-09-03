namespace AskARabbiLIB.DvarTorah;

/// <summary>Contains the display metadata for one published past Dvar Torah.</summary>
/// <param name="Week">Reading week represented by the publication.</param>
/// <param name="Title">Display title.</param>
/// <param name="Tags">Up to three searchable topic tags.</param>
/// <param name="PublishedAtUtc">UTC time at which the article was published.</param>
public sealed record WeeklyDvarTorahArchiveItem(WeeklyDvarTorahWeek Week, string Title, IReadOnlyList<string> Tags, DateTimeOffset PublishedAtUtc);
