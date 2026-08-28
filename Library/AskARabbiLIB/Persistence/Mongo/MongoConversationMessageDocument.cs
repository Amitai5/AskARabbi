using MongoDB.Bson.Serialization.Attributes;

namespace AskARabbiLIB.Persistence.Mongo;

[BsonIgnoreExtraElements]
internal sealed class MongoConversationMessageDocument
{
    [BsonId]
    public required string Id { get; init; }

    [BsonElement("conversationId")]
    public required string ConversationId { get; init; }

    [BsonElement("userId")]
    public required string UserId { get; init; }

    [BsonElement("messageId")]
    public required string MessageId { get; init; }

    [BsonElement("role")]
    public required string Role { get; init; }

    [BsonElement("content")]
    public required string Content { get; init; }

    [BsonElement("createdAtUtc")]
    public DateTime CreatedAtUtc { get; init; }
}
