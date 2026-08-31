using MongoDB.Bson.Serialization.Attributes;

namespace AskARabbiLIB.Persistence.Mongo;

[BsonIgnoreExtraElements]
internal sealed class MongoConversationSourceDocument
{
    [BsonElement("number")]
    public int Number { get; init; }

    [BsonElement("title")]
    public required string Title { get; init; }

    [BsonElement("hebrewTitle")]
    public required string HebrewTitle { get; init; }

    [BsonElement("canonicalReference")]
    public required string CanonicalReference { get; init; }

    [BsonElement("edition")]
    public required string Edition { get; init; }

    [BsonElement("language")]
    public required string Language { get; init; }

    [BsonElement("collection")]
    public required string Collection { get; init; }

    [BsonElement("license")]
    public required string License { get; init; }

    [BsonElement("sourceUrl")]
    public required string SourceUrl { get; init; }

    [BsonElement("attributionUrl")]
    public required string AttributionUrl { get; init; }

    [BsonElement("quotations")]
    public List<string> Quotations { get; init; } = [];

    [BsonElement("context")]
    public required string Context { get; init; }

    [BsonElement("isExcerpt")]
    public bool IsExcerpt { get; init; }
}
