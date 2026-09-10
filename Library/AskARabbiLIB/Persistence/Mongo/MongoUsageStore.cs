using System.Globalization;
using AskARabbiLIB.Usage;
using MongoDB.Driver;

namespace AskARabbiLIB.Persistence.Mongo;

/// <summary>Stores atomic monthly token counters and chat admission leases in MongoDB.</summary>
public sealed class MongoUsageStore : IUsageStore
{
    private readonly Func<FilterDefinition<MongoUsageDocument>, CancellationToken, Task<MongoUsageDocument?>> findOne;
    private readonly Func<FilterDefinition<MongoUsageDocument>, UpdateDefinition<MongoUsageDocument>, CancellationToken, Task<UpdateResult>> updateOne;
    private readonly Func<MongoUsageDocument, CancellationToken, Task> insertOne;

    /// <summary>Initializes a MongoDB usage store.</summary>
    /// <param name="database">MongoDB database.</param>
    /// <param name="options">Collection configuration.</param>
    public MongoUsageStore(IMongoDatabase database, MongoDatabaseOptions options)
    {
        ArgumentNullException.ThrowIfNull(database);
        ArgumentNullException.ThrowIfNull(options);
        var collection = database.GetCollection<MongoUsageDocument>(options.UsageCollectionName);
        findOne = async (filter, token) => await collection.Find(filter).FirstOrDefaultAsync(token).ConfigureAwait(false);
        updateOne = (filter, update, token) => collection.UpdateOneAsync(filter, update, cancellationToken: token);
        insertOne = (document, token) => collection.InsertOneAsync(document, cancellationToken: token);
    }

    // Test the exact Mongo filters and compare-and-set retries without a live database.
    internal MongoUsageStore(Func<FilterDefinition<MongoUsageDocument>, CancellationToken, Task<MongoUsageDocument?>> findOne, Func<FilterDefinition<MongoUsageDocument>, UpdateDefinition<MongoUsageDocument>, CancellationToken, Task<UpdateResult>> updateOne, Func<MongoUsageDocument, CancellationToken, Task> insertOne)
    {
        this.findOne = findOne ?? throw new ArgumentNullException(nameof(findOne));
        this.updateOne = updateOne ?? throw new ArgumentNullException(nameof(updateOne));
        this.insertOne = insertOne ?? throw new ArgumentNullException(nameof(insertOne));
    }

    /// <inheritdoc/>
    public async Task<long> GetTokenCountAsync(Guid userId, DateTimeOffset periodStartUtc, DateTimeOffset periodEndUtc, CancellationToken cancellationToken = default)
    {
        var id = CreateId(userId, periodStartUtc);
        var filter = Builders<MongoUsageDocument>.Filter.Eq(item => item.Id, id) & Builders<MongoUsageDocument>.Filter.Eq(item => item.PeriodEndUtc, periodEndUtc.UtcDateTime);
        var document = await findOne(filter, cancellationToken).ConfigureAwait(false);
        return document?.TokenCount ?? 0;
    }

    /// <inheritdoc/>
    public async Task<bool> TryAcquireChatAsync(ChatUsageLease lease, DateTimeOffset now, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(lease);
        var update = Builders<MongoUsageDocument>.Update
            .Set(document => document.ChatLeaseId, lease.Id.ToString("D"))
            .Set(document => document.ChatLeaseExpiresAtUtc, lease.ExpiresAtUtc.UtcDateTime)
            .Set(document => document.ChatLeaseTokens, 0);
        var result = await updateOne(CreateAcquireFilter(lease, now), update, cancellationToken).ConfigureAwait(false);
        if (result.MatchedCount == 1)
        {
            return true;
        }

        try
        {
            await insertOne(new MongoUsageDocument
            {
                Id = CreateId(lease.UserId, lease.PeriodStartUtc),
                UserId = lease.UserId.ToString("D"),
                PeriodStartUtc = lease.PeriodStartUtc.UtcDateTime,
                PeriodEndUtc = lease.PeriodEndUtc.UtcDateTime,
                ChatLeaseId = lease.Id.ToString("D"),
                ChatLeaseExpiresAtUtc = lease.ExpiresAtUtc.UtcDateTime,
            }, cancellationToken).ConfigureAwait(false);
            return true;
        }
        catch (MongoWriteException exception) when (exception.WriteError?.Category == ServerErrorCategory.DuplicateKey)
        {
            // The unique monthly _id prevents a concurrent first request from bypassing admission.
            return false;
        }
    }

    /// <inheritdoc/>
    public async Task<bool> RecordTokensAsync(ChatUsageLease lease, long cumulativeTokens, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(lease);
        ArgumentOutOfRangeException.ThrowIfNegative(cumulativeTokens);
        var owned = CreateOwnedFilter(lease);
        while (true)
        {
            var current = await findOne(owned, cancellationToken).ConfigureAwait(false);
            if (current is null)
            {
                return false;
            }
            if (cumulativeTokens <= current.ChatLeaseTokens)
            {
                return true;
            }

            var filter = owned & Builders<MongoUsageDocument>.Filter.Eq(document => document.ChatLeaseTokens, current.ChatLeaseTokens);
            var update = Builders<MongoUsageDocument>.Update
                .Inc(document => document.TokenCount, cumulativeTokens - current.ChatLeaseTokens)
                .Set(document => document.ChatLeaseTokens, cumulativeTokens);
            var result = await updateOne(filter, update, cancellationToken).ConfigureAwait(false);
            if (result.MatchedCount == 1)
            {
                return true;
            }
        }
    }

    /// <inheritdoc/>
    public async Task ReleaseChatAsync(ChatUsageLease lease, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(lease);
        var update = Builders<MongoUsageDocument>.Update
            .Set(document => document.ChatLeaseId, null)
            .Set(document => document.ChatLeaseExpiresAtUtc, null);
        await updateOne(CreateOwnedFilter(lease), update, cancellationToken).ConfigureAwait(false);
    }

    internal static FilterDefinition<MongoUsageDocument> CreateAcquireFilter(ChatUsageLease lease, DateTimeOffset now)
    {
        var filter = Builders<MongoUsageDocument>.Filter;
        return filter.Eq(document => document.Id, CreateId(lease.UserId, lease.PeriodStartUtc))
            & filter.Eq(document => document.PeriodEndUtc, lease.PeriodEndUtc.UtcDateTime)
            & (filter.Exists(document => document.TokenCount, false) | filter.Lt(document => document.TokenCount, lease.TokenLimit))
            & (filter.Eq(document => document.ChatLeaseExpiresAtUtc, null) | filter.Lte(document => document.ChatLeaseExpiresAtUtc, now.UtcDateTime));
    }

    internal static FilterDefinition<MongoUsageDocument> CreateOwnedFilter(ChatUsageLease lease) => Builders<MongoUsageDocument>.Filter.Eq(document => document.Id, CreateId(lease.UserId, lease.PeriodStartUtc))
        & Builders<MongoUsageDocument>.Filter.Eq(document => document.ChatLeaseId, lease.Id.ToString("D"));

    private static string CreateId(Guid userId, DateTimeOffset periodStartUtc) => string.Create(CultureInfo.InvariantCulture, $"{userId:D}:{periodStartUtc.UtcDateTime:yyyyMM}");
}
