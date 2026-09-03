namespace AskARabbi.Api.Contracts.DvarTorah;

/// <summary>Contains one searchable page of past weekly Dvar Torah metadata.</summary>
public sealed record WeeklyDvarTorahArchiveResponse(IReadOnlyList<WeeklyDvarTorahArchiveItemResponse> Items, int Page, int PageSize, long TotalCount, long TotalPages);
