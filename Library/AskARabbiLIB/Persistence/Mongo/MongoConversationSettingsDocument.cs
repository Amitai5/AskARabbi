using MongoDB.Bson.Serialization.Attributes;

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

    [BsonElement("updatedAtUtc")]
    public DateTime UpdatedAtUtc { get; init; }
}
