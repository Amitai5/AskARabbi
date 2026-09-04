namespace AskARabbiLIB.DvarTorah.Audio;

/// <summary>Versioned browser-safe narration timings tied to the exact displayed article.</summary>
public sealed record DvarTorahAudioTimings
{
    public int SchemaVersion { get; init; } = 1;
    public required string Version { get; init; }
    public required string Voice { get; init; }
    public required string Title { get; init; }
    public required string Body { get; init; }
    public required double DurationMs { get; init; }
    public string TextOffsetUnit { get; init; } = "UTF-16 code units";
    public required IReadOnlyList<DvarTorahAudioWord> Words { get; init; }
}
