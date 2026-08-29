namespace AskARabbiLIB.Retrieval;

/// <summary>Contains one scored Azure vector-store result and its file-level provenance.</summary>
/// <param name="FileId">Provider file identifier.</param>
/// <param name="FileName">Provider file name.</param>
/// <param name="Score">Provider relevance score from zero to one.</param>
/// <param name="Attributes">Trusted file attributes supplied during publication.</param>
/// <param name="Content">Retrieved text chunks.</param>
public sealed record AzureOpenAIVectorStoreSearchResult(string FileId, string FileName, double Score, IReadOnlyDictionary<string, string> Attributes, IReadOnlyList<string> Content);
