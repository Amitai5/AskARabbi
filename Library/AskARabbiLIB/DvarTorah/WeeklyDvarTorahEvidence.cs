namespace AskARabbiLIB.DvarTorah;

internal sealed record WeeklyDvarTorahEvidence(string EvidenceId, WeeklyDvarTorahSourceKind Kind, string Title, string Publisher, string SourceUrl, string PresentedText, DateTimeOffset RetrievedAtUtc, string? CanonicalReference, DateTimeOffset? PublishedAtUtc, string? License)
{
    internal WeeklyDvarTorahSource ToSource() => new(EvidenceId, Kind, Title, Publisher, SourceUrl, PresentedText, RetrievedAtUtc, CanonicalReference, PublishedAtUtc, License);
}
