namespace AskARabbiLIB.Persistence.Mongo;

/// <summary>Configures Azure Cosmos DB for MongoDB collections used by AskRabbi.</summary>
public sealed class MongoDatabaseOptions
{
    /// <summary>Gets the configuration section name.</summary>
    public const string SectionName = "MongoDB";

    /// <summary>Gets the Azure Cosmos DB for MongoDB connection string.</summary>
    public string ConnectionString { get; init; } = string.Empty;

    /// <summary>Gets the database name.</summary>
    public string DatabaseName { get; init; } = "askarabbi";

    /// <summary>Gets the user-account collection name.</summary>
    public string UsersCollectionName { get; init; } = "users";

    /// <summary>Gets the conversation collection name.</summary>
    public string ConversationsCollectionName { get; init; } = "conversations";

    /// <summary>Gets the conversation-message collection name.</summary>
    public string ConversationMessagesCollectionName { get; init; } = "conversationMessages";

    /// <summary>Gets the conversation-settings collection name.</summary>
    public string ConversationSettingsCollectionName { get; init; } = "conversationSettings";

    /// <summary>Gets the monthly usage collection name.</summary>
    public string UsageCollectionName { get; init; } = "usage";

    /// <summary>Gets whether the minimum MongoDB connection configuration is present.</summary>
    public bool IsConfigured => !string.IsNullOrWhiteSpace(ConnectionString) && !string.IsNullOrWhiteSpace(DatabaseName);

    /// <summary>Validates database and collection configuration.</summary>
    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(ConnectionString))
        {
            throw new InvalidOperationException($"{SectionName}:ConnectionString is required.");
        }

        ValidateName(DatabaseName, nameof(DatabaseName));
        ValidateName(UsersCollectionName, nameof(UsersCollectionName));
        ValidateName(ConversationsCollectionName, nameof(ConversationsCollectionName));
        ValidateName(ConversationMessagesCollectionName, nameof(ConversationMessagesCollectionName));
        ValidateName(ConversationSettingsCollectionName, nameof(ConversationSettingsCollectionName));
        ValidateName(UsageCollectionName, nameof(UsageCollectionName));
    }

    private static void ValidateName(string value, string propertyName)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new InvalidOperationException($"{SectionName}:{propertyName} is required.");
        }
    }
}
