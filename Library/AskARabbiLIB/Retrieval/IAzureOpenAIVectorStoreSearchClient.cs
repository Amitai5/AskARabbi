namespace AskARabbiLIB.Retrieval;

/// <summary>Provides the narrow Azure OpenAI vector-store and Responses file-search operations required at runtime.</summary>
public interface IAzureOpenAIVectorStoreSearchClient
{
    /// <summary>Loads one vector store and its corpus metadata.</summary>
    /// <param name="vectorStoreId">Provider vector-store identifier.</param>
    /// <param name="cancellationToken">Token used to cancel the request.</param>
    /// <returns>Current vector-store status.</returns>
    Task<AzureOpenAIVectorStoreInfo> GetAsync(string vectorStoreId, CancellationToken cancellationToken = default);

    /// <summary>Uses a forced Responses file-search call and returns only its scored source chunks.</summary>
    /// <param name="vectorStoreId">Provider vector-store identifier.</param>
    /// <param name="request">Bounded search request.</param>
    /// <param name="cancellationToken">Token used to cancel the request.</param>
    /// <returns>Scored chunks and trusted file attributes.</returns>
    Task<AzureOpenAIVectorStoreSearchPage> SearchAsync(string vectorStoreId, AzureOpenAIVectorStoreSearchRequest request, CancellationToken cancellationToken = default);
}
