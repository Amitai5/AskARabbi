namespace AskARabbiLIB.Retrieval;

/// <summary>Reports progress while a reproducible segment index is built.</summary>
/// <param name="DocumentsCompleted">Number of indexed documents.</param>
/// <param name="DocumentCount">Total number of documents.</param>
/// <param name="SegmentsCompleted">Number of indexed segments.</param>
/// <param name="CurrentTitle">Title of the most recently indexed document.</param>
public sealed record SourceIndexProgress(int DocumentsCompleted, int DocumentCount, long SegmentsCompleted, string CurrentTitle);
