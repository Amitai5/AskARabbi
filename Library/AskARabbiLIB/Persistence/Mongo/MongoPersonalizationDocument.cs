using MongoDB.Bson.Serialization.Attributes;
using AskARabbiLIB.Calendar;

namespace AskARabbiLIB.Persistence.Mongo;

[BsonIgnoreExtraElements]
internal sealed class MongoPersonalizationDocument
{
    [BsonElement("fullName")]
    public required string FullName { get; init; }

    [BsonElement("birthDate")]
    [BsonSerializer(typeof(DateOnlySerializer))]
    public DateOnly BirthDate { get; init; }

    [BsonElement("birthTime")]
    [BsonSerializer(typeof(TimeOnlySerializer))]
    public TimeOnly BirthTime { get; init; }

    [BsonElement("birthTimeZone")]
    public required string BirthTimeZone { get; init; }

    [BsonElement("birthLocation")]
    public CalendarLocation? BirthLocation { get; init; }

    [BsonElement("currentLocation")]
    public CalendarLocation? CurrentLocation { get; init; }

    [BsonElement("conversationLanguage")]
    public required string ConversationLanguage { get; init; }

    [BsonElement("quotationLanguage")]
    public required string QuotationLanguage { get; init; }

    [BsonElement("religiousMovement")]
    public required string ReligiousMovement { get; init; }

    [BsonElement("jewishHeritage")]
    public required string JewishHeritage { get; init; }

    [BsonElement("additionalContext")]
    public string? AdditionalContext { get; init; }
}
