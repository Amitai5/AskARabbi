namespace AskARabbiLIB.Retrieval;

/// <summary>Reports a completed reproducible corpus publication.</summary>
/// <param name="VectorStoreId">New provider vector-store identifier.</param>
/// <param name="CorpusFingerprint">Fingerprint stamped into the store and every file.</param>
/// <param name="DocumentCount">Uploaded document count.</param>
/// <param name="SegmentCount">Original canonical segment count.</param>
/// <param name="SearchRecordCount">Full-segment or explicit-excerpt search record count.</param>
/// <param name="UsageBytes">Billable vector-store usage reported by Azure.</param>
public sealed record AzureOpenAIVectorStorePublication(string VectorStoreId, string CorpusFingerprint, int DocumentCount, long SegmentCount, long SearchRecordCount, long UsageBytes)
{
    /// <summary>Gets the number of bounded provider files used to represent the logical documents.</summary>
    public int FileCount { get; init; }
}
