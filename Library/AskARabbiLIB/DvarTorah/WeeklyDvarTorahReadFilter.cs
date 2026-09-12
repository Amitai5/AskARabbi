namespace AskARabbiLIB.DvarTorah;

/// <summary>Restricts an archive query to read or unread teachings before pagination.</summary>
/// <param name="ReadWeekKeys">Account-owned keys of completed teachings.</param>
/// <param name="IsRead">Whether to include read rather than unread teachings.</param>
public sealed record WeeklyDvarTorahReadFilter(IReadOnlyList<string> ReadWeekKeys, bool IsRead);
