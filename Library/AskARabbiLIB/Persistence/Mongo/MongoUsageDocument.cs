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
}
