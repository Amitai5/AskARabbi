namespace AskARabbiLIB.Retrieval;

/// <summary>Contains one generated Azure upload artifact and its trusted file attributes.</summary>
/// <param name="FileName">Deterministic provider file name.</param>
/// <param name="Content">UTF-8 search document content.</param>
/// <param name="Attributes">File-level provenance and filtering attributes.</param>
/// <param name="SourceSegmentCount">Original canonical segment count.</param>
/// <param name="SearchRecordCount">Full-segment or explicit-excerpt record count.</param>
public sealed record AzureOpenAIVectorStoreCorpusDocument(string FileName, byte[] Content, IReadOnlyDictionary<string, string> Attributes, int SourceSegmentCount, int SearchRecordCount);
