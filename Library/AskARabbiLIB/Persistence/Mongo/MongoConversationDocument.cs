using MongoDB.Bson.Serialization.Attributes;

namespace AskARabbiLIB.Persistence.Mongo;

[BsonIgnoreExtraElements]
internal sealed class MongoConversationDocument
{
    [BsonId]
    public required string Id { get; init; }

    [BsonElement("userId")]
    public required string UserId { get; init; }

    [BsonElement("title")]
    public required string Title { get; init; }

    [BsonElement("enabledSourceKeys")]
    public required List<string> EnabledSourceKeys { get; init; }

    [BsonElement("createdAtUtc")]
    public DateTime CreatedAtUtc { get; init; }

    [BsonElement("updatedAtUtc")]
    public DateTime UpdatedAtUtc { get; init; }
}
