namespace AskARabbiLIB.Retrieval;

/// <summary>Reports progress while publishing checksum-verified corpus documents.</summary>
/// <param name="Stage">Current publication stage.</param>
/// <param name="CompletedDocuments">Documents completed in the current corpus.</param>
/// <param name="TotalDocuments">Total documents selected for publication.</param>
/// <param name="SearchRecordCount">Search records generated so far.</param>
/// <param name="CurrentTitle">Current document title when available.</param>
public sealed record AzureOpenAIVectorStorePublicationProgress(string Stage, int CompletedDocuments, int TotalDocuments, long SearchRecordCount, string? CurrentTitle)
{
    /// <summary>Gets the number of bounded provider files completed in the current stage.</summary>
    public int CompletedFiles { get; init; }

    /// <summary>Gets the total bounded provider files selected for publication.</summary>
    public int TotalFiles { get; init; }
}
