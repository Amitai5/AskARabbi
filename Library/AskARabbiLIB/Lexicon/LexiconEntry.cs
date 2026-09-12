namespace AskARabbiLIB.Lexicon;

/// <summary>Preserves a source dictionary article independently of its search index.</summary>
public sealed record LexiconEntry
{
    public required string DictionaryId { get; init; }
    public required string Revision { get; init; }
    public required string EntryId { get; init; }
    public required string Headword { get; init; }
    public required string Text { get; init; }
    public required string SourceUrl { get; init; }
    public required string SourceFile { get; init; }
    public required string SourceSha256 { get; init; }
    public required string Edition { get; init; }
    public required string License { get; init; }
    public required string LicenseUrl { get; init; }
    public string DefinitionLanguage { get; init; } = "English";
    public string LanguageScope { get; init; } = "Biblical Hebrew and Biblical Aramaic";
}
