namespace AskARabbiLIB.Retrieval;

/// <summary>Contains one bounded set of Responses file-search results.</summary>
/// <param name="Results">Scored file chunks.</param>
/// <param name="HasMore">Whether Azure reports additional results.</param>
public sealed record AzureOpenAIVectorStoreSearchPage(IReadOnlyList<AzureOpenAIVectorStoreSearchResult> Results, bool HasMore);
