using MongoDB.Driver;

namespace AskARabbiLIB.Persistence.Mongo;

/// <summary>Creates indexes for account uniqueness, recent conversations, and chronological message history.</summary>
public sealed class MongoIndexManager
{
    private readonly IMongoCollection<MongoUserAccountDocument> users;
    private readonly IMongoCollection<MongoConversationDocument> conversations;
    private readonly IMongoCollection<MongoConversationMessageDocument> messages;
    private readonly IMongoCollection<MongoWeeklyDvarTorahDocument> weeklyDvarTorah;

    /// <summary>Initializes a MongoDB index manager.</summary>
    /// <param name="database">MongoDB database.</param>
    /// <param name="options">Collection configuration.</param>
    public MongoIndexManager(IMongoDatabase database, MongoDatabaseOptions options)
    {
        ArgumentNullException.ThrowIfNull(database);
        ArgumentNullException.ThrowIfNull(options);
        users = database.GetCollection<MongoUserAccountDocument>(options.UsersCollectionName);
        conversations = database.GetCollection<MongoConversationDocument>(options.ConversationsCollectionName);
        messages = database.GetCollection<MongoConversationMessageDocument>(options.ConversationMessagesCollectionName);
        weeklyDvarTorah = database.GetCollection<MongoWeeklyDvarTorahDocument>(options.DvarTorahCollectionName);
    }

    /// <summary>Creates required indexes if they do not already exist.</summary>
    /// <param name="cancellationToken">Token that can cancel the operation.</param>
    /// <returns>A task representing the operation.</returns>
    public async Task EnsureCreatedAsync(CancellationToken cancellationToken = default)
    {
        var userIndex = new CreateIndexModel<MongoUserAccountDocument>(Builders<MongoUserAccountDocument>.IndexKeys.Ascending(document => document.ProviderUserId), new CreateIndexOptions
        {
            Name = "ux_users_providerUserId",
            Unique = true,
        });
        var conversationIndex = new CreateIndexModel<MongoConversationDocument>(Builders<MongoConversationDocument>.IndexKeys
            .Ascending(document => document.UserId)
            .Descending(document => document.UpdatedAtUtc), new CreateIndexOptions
            {
                Name = "ix_conversations_userId_updatedAtUtc",
            });
        var messageIndex = CreateMessageIndex();

        await users.Indexes.CreateOneAsync(userIndex, cancellationToken: cancellationToken).ConfigureAwait(false);
        await conversations.Indexes.CreateOneAsync(conversationIndex, cancellationToken: cancellationToken).ConfigureAwait(false);
        await messages.Indexes.CreateOneAsync(messageIndex, cancellationToken: cancellationToken).ConfigureAwait(false);
        await weeklyDvarTorah.Indexes.CreateOneAsync(MongoWeeklyDvarTorahStore.CreateLatestPublishedIndex(), cancellationToken: cancellationToken).ConfigureAwait(false);
    }

    internal static CreateIndexModel<MongoConversationMessageDocument> CreateMessageIndex()
    {
        var keys = Builders<MongoConversationMessageDocument>.IndexKeys
            .Ascending(document => document.CreatedAtUtc)
            .Ascending(document => document.Id);

        // Cosmos DB requires the composite-index sequence to exactly match every ORDER BY field.
        return new CreateIndexModel<MongoConversationMessageDocument>(keys);
    }
}
