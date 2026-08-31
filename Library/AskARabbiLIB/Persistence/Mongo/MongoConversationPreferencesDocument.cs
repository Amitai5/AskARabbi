using AskARabbiLIB.ConversationSettings;
using MongoDB.Bson.Serialization.Attributes;

namespace AskARabbiLIB.Persistence.Mongo;

[BsonIgnoreExtraElements]
internal sealed class MongoConversationPreferencesDocument
{
    internal const int CurrentDefaultsVersion = 1;

    [BsonElement("defaultsVersion")]
    public int DefaultsVersion { get; init; }

    [BsonElement("showSourceContextByDefault")]
    public bool ShowSourceContextByDefault { get; init; }

    [BsonElement("emailProductUpdates")]
    public bool EmailProductUpdates { get; init; }

    internal ConversationPreferences ToDomain() => new()
    {
        ShowSourceContextByDefault = DefaultsVersion >= CurrentDefaultsVersion && ShowSourceContextByDefault,
        EmailProductUpdates = EmailProductUpdates,
    };

    internal static MongoConversationPreferencesDocument FromDomain(ConversationPreferences preferences) => new()
    {
        DefaultsVersion = CurrentDefaultsVersion,
        ShowSourceContextByDefault = preferences.ShowSourceContextByDefault,
        EmailProductUpdates = preferences.EmailProductUpdates,
    };
}
