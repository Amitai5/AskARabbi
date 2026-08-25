namespace AskARabbiLIB.Retrieval;

/// <summary>Describes a built segment index and its corpus identity.</summary>
/// <param name="SchemaVersion">Segment-index schema version.</param>
/// <param name="CorpusFingerprint">Deterministic corpus fingerprint.</param>
/// <param name="DocumentCount">Number of indexed documents.</param>
/// <param name="SegmentCount">Number of indexed segments.</param>
/// <param name="FileSizeBytes">SQLite file size, or zero for in-memory indexes.</param>
public sealed record SourceIndexStatistics(string SchemaVersion, string CorpusFingerprint, int DocumentCount, long SegmentCount, long FileSizeBytes);
