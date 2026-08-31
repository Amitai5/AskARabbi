using AskARabbiLIB.Conversations;
using MongoDB.Driver;

namespace AskARabbiLIB.Persistence.Mongo;

/// <summary>Stores owner-scoped conversations in Azure Cosmos DB for MongoDB.</summary>
public sealed class MongoConversationStore : IConversationStore
{
    private readonly IMongoCollection<MongoConversationDocument> conversations;
    private readonly IMongoCollection<MongoConversationMessageDocument> messages;

    /// <summary>Initializes a MongoDB conversation store.</summary>
    /// <param name="database">MongoDB database.</param>
    /// <param name="options">Collection configuration.</param>
    public MongoConversationStore(IMongoDatabase database, MongoDatabaseOptions options)
    {
        ArgumentNullException.ThrowIfNull(database);
        ArgumentNullException.ThrowIfNull(options);
        conversations = database.GetCollection<MongoConversationDocument>(options.ConversationsCollectionName);
        messages = database.GetCollection<MongoConversationMessageDocument>(options.ConversationMessagesCollectionName);
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<ConversationSummary>> ListAsync(Guid userId, int limit, CancellationToken cancellationToken = default)
    {
        var documents = await conversations.Find(document => document.UserId == userId.ToString("D"))
            .SortByDescending(document => document.UpdatedAtUtc)
            .Limit(limit)
            .Project(document => new MongoConversationSummaryProjection
            {
                Id = document.Id,
                Title = document.Title,
                EnabledSourceKeys = document.EnabledSourceKeys,
                UpdatedAtUtc = document.UpdatedAtUtc,
            })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return documents.Select(document => new ConversationSummary(Guid.Parse(document.Id), document.Title, document.EnabledSourceKeys, AsUtc(document.UpdatedAtUtc))).ToArray();
    }

    /// <inheritdoc/>
    public async Task<Conversation?> GetAsync(Guid userId, Guid conversationId, CancellationToken cancellationToken = default)
    {
        var document = await conversations.Find(OwnerFilter(userId, conversationId)).FirstOrDefaultAsync(cancellationToken).ConfigureAwait(false);
        if (document is null)
        {
            return null;
        }

        var messageDocuments = await messages.Find(MessageOwnerFilter(userId, conversationId))
            .Sort(CreateMessageSort())
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        return ToDomain(document, messageDocuments);
    }

    /// <inheritdoc/>
    public Task CreateAsync(Conversation conversation, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(conversation);
        return conversations.InsertOneAsync(ToDocument(conversation), cancellationToken: cancellationToken);
    }

    /// <inheritdoc/>
    public async Task<Conversation?> AppendMessageAsync(Guid userId, Guid conversationId, ConversationMessage message, DateTimeOffset updatedAtUtc, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(message);
        var ownerExists = await conversations.Find(OwnerFilter(userId, conversationId)).AnyAsync(cancellationToken).ConfigureAwait(false);
        if (!ownerExists)
        {
            return null;
        }

        try
        {
            await messages.InsertOneAsync(ToDocument(userId, conversationId, message), cancellationToken: cancellationToken).ConfigureAwait(false);
        }
        catch (MongoWriteException exception) when (exception.WriteError?.Category == ServerErrorCategory.DuplicateKey)
        {
            return await GetAsync(userId, conversationId, cancellationToken).ConfigureAwait(false);
        }

        var update = Builders<MongoConversationDocument>.Update.Set(document => document.UpdatedAtUtc, updatedAtUtc.UtcDateTime);
        await conversations.UpdateOneAsync(OwnerFilter(userId, conversationId), update, cancellationToken: cancellationToken).ConfigureAwait(false);

        return await GetAsync(userId, conversationId, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async Task<bool> RenameAsync(Guid userId, Guid conversationId, string title, DateTimeOffset updatedAtUtc, CancellationToken cancellationToken = default)
    {
        var update = Builders<MongoConversationDocument>.Update.Set(document => document.Title, title).Set(document => document.UpdatedAtUtc, updatedAtUtc.UtcDateTime);
        var result = await conversations.UpdateOneAsync(OwnerFilter(userId, conversationId), update, cancellationToken: cancellationToken).ConfigureAwait(false);
        return result.MatchedCount == 1;
    }

    /// <inheritdoc/>
    public async Task<bool> UpdateSourcesAsync(Guid userId, Guid conversationId, IReadOnlyList<string> sourceKeys, DateTimeOffset updatedAtUtc, CancellationToken cancellationToken = default)
    {
        var update = Builders<MongoConversationDocument>.Update.Set(document => document.EnabledSourceKeys, sourceKeys.ToList()).Set(document => document.UpdatedAtUtc, updatedAtUtc.UtcDateTime);
        var result = await conversations.UpdateOneAsync(OwnerFilter(userId, conversationId), update, cancellationToken: cancellationToken).ConfigureAwait(false);
        return result.MatchedCount == 1;
    }

    /// <inheritdoc/>
    public async Task<bool> DeleteAsync(Guid userId, Guid conversationId, CancellationToken cancellationToken = default)
    {
        var result = await conversations.DeleteOneAsync(OwnerFilter(userId, conversationId), cancellationToken).ConfigureAwait(false);
        if (result.DeletedCount != 1)
        {
            return false;
        }

        await messages.DeleteManyAsync(MessageOwnerFilter(userId, conversationId), cancellationToken).ConfigureAwait(false);
        return true;
    }

    private static FilterDefinition<MongoConversationDocument> OwnerFilter(Guid userId, Guid conversationId) => Builders<MongoConversationDocument>.Filter.And(
        Builders<MongoConversationDocument>.Filter.Eq(document => document.Id, conversationId.ToString("D")),
        Builders<MongoConversationDocument>.Filter.Eq(document => document.UserId, userId.ToString("D")));

    private static FilterDefinition<MongoConversationMessageDocument> MessageOwnerFilter(Guid userId, Guid conversationId) => Builders<MongoConversationMessageDocument>.Filter.And(
        Builders<MongoConversationMessageDocument>.Filter.Eq(document => document.ConversationId, conversationId.ToString("D")),
        Builders<MongoConversationMessageDocument>.Filter.Eq(document => document.UserId, userId.ToString("D")));

    internal static SortDefinition<MongoConversationMessageDocument> CreateMessageSort() => Builders<MongoConversationMessageDocument>.Sort
        .Ascending(document => document.CreatedAtUtc)
        .Ascending(document => document.Id);

    private static MongoConversationDocument ToDocument(Conversation conversation) => new()
    {
        Id = conversation.Id.ToString("D"),
        UserId = conversation.UserId.ToString("D"),
        Title = conversation.Title,
        EnabledSourceKeys = conversation.EnabledSourceKeys.ToList(),
        CreatedAtUtc = conversation.CreatedAtUtc.UtcDateTime,
        UpdatedAtUtc = conversation.UpdatedAtUtc.UtcDateTime,
    };

    private static MongoConversationMessageDocument ToDocument(Guid userId, Guid conversationId, ConversationMessage message) => new()
    {
        Id = $"{conversationId:D}:{message.Id:D}",
        ConversationId = conversationId.ToString("D"),
        UserId = userId.ToString("D"),
        MessageId = message.Id.ToString("D"),
        Role = message.Role.ToString(),
        Content = message.Content,
        CreatedAtUtc = message.CreatedAtUtc.UtcDateTime,
    };

    private static Conversation ToDomain(MongoConversationDocument document, IReadOnlyList<MongoConversationMessageDocument> messageDocuments) => new()
    {
        Id = Guid.Parse(document.Id),
        UserId = Guid.Parse(document.UserId),
        Title = document.Title,
        EnabledSourceKeys = document.EnabledSourceKeys,
        Messages = messageDocuments.Select(ToDomain).ToArray(),
        CreatedAtUtc = AsUtc(document.CreatedAtUtc),
        UpdatedAtUtc = AsUtc(document.UpdatedAtUtc),
    };

    private static ConversationMessage ToDomain(MongoConversationMessageDocument document) => new()
    {
        Id = Guid.Parse(document.MessageId),
        Role = Enum.Parse<ConversationMessageRole>(document.Role, false),
        Content = document.Content,
        CreatedAtUtc = AsUtc(document.CreatedAtUtc),
    };

    private static DateTimeOffset AsUtc(DateTime value) => new(DateTime.SpecifyKind(value, DateTimeKind.Utc));
}
