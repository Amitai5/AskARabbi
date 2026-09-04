namespace AskARabbiLIB.DvarTorah.Audio;

/// <summary>Identifies exclusive ownership of a narration version, separate from the text publication lease.</summary>
public sealed record WeeklyDvarTorahAudioLease(string WeekKey, string Version, string LeaseId, DateTimeOffset ExpiresAtUtc);
