namespace AskARabbiLIB.Retrieval;

/// <summary>Reports trusted status and corpus metadata for one Azure OpenAI vector store.</summary>
/// <param name="Id">Provider vector-store identifier.</param>
/// <param name="Name">Provider vector-store name.</param>
/// <param name="Status">Provider processing status.</param>
/// <param name="UsageBytes">Billable vector-store usage reported by Azure.</param>
/// <param name="CompletedFileCount">Successfully indexed file count.</param>
/// <param name="FailedFileCount">Failed file count.</param>
/// <param name="Metadata">Store-level metadata.</param>
public sealed record AzureOpenAIVectorStoreInfo(string Id, string Name, string Status, long UsageBytes, int CompletedFileCount, int FailedFileCount, IReadOnlyDictionary<string, string> Metadata);
