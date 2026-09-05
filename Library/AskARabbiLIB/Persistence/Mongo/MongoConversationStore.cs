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
            .Project<MongoConversationSummaryProjection>(Builders<MongoConversationDocument>.Projection
                .Include(document => document.Id)
                .Include(document => document.Title)
                .Include(document => document.EnabledSourceKeys)
                .Include(document => document.UpdatedAtUtc))
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
    public async Task CreateAsync(Conversation conversation, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(conversation);
        var initialMessages = conversation.Messages.Select(message => ToDocument(conversation.UserId, conversation.Id, message)).ToArray();
        if (initialMessages.Length == 0)
        {
            await conversations.InsertOneAsync(ToDocument(conversation), cancellationToken: cancellationToken).ConfigureAwait(false);
            return;
        }

        await messages.InsertManyAsync(initialMessages, cancellationToken: cancellationToken).ConfigureAwait(false);
        try
        {
            await conversations.InsertOneAsync(ToDocument(conversation), cancellationToken: cancellationToken).ConfigureAwait(false);
        }
        catch (Exception creationException)
        {
            try
            {
                await messages.DeleteManyAsync(MessageOwnerFilter(conversation.UserId, conversation.Id), CancellationToken.None).ConfigureAwait(false);
            }
            catch (Exception cleanupException)
            {
                throw new AggregateException("Conversation creation failed and its initial-message compensation also failed.", creationException, cleanupException);
            }

            throw;
        }
    }

    /// <inheritdoc/>
    public async Task<Conversation?> AppendMessageAsync(Guid userId, Guid conversationId, ConversationMessage message, DateTimeOffset updatedAtUtc, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(message);
        var conversation = await GetAsync(userId, conversationId, cancellationToken).ConfigureAwait(false);
        return conversation is null ? null : await AppendMessageAsync(conversation, message, updatedAtUtc, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public Task<Conversation?> AppendMessageAsync(Conversation conversation, ConversationMessage message, DateTimeOffset updatedAtUtc, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(conversation);
        ArgumentNullException.ThrowIfNull(message);
        return AppendMessageCoreAsync(conversation, message, null, updatedAtUtc, cancellationToken);
    }

    /// <inheritdoc/>
    public Task<Conversation?> AppendMessageWithTitleAsync(Conversation conversation, ConversationMessage message, string title, DateTimeOffset updatedAtUtc, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(conversation);
        ArgumentNullException.ThrowIfNull(message);
        if (string.IsNullOrWhiteSpace(title))
        {
            throw new ArgumentException("A generated title is required.", nameof(title));
        }

        return AppendMessageCoreAsync(conversation, message, title, updatedAtUtc, cancellationToken);
    }

    private async Task<Conversation?> AppendMessageCoreAsync(Conversation conversation, ConversationMessage message, string? title, DateTimeOffset updatedAtUtc, CancellationToken cancellationToken)
    {
        if (conversation.Messages.Any(existing => existing.Id == message.Id))
        {
            return title is null || string.Equals(conversation.Title, title, StringComparison.Ordinal)
                ? conversation
                : await UpdateMetadataAsync(conversation, title, updatedAtUtc, cancellationToken).ConfigureAwait(false);
        }

        try
        {
            await messages.InsertOneAsync(ToDocument(conversation.UserId, conversation.Id, message), cancellationToken: cancellationToken).ConfigureAwait(false);
        }
        catch (MongoWriteException exception) when (exception.WriteError?.Category == ServerErrorCategory.DuplicateKey)
        {
            var canonical = await GetAsync(conversation.UserId, conversation.Id, cancellationToken).ConfigureAwait(false);
            return canonical is null || title is null || string.Equals(canonical.Title, title, StringComparison.Ordinal)
                ? canonical
                : await UpdateMetadataAsync(canonical, title, updatedAtUtc, cancellationToken).ConfigureAwait(false);
        }

        UpdateResult updateResult;
        try
        {
            updateResult = await conversations.UpdateOneAsync(OwnerFilter(conversation.UserId, conversation.Id), CreateMetadataUpdate(updatedAtUtc, title), cancellationToken: cancellationToken).ConfigureAwait(false);
        }
        catch (Exception updateException)
        {
            try
            {
                await DeleteMessageAsync(conversation.Id, message.Id, CancellationToken.None).ConfigureAwait(false);
            }
            catch (Exception cleanupException)
            {
                throw new AggregateException("Conversation metadata update failed and its appended-message compensation also failed.", updateException, cleanupException);
            }

            throw;
        }

        if (updateResult.MatchedCount != 1)
        {
            await DeleteMessageAsync(conversation.Id, message.Id, cancellationToken).ConfigureAwait(false);
            return null;
        }

        var updatedMessages = conversation.Messages.Append(message).OrderBy(value => value.CreatedAtUtc).ThenBy(value => value.Id).ToArray();
        return conversation with { Title = title ?? conversation.Title, Messages = updatedMessages, UpdatedAtUtc = updatedAtUtc };
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

    internal static UpdateDefinition<MongoConversationDocument> CreateMetadataUpdate(DateTimeOffset updatedAtUtc, string? title)
    {
        var updates = new List<UpdateDefinition<MongoConversationDocument>>
        {
            Builders<MongoConversationDocument>.Update.Set(document => document.UpdatedAtUtc, updatedAtUtc.UtcDateTime),
        };
        if (title is not null)
        {
            updates.Add(Builders<MongoConversationDocument>.Update.Set(document => document.Title, title));
        }

        return Builders<MongoConversationDocument>.Update.Combine(updates);
    }

    private async Task<Conversation?> UpdateMetadataAsync(Conversation conversation, string title, DateTimeOffset updatedAtUtc, CancellationToken cancellationToken)
    {
        var result = await conversations.UpdateOneAsync(OwnerFilter(conversation.UserId, conversation.Id), CreateMetadataUpdate(updatedAtUtc, title), cancellationToken: cancellationToken).ConfigureAwait(false);
        return result.MatchedCount == 1 ? conversation with { Title = title, UpdatedAtUtc = updatedAtUtc } : null;
    }

    private Task<DeleteResult> DeleteMessageAsync(Guid conversationId, Guid messageId, CancellationToken cancellationToken) => messages.DeleteOneAsync(document => document.Id == $"{conversationId:D}:{messageId:D}", cancellationToken);

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
        Sources = message.Sources.Select(ToDocument).ToList(),
        CreatedAtUtc = message.CreatedAtUtc.UtcDateTime,
    };

    private static MongoConversationSourceDocument ToDocument(ConversationSourceCitation source) => new()
    {
        Number = source.Number,
        Title = source.Title,
        HebrewTitle = source.HebrewTitle,
        CanonicalReference = source.CanonicalReference,
        Edition = source.Edition,
        Language = source.Language,
        Collection = source.Collection,
        License = source.License,
        SourceUrl = source.SourceUrl,
        AttributionUrl = source.AttributionUrl,
        Quotations = source.Quotations.ToList(),
        Context = source.Context,
        IsExcerpt = source.IsExcerpt,
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
        Sources = document.Sources.Select(ToDomain).ToArray(),
        CreatedAtUtc = AsUtc(document.CreatedAtUtc),
    };

    private static ConversationSourceCitation ToDomain(MongoConversationSourceDocument source) => new()
    {
        Number = source.Number,
        Title = source.Title,
        HebrewTitle = source.HebrewTitle,
        CanonicalReference = source.CanonicalReference,
        Edition = source.Edition,
        Language = source.Language,
        Collection = source.Collection,
        License = source.License,
        SourceUrl = source.SourceUrl,
        AttributionUrl = source.AttributionUrl,
        Quotations = source.Quotations.ToArray(),
        Context = source.Context,
        IsExcerpt = source.IsExcerpt,
    };

    private static DateTimeOffset AsUtc(DateTime value) => new(DateTime.SpecifyKind(value, DateTimeKind.Utc));
}
