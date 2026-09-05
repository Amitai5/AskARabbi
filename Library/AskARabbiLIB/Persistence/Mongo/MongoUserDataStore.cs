using AskARabbiLIB.Accounts;
using MongoDB.Bson;
using MongoDB.Driver;

namespace AskARabbiLIB.Persistence.Mongo;

/// <summary>Uses atomic account-document leases; erasure remains retryable without cross-collection transactions.</summary>
public sealed class MongoUserDataStore : IUserDataStore
{
    private readonly IMongoCollection<BsonDocument> users;
    private readonly IMongoCollection<BsonDocument>[] chatCollections;
    private readonly IMongoCollection<BsonDocument> settings;
    private readonly IMongoCollection<BsonDocument> usage;

    /// <summary>Initializes owner-scoped deletion storage. Shared publications and source libraries are deliberately excluded.</summary>
    /// <param name="database">Application database.</param>
    /// <param name="options">Existing collection names.</param>
    public MongoUserDataStore(IMongoDatabase database, MongoDatabaseOptions options)
    {
        ArgumentNullException.ThrowIfNull(database);
        ArgumentNullException.ThrowIfNull(options);
        users = database.GetCollection<BsonDocument>(options.UsersCollectionName);
        chatCollections = [database.GetCollection<BsonDocument>(options.ConversationsCollectionName), database.GetCollection<BsonDocument>(options.ConversationMessagesCollectionName)];
        settings = database.GetCollection<BsonDocument>(options.ConversationSettingsCollectionName);
        usage = database.GetCollection<BsonDocument>(options.UsageCollectionName);
    }

    /// <inheritdoc/>
    public async Task<bool> TryAcquireAsync(Guid userId, Guid operationId, bool exclusive, DateTimeOffset now, DateTimeOffset expiresAt, CancellationToken cancellationToken = default)
    {
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(expiresAt, now);
        var owner = Owner(userId);
        // Expired leases belong to terminated requests/replicas; live requests have a much shorter hard timeout.
        await users.UpdateOneAsync(owner, new BsonDocument("$pull", new BsonDocument("dataOperations", new BsonDocument("expiresAtUtc", new BsonDocument("$lte", now.UtcDateTime)))), cancellationToken: cancellationToken).ConfigureAwait(false);
        var filter = ActiveOwner(userId);
        var incompatible = new BsonDocument("expiresAtUtc", new BsonDocument("$gt", now.UtcDateTime));
        if (!exclusive)
        {
            incompatible.Add("exclusive", true);
        }
        filter.Add("dataOperations", new BsonDocument("$not", new BsonDocument("$elemMatch", incompatible)));
        var lease = new BsonDocument { { "id", operationId.ToString("D") }, { "exclusive", exclusive }, { "expiresAtUtc", expiresAt.UtcDateTime } };
        var result = await users.UpdateOneAsync(filter, new BsonDocument("$push", new BsonDocument("dataOperations", lease)), cancellationToken: cancellationToken).ConfigureAwait(false);
        return result.MatchedCount == 1;
    }

    /// <inheritdoc/>
    public Task ReleaseAsync(Guid userId, Guid operationId, CancellationToken cancellationToken = default) => users.UpdateOneAsync(Owner(userId), new BsonDocument("$pull", new BsonDocument("dataOperations", new BsonDocument("id", operationId.ToString("D")))), cancellationToken: cancellationToken);

    /// <inheritdoc/>
    public async Task<PendingAccountDeletion?> TryRequestDeletionAsync(Guid userId, DateTimeOffset now, CancellationToken cancellationToken = default)
    {
        var filter = ActiveOwner(userId);
        filter.Add("dataOperations", new BsonDocument("$not", new BsonDocument("$elemMatch", new BsonDocument("expiresAtUtc", new BsonDocument("$gt", now.UtcDateTime)))));
        var document = await users.FindOneAndUpdateAsync(filter, new BsonDocument("$set", new BsonDocument("deletionRequestedAtUtc", now.UtcDateTime)), new FindOneAndUpdateOptions<BsonDocument> { ReturnDocument = ReturnDocument.After }, cancellationToken).ConfigureAwait(false);
        return document is null ? null : ToPending(document);
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<PendingAccountDeletion>> ListPendingDeletionsAsync(CancellationToken cancellationToken = default)
    {
        // Do not repeatedly return only the first page: a failing provider identity must not starve later requests.
        var documents = await users.Find(new BsonDocument("deletionRequestedAtUtc", new BsonDocument("$type", "date"))).ToListAsync(cancellationToken).ConfigureAwait(false);
        return documents.Select(ToPending).ToArray();
    }

    /// <inheritdoc/>
    public async Task DeleteChatsAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var owner = DataOwner(userId);
        foreach (var collection in chatCollections)
        {
            await collection.DeleteManyAsync(owner, cancellationToken).ConfigureAwait(false);
        }
    }

    /// <inheritdoc/>
    public async Task CompleteDeletionAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var pendingOwner = Owner(userId).Add("deletionRequestedAtUtc", new BsonDocument("$type", "date"));
        if (!await users.Find(pendingOwner).AnyAsync(cancellationToken).ConfigureAwait(false))
        {
            return;
        }
        await DeleteChatsAsync(userId, cancellationToken).ConfigureAwait(false);
        // Conversation settings use the owner's ID as _id, not a userId field.
        await settings.DeleteManyAsync(Owner(userId), cancellationToken).ConfigureAwait(false);
        await usage.DeleteManyAsync(DataOwner(userId), cancellationToken).ConfigureAwait(false);
        // Keep the recovery record until every owned collection has been erased successfully.
        await users.DeleteOneAsync(pendingOwner, cancellationToken).ConfigureAwait(false);
    }

    private static BsonDocument Owner(Guid userId)
    {
        if (userId == Guid.Empty)
        {
            throw new ArgumentException("An account ID is required.", nameof(userId));
        }
        return new BsonDocument("_id", userId.ToString("D"));
    }

    private static BsonDocument ActiveOwner(Guid userId) => Owner(userId).Add("deletionRequestedAtUtc", BsonNull.Value);
    private static BsonDocument DataOwner(Guid userId) => new("userId", Owner(userId)["_id"]);
    private static PendingAccountDeletion ToPending(BsonDocument document) => new(Guid.Parse(document["_id"].AsString), document["providerUserId"].AsString);
}
