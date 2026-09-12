namespace AskARabbi.Api.Contracts.DvarTorah;

/// <summary>Account-owned teaching keys explicitly marked as read.</summary>
public sealed record WeeklyDvarTorahReadStateResponse(IReadOnlyList<string> ReadWeekKeys);
