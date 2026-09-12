using AskARabbiLIB.Accounts;
using MongoDB.Bson;
using MongoDB.Driver;

namespace AskARabbiLIB.Persistence.Mongo;

/// <summary>Serializes admission decisions with a single-document compare-and-swap, without cross-collection transactions.</summary>
public sealed class MongoAccountRegistrationStore : IAccountRegistrationStore
{
    private const string StateId = "account-registration";
    private readonly IMongoCollection<BsonDocument> state;
    private readonly IMongoCollection<BsonDocument> users;

    /// <summary>Initializes durable public signup admission.</summary>
    /// <param name="database">Application database.</param>
    /// <param name="options">Existing collection configuration.</param>
    public MongoAccountRegistrationStore(IMongoDatabase database, MongoDatabaseOptions options)
    {
        ArgumentNullException.ThrowIfNull(database);
        ArgumentNullException.ThrowIfNull(options);
        state = database.GetCollection<BsonDocument>(options.RegistrationCollectionName);
        users = database.GetCollection<BsonDocument>(options.UsersCollectionName);
    }

    /// <inheritdoc/>
    public async Task<AccountRegistrationSnapshot> ReadAsync(string? providerUserId, CancellationToken cancellationToken = default)
    {
        var document = await state.Find(Owner()).FirstOrDefaultAsync(cancellationToken).ConfigureAwait(false);
        if (document is null)
        {
            // Bootstrap once before public signup is enabled and after old API revisions are drained.
            // All subsequent decisions use ONLY the singleton, not cross-collection count reads.
            var existing = await users.Find(FilterDefinition<BsonDocument>.Empty)
                .Project(new BsonDocument { { "providerUserId", 1 }, { "_id", 0 } })
                .ToListAsync(cancellationToken).ConfigureAwait(false);
            var identities = existing.Select(value => value["providerUserId"].AsString).Distinct(StringComparer.Ordinal);
            try
            {
                await state.InsertOneAsync(new BsonDocument { { "_id", StateId }, { "revision", 0L }, { "accounts", new BsonArray(identities) }, { "reservations", new BsonArray() } }, cancellationToken: cancellationToken).ConfigureAwait(false);
            }
            catch (MongoWriteException exception) when (exception.WriteError.Category == ServerErrorCategory.DuplicateKey)
            {
                // Another replica initialized the same singleton; its state is authoritative.
            }
            document = await state.Find(Owner()).FirstOrDefaultAsync(cancellationToken).ConfigureAwait(false)
                ?? throw new PersistenceUnavailableException();
        }

        var occupied = document["accounts"].AsBsonArray.Select(value => value.AsString)
            .Concat(document["reservations"].AsBsonArray.Select(value => value["providerUserId"].AsString))
            .ToHashSet(StringComparer.Ordinal);
        return new(document["revision"].ToInt64(), occupied.Count, providerUserId is not null && occupied.Contains(providerUserId));
    }

    /// <inheritdoc/>
    public async Task<bool> TryReserveAsync(long revision, Guid operationId, string providerUserId, CancellationToken cancellationToken = default)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(revision);
        ArgumentException.ThrowIfNullOrWhiteSpace(providerUserId);
        ValidateOperation(operationId);
        var filter = Owner().Add("revision", revision);
        var reservation = new BsonDocument { { "id", operationId.ToString("D") }, { "providerUserId", providerUserId } };
        var update = new BsonDocument { { "$inc", new BsonDocument("revision", 1L) }, { "$push", new BsonDocument("reservations", reservation) } };
        var result = await state.UpdateOneAsync(filter, update, cancellationToken: cancellationToken).ConfigureAwait(false);
        return result.MatchedCount == 1;
    }

    /// <inheritdoc/>
    public Task CompleteAsync(Guid operationId, string providerUserId, CancellationToken cancellationToken = default)
    {
        ValidateOperation(operationId);
        ArgumentException.ThrowIfNullOrWhiteSpace(providerUserId);
        return state.UpdateOneAsync(Owner(), new BsonDocument
        {
            { "$inc", new BsonDocument("revision", 1L) },
            { "$addToSet", new BsonDocument("accounts", providerUserId) },
            { "$pull", new BsonDocument("reservations", new BsonDocument("id", operationId.ToString("D"))) },
        }, cancellationToken: cancellationToken);
    }

    /// <summary>Releases the registered place after owned data is erased, preserving in-flight reservations.</summary>
    /// <param name="providerUserId">Identity whose data was erased.</param>
    /// <param name="cancellationToken">Token that can cancel the operation.</param>
    /// <returns>A task representing the idempotent release.</returns>
    public Task ReleaseAccountAsync(string providerUserId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(providerUserId);
        // Never upsert here: deletion before the first admission snapshot needs no ledger migration.
        return state.UpdateOneAsync(Owner(), new BsonDocument
        {
            { "$inc", new BsonDocument("revision", 1L) },
            { "$pull", new BsonDocument("accounts", providerUserId) },
        }, cancellationToken: cancellationToken);
    }

    private static BsonDocument Owner() => new("_id", StateId);

    private static void ValidateOperation(Guid operationId)
    {
        if (operationId == Guid.Empty)
        {
            throw new ArgumentException("A registration operation ID is required.", nameof(operationId));
        }
    }
}
