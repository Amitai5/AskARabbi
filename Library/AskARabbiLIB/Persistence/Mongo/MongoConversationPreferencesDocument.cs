using MongoDB.Bson.Serialization.Attributes;

namespace AskARabbiLIB.Persistence.Mongo;

[BsonIgnoreExtraElements]
internal sealed class MongoConversationPreferencesDocument
{
    [BsonElement("showSourceContextByDefault")]
    public bool ShowSourceContextByDefault { get; init; } = true;

    [BsonElement("emailProductUpdates")]
    public bool EmailProductUpdates { get; init; }
}
