using MongoDB.Bson.Serialization.Attributes;

namespace AskARabbiLIB.Persistence.Mongo;

[BsonIgnoreExtraElements]
internal sealed class MongoWeeklyDvarTorahArchiveDocument
{
    [BsonId]
    public string? Id { get; init; }

    [BsonElement("shabbatDate")]
    [BsonSerializer(typeof(DateOnlySerializer))]
    public DateOnly ShabbatDate { get; init; }

    [BsonElement("hebrewDate")]
    public string? HebrewDate { get; init; }

    [BsonElement("parashah")]
    public string? Parashah { get; init; }

    [BsonElement("holiday")]
    public string? Holiday { get; init; }

    [BsonElement("inIsrael")]
    public bool InIsrael { get; init; }

    [BsonElement("title")]
    public string? Title { get; init; }

    [BsonElement("tags")]
    public string[]? Tags { get; init; }

    [BsonElement("publishedAtUtc")]
    public DateTime? PublishedAtUtc { get; init; }
}
