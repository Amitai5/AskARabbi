namespace AskARabbi.Api.Contracts.DvarTorah;

/// <summary>Provides the calendar identity of one weekly Dvar Torah.</summary>
public sealed record WeeklyDvarTorahWeekResponse(string WeekKey, DateOnly ShabbatDate, string HebrewDate, string? Parashah, string? Holiday, bool InIsrael);
