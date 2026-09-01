using MongoDB.Bson.Serialization.Attributes;

namespace AskARabbiLIB.Persistence.Mongo;

[BsonIgnoreExtraElements]
internal sealed class MongoWeeklyDvarTorahDocument
{
    [BsonId]
    public required string Id { get; init; }

    [BsonElement("shabbatDate")]
    [BsonSerializer(typeof(DateOnlySerializer))]
    public DateOnly ShabbatDate { get; init; }

    [BsonElement("hebrewDate")]
    public required string HebrewDate { get; init; }

    [BsonElement("parashah")]
    [BsonIgnoreIfNull]
    public string? Parashah { get; init; }

    [BsonElement("holiday")]
    [BsonIgnoreIfNull]
    public string? Holiday { get; init; }

    [BsonElement("inIsrael")]
    public bool InIsrael { get; init; }

    [BsonElement("status")]
    public required string Status { get; set; }

    [BsonElement("title")]
    [BsonIgnoreIfNull]
    public string? Title { get; set; }

    [BsonElement("body")]
    [BsonIgnoreIfNull]
    public string? Body { get; set; }

    [BsonElement("generatorVersion")]
    [BsonIgnoreIfNull]
    public string? GeneratorVersion { get; set; }

    [BsonElement("centralTeaching")]
    [BsonIgnoreIfNull]
    public string? CentralTeaching { get; set; }

    [BsonElement("tags")]
    [BsonIgnoreIfNull]
    public string[]? Tags { get; set; }

    [BsonElement("sources")]
    [BsonIgnoreIfNull]
    public MongoWeeklyDvarTorahSourceDocument[]? Sources { get; set; }

    [BsonElement("torahGroundingPercent")]
    [BsonIgnoreIfNull]
    public int? TorahGroundingPercent { get; set; }

    [BsonElement("safetyReviewVersion")]
    [BsonIgnoreIfNull]
    public string? SafetyReviewVersion { get; set; }

    [BsonElement("model")]
    [BsonIgnoreIfNull]
    public string? Model { get; set; }

    [BsonElement("newsWindowStartedAtUtc")]
    [BsonIgnoreIfNull]
    public DateTime? NewsWindowStartedAtUtc { get; set; }

    [BsonElement("newsWindowEndedAtUtc")]
    [BsonIgnoreIfNull]
    public DateTime? NewsWindowEndedAtUtc { get; set; }

    [BsonElement("generatedAtUtc")]
    [BsonIgnoreIfNull]
    public DateTime? GeneratedAtUtc { get; set; }

    [BsonElement("publishedAtUtc")]
    [BsonIgnoreIfNull]
    public DateTime? PublishedAtUtc { get; set; }

    [BsonElement("generationLeaseId")]
    [BsonIgnoreIfNull]
    public string? GenerationLeaseId { get; set; }

    [BsonElement("generationLeaseExpiresAtUtc")]
    [BsonIgnoreIfNull]
    public DateTime? GenerationLeaseExpiresAtUtc { get; set; }

    [BsonElement("generationAttemptCount")]
    public int GenerationAttemptCount { get; set; }

    [BsonElement("lastAttemptedAtUtc")]
    public DateTime LastAttemptedAtUtc { get; set; }

    [BsonElement("failureCode")]
    [BsonIgnoreIfNull]
    public string? FailureCode { get; set; }

    [BsonElement("updatedAtUtc")]
    public DateTime UpdatedAtUtc { get; set; }
}
