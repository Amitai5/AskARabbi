using AskARabbiLIB.DvarTorah;
using MongoDB.Driver;

namespace AskARabbiLIB.Persistence.Mongo;

/// <summary>Stores idempotently generated weekly Dvar Torah publications in MongoDB.</summary>
public sealed class MongoWeeklyDvarTorahStore : IWeeklyDvarTorahGenerationStore
{
    internal const string GeneratingStatus = "Generating";
    internal const string PublishedStatus = "Published";
    internal const string FailedStatus = "Failed";
    private readonly IMongoCollection<MongoWeeklyDvarTorahDocument> collection;

    /// <summary>Initializes a MongoDB weekly Dvar Torah store.</summary>
    /// <param name="database">MongoDB database.</param>
    /// <param name="options">Collection configuration.</param>
    public MongoWeeklyDvarTorahStore(IMongoDatabase database, MongoDatabaseOptions options)
    {
        ArgumentNullException.ThrowIfNull(database);
        ArgumentNullException.ThrowIfNull(options);
        collection = database.GetCollection<MongoWeeklyDvarTorahDocument>(options.DvarTorahCollectionName);
    }

    /// <inheritdoc/>
    public async Task<WeeklyDvarTorahArticle?> GetPublishedAsync(WeeklyDvarTorahWeek week, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(week);
        var filter = Builders<MongoWeeklyDvarTorahDocument>.Filter.Eq(document => document.Id, week.WeekKey)
            & Builders<MongoWeeklyDvarTorahDocument>.Filter.Eq(document => document.Status, PublishedStatus);
        var document = await collection.Find(filter).FirstOrDefaultAsync(cancellationToken).ConfigureAwait(false);
        return document is null ? null : ToDomain(document);
    }

    /// <inheritdoc/>
    public async Task<WeeklyDvarTorahArticle?> GetLatestPublishedAsync(bool inIsrael, DateOnly notAfter, CancellationToken cancellationToken = default)
    {
        var filter = Builders<MongoWeeklyDvarTorahDocument>.Filter.Eq(document => document.InIsrael, inIsrael)
            & Builders<MongoWeeklyDvarTorahDocument>.Filter.Eq(document => document.Status, PublishedStatus)
            & Builders<MongoWeeklyDvarTorahDocument>.Filter.Lte(document => document.ShabbatDate, notAfter);
        var document = await collection.Find(filter).Sort(CreateLatestPublishedSort()).Limit(1).FirstOrDefaultAsync(cancellationToken).ConfigureAwait(false);
        return document is null ? null : ToDomain(document);
    }

    /// <inheritdoc/>
    public async Task<WeeklyDvarTorahGenerationLease?> TryAcquireGenerationLeaseAsync(WeeklyDvarTorahWeek week, string leaseId, DateTimeOffset acquiredAtUtc, DateTimeOffset expiresAtUtc, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(week);
        if (string.IsNullOrWhiteSpace(leaseId))
        {
            throw new ArgumentException("A generation lease ID is required.", nameof(leaseId));
        }
        if (expiresAtUtc <= acquiredAtUtc)
        {
            throw new ArgumentException("The generation lease must expire after it is acquired.", nameof(expiresAtUtc));
        }

        var normalizedLeaseId = leaseId.Trim();
        if (normalizedLeaseId.Length > 160)
        {
            throw new ArgumentException("The generation lease ID cannot exceed 160 characters.", nameof(leaseId));
        }

        if (await TryTakeExistingLeaseAsync(week, normalizedLeaseId, acquiredAtUtc, expiresAtUtc, cancellationToken).ConfigureAwait(false))
        {
            return new WeeklyDvarTorahGenerationLease(week, normalizedLeaseId, expiresAtUtc.ToUniversalTime());
        }

        var document = new MongoWeeklyDvarTorahDocument
        {
            Id = week.WeekKey,
            ShabbatDate = week.ShabbatDate,
            HebrewDate = week.HebrewDate,
            Parashah = week.Parashah,
            Holiday = week.Holiday,
            InIsrael = week.InIsrael,
            Status = GeneratingStatus,
            GenerationLeaseId = normalizedLeaseId,
            GenerationLeaseExpiresAtUtc = expiresAtUtc.UtcDateTime,
            GenerationAttemptCount = 1,
            LastAttemptedAtUtc = acquiredAtUtc.UtcDateTime,
            UpdatedAtUtc = acquiredAtUtc.UtcDateTime,
        };

        try
        {
            await collection.InsertOneAsync(document, cancellationToken: cancellationToken).ConfigureAwait(false);
            return new WeeklyDvarTorahGenerationLease(week, normalizedLeaseId, expiresAtUtc.ToUniversalTime());
        }
        catch (MongoWriteException exception) when (exception.WriteError?.Category == ServerErrorCategory.DuplicateKey)
        {
            var acquired = await TryTakeExistingLeaseAsync(week, normalizedLeaseId, acquiredAtUtc, expiresAtUtc, cancellationToken).ConfigureAwait(false);
            return acquired ? new WeeklyDvarTorahGenerationLease(week, normalizedLeaseId, expiresAtUtc.ToUniversalTime()) : null;
        }
    }

    /// <inheritdoc/>
    public async Task<bool> PublishAsync(WeeklyDvarTorahGenerationLease lease, WeeklyDvarTorahArticle article, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(lease);
        ArgumentNullException.ThrowIfNull(article);
        if (article.Week.WeekKey != lease.Week.WeekKey)
        {
            throw new ArgumentException("The article does not belong to the leased reading week.", nameof(article));
        }

        var filter = CreateActiveOwnedLeaseFilter(lease, article.PublishedAtUtc);
        var update = Builders<MongoWeeklyDvarTorahDocument>.Update
            .Set(document => document.Status, PublishedStatus)
            .Set(document => document.Title, article.Title)
            .Set(document => document.Body, article.Body)
            .Set(document => document.GeneratorVersion, article.GeneratorVersion)
            .Set(document => document.GeneratedAtUtc, article.GeneratedAtUtc.UtcDateTime)
            .Set(document => document.PublishedAtUtc, article.PublishedAtUtc.UtcDateTime)
            .Set(document => document.GenerationLeaseId, null)
            .Set(document => document.GenerationLeaseExpiresAtUtc, null)
            .Set(document => document.FailureCode, null)
            .Set(document => document.UpdatedAtUtc, article.PublishedAtUtc.UtcDateTime);
        var result = await collection.UpdateOneAsync(filter, update, cancellationToken: cancellationToken).ConfigureAwait(false);
        return result.MatchedCount == 1;
    }

    /// <inheritdoc/>
    public async Task RecordGenerationFailureAsync(WeeklyDvarTorahGenerationLease lease, string failureCode, DateTimeOffset failedAtUtc, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(lease);
        if (string.IsNullOrWhiteSpace(failureCode))
        {
            throw new ArgumentException("A safe failure code is required.", nameof(failureCode));
        }

        var normalizedFailureCode = failureCode.Trim();
        if (normalizedFailureCode.Length > 120)
        {
            throw new ArgumentException("The failure code cannot exceed 120 characters.", nameof(failureCode));
        }

        var update = Builders<MongoWeeklyDvarTorahDocument>.Update
            .Set(document => document.Status, FailedStatus)
            .Set(document => document.GenerationLeaseId, null)
            .Set(document => document.GenerationLeaseExpiresAtUtc, null)
            .Set(document => document.FailureCode, normalizedFailureCode)
            .Set(document => document.UpdatedAtUtc, failedAtUtc.UtcDateTime);
        await collection.UpdateOneAsync(CreateOwnedLeaseFilter(lease), update, cancellationToken: cancellationToken).ConfigureAwait(false);
    }

    internal static CreateIndexModel<MongoWeeklyDvarTorahDocument> CreateLatestPublishedIndex()
    {
        var keys = Builders<MongoWeeklyDvarTorahDocument>.IndexKeys
            .Ascending(document => document.InIsrael)
            .Ascending(document => document.Status)
            .Descending(document => document.ShabbatDate);
        return new CreateIndexModel<MongoWeeklyDvarTorahDocument>(keys, new CreateIndexOptions { Name = "ix_weeklyDvarTorah_inIsrael_status_shabbatDate" });
    }

    internal static SortDefinition<MongoWeeklyDvarTorahDocument> CreateLatestPublishedSort() => Builders<MongoWeeklyDvarTorahDocument>.Sort.Descending(document => document.ShabbatDate);

    internal static FilterDefinition<MongoWeeklyDvarTorahDocument> CreateOwnedLeaseFilter(WeeklyDvarTorahGenerationLease lease)
    {
        ArgumentNullException.ThrowIfNull(lease);
        return Builders<MongoWeeklyDvarTorahDocument>.Filter.Eq(document => document.Id, lease.Week.WeekKey)
            & Builders<MongoWeeklyDvarTorahDocument>.Filter.Eq(document => document.Status, GeneratingStatus)
            & Builders<MongoWeeklyDvarTorahDocument>.Filter.Eq(document => document.GenerationLeaseId, lease.LeaseId)
            & Builders<MongoWeeklyDvarTorahDocument>.Filter.Eq(document => document.GenerationLeaseExpiresAtUtc, lease.ExpiresAtUtc.UtcDateTime);
    }

    internal static FilterDefinition<MongoWeeklyDvarTorahDocument> CreateActiveOwnedLeaseFilter(WeeklyDvarTorahGenerationLease lease, DateTimeOffset publishedAtUtc)
    {
        return CreateOwnedLeaseFilter(lease)
            & Builders<MongoWeeklyDvarTorahDocument>.Filter.Gt(document => document.GenerationLeaseExpiresAtUtc, publishedAtUtc.UtcDateTime);
    }

    private async Task<bool> TryTakeExistingLeaseAsync(WeeklyDvarTorahWeek week, string leaseId, DateTimeOffset acquiredAtUtc, DateTimeOffset expiresAtUtc, CancellationToken cancellationToken)
    {
        var filter = Builders<MongoWeeklyDvarTorahDocument>.Filter.Eq(document => document.Id, week.WeekKey)
            & Builders<MongoWeeklyDvarTorahDocument>.Filter.Ne(document => document.Status, PublishedStatus)
            & (Builders<MongoWeeklyDvarTorahDocument>.Filter.Eq(document => document.GenerationLeaseExpiresAtUtc, null)
                | Builders<MongoWeeklyDvarTorahDocument>.Filter.Lte(document => document.GenerationLeaseExpiresAtUtc, acquiredAtUtc.UtcDateTime));
        var update = Builders<MongoWeeklyDvarTorahDocument>.Update
            .Set(document => document.ShabbatDate, week.ShabbatDate)
            .Set(document => document.HebrewDate, week.HebrewDate)
            .Set(document => document.Parashah, week.Parashah)
            .Set(document => document.Holiday, week.Holiday)
            .Set(document => document.InIsrael, week.InIsrael)
            .Set(document => document.Status, GeneratingStatus)
            .Set(document => document.GenerationLeaseId, leaseId)
            .Set(document => document.GenerationLeaseExpiresAtUtc, expiresAtUtc.UtcDateTime)
            .Set(document => document.LastAttemptedAtUtc, acquiredAtUtc.UtcDateTime)
            .Set(document => document.FailureCode, null)
            .Set(document => document.UpdatedAtUtc, acquiredAtUtc.UtcDateTime)
            .Inc(document => document.GenerationAttemptCount, 1);
        var result = await collection.UpdateOneAsync(filter, update, cancellationToken: cancellationToken).ConfigureAwait(false);
        return result.MatchedCount == 1;
    }

    private static WeeklyDvarTorahArticle ToDomain(MongoWeeklyDvarTorahDocument document)
    {
        if (document.Status != PublishedStatus || document.Title is null || document.Body is null || document.GeneratorVersion is null || document.GeneratedAtUtc is null || document.PublishedAtUtc is null)
        {
            throw new InvalidOperationException($"Weekly Dvar Torah document '{document.Id}' is not a complete publication.");
        }

        var week = new WeeklyDvarTorahWeek(document.ShabbatDate, document.HebrewDate, document.Parashah, document.Holiday, document.InIsrael);
        if (!string.Equals(document.Id, week.WeekKey, StringComparison.Ordinal))
        {
            throw new InvalidOperationException($"Weekly Dvar Torah document '{document.Id}' does not match its reading week.");
        }

        return new WeeklyDvarTorahArticle(week, document.Title, document.Body, document.GeneratorVersion, AsUtc(document.GeneratedAtUtc.Value), AsUtc(document.PublishedAtUtc.Value));
    }

    private static DateTimeOffset AsUtc(DateTime value) => new(DateTime.SpecifyKind(value, DateTimeKind.Utc));
}
