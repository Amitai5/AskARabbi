using MongoDB.Bson.Serialization.Attributes;

namespace AskARabbiLIB.Persistence.Mongo;

internal sealed class MongoConversationSummaryProjection
{
    [BsonId]
    public required string Id { get; init; }

    [BsonElement("title")]
    public string Title { get; init; } = "New Conversation";

    [BsonElement("enabledSourceKeys")]
    public List<string> EnabledSourceKeys { get; init; } = [];

    [BsonElement("updatedAtUtc")]
    public DateTime UpdatedAtUtc { get; init; }
}
