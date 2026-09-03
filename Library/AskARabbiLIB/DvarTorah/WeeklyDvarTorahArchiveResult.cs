namespace AskARabbiLIB.DvarTorah;

/// <summary>Contains one page of published Dvar Torah metadata and the total matching count.</summary>
/// <param name="Items">Metadata returned for the requested page.</param>
/// <param name="TotalCount">Total number of matching past publications.</param>
public sealed record WeeklyDvarTorahArchiveResult(IReadOnlyList<WeeklyDvarTorahArchiveItem> Items, long TotalCount);
