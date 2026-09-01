using AskARabbiLIB.DvarTorah;

namespace AskARabbi.Api.Contracts.DvarTorah;

/// <summary>Provides one inspectable source used by a weekly Dvar Torah.</summary>
public sealed record WeeklyDvarTorahSourceResponse(string SourceId, WeeklyDvarTorahSourceKind Kind, string Title, string Publisher, string SourceUrl, string Excerpt, DateTimeOffset RetrievedAtUtc, string? CanonicalReference, DateTimeOffset? PublishedAtUtc, string? License);
