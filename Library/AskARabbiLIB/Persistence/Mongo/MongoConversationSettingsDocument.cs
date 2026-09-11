using MongoDB.Bson.Serialization.Attributes;
using AskARabbiLIB.Calendar;
using AskARabbiLIB.ConversationSettings;

namespace AskARabbiLIB.Persistence.Mongo;

[BsonIgnoreExtraElements]
internal sealed class MongoConversationSettingsDocument
{
    [BsonId]
    public required string UserId { get; init; }

    [BsonElement("personalization")]
    [BsonIgnoreIfNull]
    public MongoPersonalizationDocument? Personalization { get; init; }

    [BsonElement("preferences")]
    [BsonIgnoreIfNull]
    public MongoConversationPreferencesDocument? Preferences { get; init; }

    [BsonElement("calendarPreferences")]
    [BsonIgnoreIfNull]
    public CalendarPreferences? CalendarPreferences { get; init; }

    [BsonElement("readingPreferences")]
    [BsonIgnoreIfNull]
    public ReadingPreferences? ReadingPreferences { get; init; }

    [BsonElement("updatedAtUtc")]
    public DateTime UpdatedAtUtc { get; init; }
}
