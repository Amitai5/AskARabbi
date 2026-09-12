using AskARabbiLIB.Lexicon;
using MongoDB.Bson.Serialization.Attributes;

namespace AskARabbiLIB.Persistence.Mongo;

[BsonIgnoreExtraElements]
internal sealed record MongoLexiconEntryDocument
{
    [BsonId]
    public required string Id { get; init; }

    [BsonElement("dictionaryId")]
    public required string DictionaryId { get; init; }

    [BsonElement("revision")]
    public required string Revision { get; init; }

    [BsonElement("lookupKeys")]
    public string[] LookupKeys { get; init; } = [];

    [BsonElement("entry")]
    public LexiconEntry? Entry { get; init; }

    [BsonElement("publishedFingerprint")]
    public string? PublishedFingerprint { get; init; }

    [BsonElement("importFingerprint")]
    public string? ImportFingerprint { get; init; }

    [BsonElement("entryCount")]
    public int EntryCount { get; init; }
}
