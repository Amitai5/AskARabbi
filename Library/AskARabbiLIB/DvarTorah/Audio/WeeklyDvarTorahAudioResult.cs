namespace AskARabbiLIB.DvarTorah.Audio;

/// <summary>Describes a non-destructive narration generation attempt.</summary>
public sealed record WeeklyDvarTorahAudioResult(WeeklyDvarTorahAudioStatus Status, WeeklyDvarTorahAudioMetadata? Audio);
