using MongoDB.Bson.Serialization.Attributes;

namespace AskARabbiLIB.Persistence.Mongo;

[BsonIgnoreExtraElements]
internal sealed class MongoWeeklyDvarTorahSourceDocument
{
    [BsonElement("sourceId")]
    public required string SourceId { get; init; }

    [BsonElement("kind")]
    public required string Kind { get; init; }

    [BsonElement("title")]
    public required string Title { get; init; }

    [BsonElement("publisher")]
    public required string Publisher { get; init; }

    [BsonElement("sourceUrl")]
    public required string SourceUrl { get; init; }

    [BsonElement("excerpt")]
    public required string Excerpt { get; init; }

    [BsonElement("retrievedAtUtc")]
    public DateTime RetrievedAtUtc { get; init; }

    [BsonElement("canonicalReference")]
    [BsonIgnoreIfNull]
    public string? CanonicalReference { get; init; }

    [BsonElement("publishedAtUtc")]
    [BsonIgnoreIfNull]
    public DateTime? PublishedAtUtc { get; init; }

    [BsonElement("license")]
    [BsonIgnoreIfNull]
    public string? License { get; init; }
}
