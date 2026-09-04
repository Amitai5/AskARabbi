namespace AskARabbiLIB.DvarTorah.Audio;

/// <summary>Identifies a completed immutable private MP3 and its timing manifest. URIs never contain SAS tokens.</summary>
public sealed record WeeklyDvarTorahAudioMetadata
{
    public required string Version { get; init; }
    public required string Voice { get; init; }
    public required double DurationMs { get; init; }
    public required string BlobName { get; init; }
    public required string BlobUri { get; init; }
    public required string TimingsBlobName { get; init; }
    public required long AudioLength { get; init; }
    public required DateTimeOffset CreatedAtUtc { get; init; }
}
