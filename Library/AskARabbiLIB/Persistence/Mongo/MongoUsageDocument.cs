using MongoDB.Bson.Serialization.Attributes;

namespace AskARabbiLIB.Persistence.Mongo;

[BsonIgnoreExtraElements]
internal sealed class MongoUsageDocument
{
    [BsonId]
    public required string Id { get; init; }

    [BsonElement("userId")]
    public required string UserId { get; init; }

    [BsonElement("periodStartUtc")]
    public DateTime PeriodStartUtc { get; init; }

    [BsonElement("periodEndUtc")]
    public DateTime PeriodEndUtc { get; init; }

    [BsonElement("answerCount")]
    public int AnswerCount { get; init; }

    [BsonElement("tokenCount")]
    public long TokenCount { get; init; }

    [BsonElement("chatLeaseId")]
    public string? ChatLeaseId { get; init; }

    [BsonElement("chatLeaseExpiresAtUtc")]
    public DateTime? ChatLeaseExpiresAtUtc { get; init; }

    [BsonElement("chatLeaseTokens")]
    public long ChatLeaseTokens { get; init; }
}
