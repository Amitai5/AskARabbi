using AskARabbiLIB.Accounts;
using MongoDB.Driver;

namespace AskARabbiLIB.Persistence.Mongo;

/// <summary>Stores AskRabbi accounts in an Azure Cosmos DB for MongoDB collection.</summary>
public sealed class MongoUserAccountStore : IUserAccountStore
{
    private readonly IMongoCollection<MongoUserAccountDocument> collection;

    /// <summary>Initializes a MongoDB user-account store.</summary>
    /// <param name="database">MongoDB database.</param>
    /// <param name="options">Collection configuration.</param>
    public MongoUserAccountStore(IMongoDatabase database, MongoDatabaseOptions options)
    {
        ArgumentNullException.ThrowIfNull(database);
        ArgumentNullException.ThrowIfNull(options);
        collection = database.GetCollection<MongoUserAccountDocument>(options.UsersCollectionName);
    }

    /// <inheritdoc/>
    public async Task<UserAccount> UpsertAsync(ExternalUserIdentity identity, DateTimeOffset updatedAtUtc, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(identity);
        if (string.IsNullOrWhiteSpace(identity.ProviderUserId))
        {
            throw new ArgumentException("Provider user ID is required.", nameof(identity));
        }
        if (string.IsNullOrWhiteSpace(identity.Email))
        {
            throw new ArgumentException("Email is required.", nameof(identity));
        }

        var utc = updatedAtUtc.UtcDateTime;
        var filter = Builders<MongoUserAccountDocument>.Filter.Eq(document => document.ProviderUserId, identity.ProviderUserId);
        var update = Builders<MongoUserAccountDocument>.Update
            .SetOnInsert(document => document.Id, Guid.NewGuid().ToString("D"))
            .SetOnInsert(document => document.CreatedAtUtc, utc)
            .Set(document => document.ProviderUserId, identity.ProviderUserId)
            .Set(document => document.Email, identity.Email.Trim())
            .Set(document => document.IsEmailVerified, identity.IsEmailVerified)
            .Set(document => document.FirstName, NormalizeOptional(identity.FirstName))
            .Set(document => document.LastName, NormalizeOptional(identity.LastName))
            .Set(document => document.ProfileImageUrl, NormalizeOptional(identity.ProfileImageUrl))
            .Set(document => document.UpdatedAtUtc, utc);
        var document = await collection.FindOneAndUpdateAsync(filter, update, new FindOneAndUpdateOptions<MongoUserAccountDocument>
        {
            IsUpsert = true,
            ReturnDocument = ReturnDocument.After,
        }, cancellationToken).ConfigureAwait(false);

        return ToDomain(document);
    }

    /// <inheritdoc/>
    public async Task<UserAccount?> GetByIdAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var document = await collection.Find(item => item.Id == userId.ToString("D")).FirstOrDefaultAsync(cancellationToken).ConfigureAwait(false);
        return document is null ? null : ToDomain(document);
    }

    private static UserAccount ToDomain(MongoUserAccountDocument document) => new()
    {
        Id = Guid.Parse(document.Id),
        ProviderUserId = document.ProviderUserId,
        Email = document.Email,
        IsEmailVerified = document.IsEmailVerified,
        FirstName = document.FirstName,
        LastName = document.LastName,
        ProfileImageUrl = document.ProfileImageUrl,
        CreatedAtUtc = AsUtc(document.CreatedAtUtc),
        UpdatedAtUtc = AsUtc(document.UpdatedAtUtc),
    };

    private static DateTimeOffset AsUtc(DateTime value) => new(DateTime.SpecifyKind(value, DateTimeKind.Utc));

    private static string? NormalizeOptional(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
