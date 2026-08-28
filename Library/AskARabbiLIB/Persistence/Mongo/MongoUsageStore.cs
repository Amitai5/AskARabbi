using System.Globalization;
using AskARabbiLIB.Usage;
using MongoDB.Driver;

namespace AskARabbiLIB.Persistence.Mongo;

/// <summary>Stores monthly answer counters in Azure Cosmos DB for MongoDB.</summary>
public sealed class MongoUsageStore : IUsageStore
{
    private readonly IMongoCollection<MongoUsageDocument> collection;

    /// <summary>Initializes a MongoDB usage store.</summary>
    /// <param name="database">MongoDB database.</param>
    /// <param name="options">Collection configuration.</param>
    public MongoUsageStore(IMongoDatabase database, MongoDatabaseOptions options)
    {
        ArgumentNullException.ThrowIfNull(database);
        ArgumentNullException.ThrowIfNull(options);
        collection = database.GetCollection<MongoUsageDocument>(options.UsageCollectionName);
    }

    /// <inheritdoc/>
    public async Task<int> GetAnswerCountAsync(Guid userId, DateTimeOffset periodStartUtc, DateTimeOffset periodEndUtc, CancellationToken cancellationToken = default)
    {
        var id = CreateId(userId, periodStartUtc);
        var document = await collection.Find(item => item.Id == id && item.PeriodEndUtc == periodEndUtc.UtcDateTime).FirstOrDefaultAsync(cancellationToken).ConfigureAwait(false);
        return document?.AnswerCount ?? 0;
    }

    /// <inheritdoc/>
    public async Task<int> IncrementAnswerCountAsync(Guid userId, DateTimeOffset periodStartUtc, DateTimeOffset periodEndUtc, CancellationToken cancellationToken = default)
    {
        var id = CreateId(userId, periodStartUtc);
        var filter = Builders<MongoUsageDocument>.Filter.Eq(document => document.Id, id);
        var update = Builders<MongoUsageDocument>.Update
            .SetOnInsert(document => document.UserId, userId.ToString("D"))
            .SetOnInsert(document => document.PeriodStartUtc, periodStartUtc.UtcDateTime)
            .SetOnInsert(document => document.PeriodEndUtc, periodEndUtc.UtcDateTime)
            .Inc(document => document.AnswerCount, 1);
        var document = await collection.FindOneAndUpdateAsync(filter, update, new FindOneAndUpdateOptions<MongoUsageDocument>
        {
            IsUpsert = true,
            ReturnDocument = ReturnDocument.After,
        }, cancellationToken).ConfigureAwait(false);
        return document.AnswerCount;
    }

    private static string CreateId(Guid userId, DateTimeOffset periodStartUtc) => string.Create(CultureInfo.InvariantCulture, $"{userId:D}:{periodStartUtc.UtcDateTime:yyyyMM}");
}
