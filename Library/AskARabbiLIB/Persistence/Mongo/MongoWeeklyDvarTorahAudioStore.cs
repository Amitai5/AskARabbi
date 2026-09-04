using AskARabbiLIB.DvarTorah;
using AskARabbiLIB.DvarTorah.Audio;
using MongoDB.Driver;

namespace AskARabbiLIB.Persistence.Mongo;

/// <summary>Maintains narration-only leases and metadata on the existing publication document.</summary>
public sealed class MongoWeeklyDvarTorahAudioStore : IWeeklyDvarTorahAudioStore
{
    private readonly Func<FilterDefinition<MongoWeeklyDvarTorahDocument>, UpdateDefinition<MongoWeeklyDvarTorahDocument>, CancellationToken, Task<UpdateResult>> updateDocument;

    /// <summary>Initializes the narration persistence boundary.</summary>
    /// <param name="database">MongoDB database.</param>
    /// <param name="options">Existing publication collection configuration.</param>
    public MongoWeeklyDvarTorahAudioStore(IMongoDatabase database, MongoDatabaseOptions options)
    {
        ArgumentNullException.ThrowIfNull(database);
        ArgumentNullException.ThrowIfNull(options);
        var collection = database.GetCollection<MongoWeeklyDvarTorahDocument>(options.DvarTorahCollectionName);
        updateDocument = (filter, update, cancellationToken) => collection.UpdateOneAsync(filter, update, cancellationToken: cancellationToken);
    }

    internal MongoWeeklyDvarTorahAudioStore(Func<FilterDefinition<MongoWeeklyDvarTorahDocument>, UpdateDefinition<MongoWeeklyDvarTorahDocument>, CancellationToken, Task<UpdateResult>> updateDocument) => this.updateDocument = updateDocument;

    /// <inheritdoc/>
    public async Task<WeeklyDvarTorahAudioLease?> TryAcquireAudioLeaseAsync(WeeklyDvarTorahArticle article, string version, string leaseId, DateTimeOffset acquiredAtUtc, DateTimeOffset expiresAtUtc, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(article);
        DvarTorahAudioValidation.ValidateVersion(version);
        ArgumentException.ThrowIfNullOrWhiteSpace(leaseId);
        if (leaseId.Length > 160 || expiresAtUtc <= acquiredAtUtc)
        {
            throw new ArgumentException("A bounded invocation identifier and a future lease expiration are required.", nameof(leaseId));
        }
        var filter = CreateAcquireFilter(article, version, acquiredAtUtc);
        var update = Builders<MongoWeeklyDvarTorahDocument>.Update
            .Set(document => document.AudioLeaseId, leaseId)
            .Set(document => document.AudioLeaseVersion, version)
            .Set(document => document.AudioLeaseExpiresAtUtc, expiresAtUtc.UtcDateTime)
            .Set(document => document.AudioFailureCode, null);
        var result = await updateDocument(filter, update, cancellationToken).ConfigureAwait(false);
        return result.MatchedCount == 1 ? new WeeklyDvarTorahAudioLease(article.Week.WeekKey, version, leaseId, expiresAtUtc.ToUniversalTime()) : null;
    }

    /// <inheritdoc/>
    public async Task<bool> PublishAudioAsync(WeeklyDvarTorahAudioLease lease, WeeklyDvarTorahArticle article, WeeklyDvarTorahAudioMetadata audio, DateTimeOffset publishedAtUtc, CancellationToken cancellationToken = default)
    {
        ValidateLease(lease);
        ArgumentNullException.ThrowIfNull(article);
        ArgumentNullException.ThrowIfNull(audio);
        if (article.Week.WeekKey != lease.WeekKey || audio.Version != lease.Version || audio.AudioLength <= 0 || audio.DurationMs <= 0 || !double.IsFinite(audio.DurationMs))
        {
            throw new ArgumentException("Audio and article must match the owned narration version.", nameof(audio));
        }
        var filter = CreateOwnedFilter(lease) & CreateArticleFilter(article)
            & Builders<MongoWeeklyDvarTorahDocument>.Filter.Gt(document => document.AudioLeaseExpiresAtUtc, publishedAtUtc.UtcDateTime);
        var update = Builders<MongoWeeklyDvarTorahDocument>.Update
            .Set(document => document.Audio, MongoWeeklyDvarTorahAudioDocument.FromDomain(audio))
            .Set(document => document.AudioLeaseId, null)
            .Set(document => document.AudioLeaseVersion, null)
            .Set(document => document.AudioLeaseExpiresAtUtc, null)
            .Set(document => document.AudioFailureCode, null);
        var result = await updateDocument(filter, update, cancellationToken).ConfigureAwait(false);
        return result.MatchedCount == 1;
    }

    /// <inheritdoc/>
    public async Task RecordAudioFailureAsync(WeeklyDvarTorahAudioLease lease, string failureCode, DateTimeOffset failedAtUtc, CancellationToken cancellationToken = default)
    {
        ValidateLease(lease);
        ArgumentException.ThrowIfNullOrWhiteSpace(failureCode);
        if (failureCode.Length > 120)
        {
            throw new ArgumentException("A safe bounded failure code is required.", nameof(failureCode));
        }
        var update = Builders<MongoWeeklyDvarTorahDocument>.Update
            .Set(document => document.AudioLeaseId, null)
            .Set(document => document.AudioLeaseVersion, null)
            .Set(document => document.AudioLeaseExpiresAtUtc, null)
            .Set(document => document.AudioFailureCode, failureCode);
        await updateDocument(CreateOwnedFilter(lease), update, cancellationToken).ConfigureAwait(false);
    }

    internal static FilterDefinition<MongoWeeklyDvarTorahDocument> CreateAcquireFilter(WeeklyDvarTorahArticle article, string version, DateTimeOffset acquiredAtUtc)
    {
        var filter = Builders<MongoWeeklyDvarTorahDocument>.Filter;
        return CreateArticleFilter(article) & filter.Ne("audio.version", version)
            & (filter.Eq(document => document.AudioLeaseExpiresAtUtc, null) | filter.Lte(document => document.AudioLeaseExpiresAtUtc, acquiredAtUtc.UtcDateTime));
    }

    internal static FilterDefinition<MongoWeeklyDvarTorahDocument> CreateArticleFilter(WeeklyDvarTorahArticle article)
    {
        var filter = Builders<MongoWeeklyDvarTorahDocument>.Filter;
        return filter.Eq(document => document.Id, article.Week.WeekKey)
            & filter.Eq(document => document.Status, MongoWeeklyDvarTorahStore.PublishedStatus)
            & filter.Eq(document => document.Title, article.Title)
            & filter.Eq(document => document.Body, article.Body)
            & filter.Eq(document => document.PublishedAtUtc, article.PublishedAtUtc.UtcDateTime);
    }

    internal static FilterDefinition<MongoWeeklyDvarTorahDocument> CreateOwnedFilter(WeeklyDvarTorahAudioLease lease)
    {
        var filter = Builders<MongoWeeklyDvarTorahDocument>.Filter;
        return filter.Eq(document => document.Id, lease.WeekKey)
            & filter.Eq(document => document.Status, MongoWeeklyDvarTorahStore.PublishedStatus)
            & filter.Eq(document => document.AudioLeaseId, lease.LeaseId)
            & filter.Eq(document => document.AudioLeaseVersion, lease.Version)
            & filter.Eq(document => document.AudioLeaseExpiresAtUtc, lease.ExpiresAtUtc.UtcDateTime);
    }

    private static void ValidateLease(WeeklyDvarTorahAudioLease lease)
    {
        ArgumentNullException.ThrowIfNull(lease);
        DvarTorahAudioValidation.GetPrefix(lease.WeekKey, lease.Version);
        ArgumentException.ThrowIfNullOrWhiteSpace(lease.LeaseId);
    }
}
