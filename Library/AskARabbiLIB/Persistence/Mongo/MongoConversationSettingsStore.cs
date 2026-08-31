using AskARabbiLIB.ConversationSettings;
using MongoDB.Driver;

namespace AskARabbiLIB.Persistence.Mongo;

/// <summary>Stores user personalization in Azure Cosmos DB for MongoDB.</summary>
public sealed class MongoConversationSettingsStore : IConversationSettingsStore
{
    private readonly IMongoCollection<MongoConversationSettingsDocument> collection;

    /// <summary>Initializes a MongoDB conversation-settings store.</summary>
    /// <param name="database">MongoDB database.</param>
    /// <param name="options">Collection configuration.</param>
    public MongoConversationSettingsStore(IMongoDatabase database, MongoDatabaseOptions options)
    {
        ArgumentNullException.ThrowIfNull(database);
        ArgumentNullException.ThrowIfNull(options);
        collection = database.GetCollection<MongoConversationSettingsDocument>(options.ConversationSettingsCollectionName);
    }

    /// <inheritdoc/>
    public async Task<PersonalizationSettings?> GetPersonalizationAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var document = await collection.Find(item => item.UserId == userId.ToString("D")).FirstOrDefaultAsync(cancellationToken).ConfigureAwait(false);
        return document?.Personalization is null ? null : ToDomain(document.Personalization);
    }

    /// <inheritdoc/>
    public Task UpsertPersonalizationAsync(Guid userId, PersonalizationSettings personalization, DateTimeOffset updatedAtUtc, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(personalization);
        var userIdValue = userId.ToString("D");
        var update = Builders<MongoConversationSettingsDocument>.Update
            .SetOnInsert(document => document.UserId, userIdValue)
            .Set(document => document.Personalization, ToDocument(personalization))
            .Set(document => document.UpdatedAtUtc, updatedAtUtc.UtcDateTime);
        return collection.UpdateOneAsync(document => document.UserId == userIdValue, update, new UpdateOptions { IsUpsert = true }, cancellationToken);
    }

    /// <inheritdoc/>
    public async Task<ConversationPreferences?> GetPreferencesAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var document = await collection.Find(item => item.UserId == userId.ToString("D")).FirstOrDefaultAsync(cancellationToken).ConfigureAwait(false);
        return document?.Preferences?.ToDomain();
    }

    /// <inheritdoc/>
    public Task UpsertPreferencesAsync(Guid userId, ConversationPreferences preferences, DateTimeOffset updatedAtUtc, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(preferences);
        var userIdValue = userId.ToString("D");
        var document = MongoConversationPreferencesDocument.FromDomain(preferences);
        var update = Builders<MongoConversationSettingsDocument>.Update
            .SetOnInsert(value => value.UserId, userIdValue)
            .Set(value => value.Preferences, document)
            .Set(value => value.UpdatedAtUtc, updatedAtUtc.UtcDateTime);
        return collection.UpdateOneAsync(value => value.UserId == userIdValue, update, new UpdateOptions { IsUpsert = true }, cancellationToken);
    }

    private static MongoPersonalizationDocument ToDocument(PersonalizationSettings personalization) => new()
    {
        FullName = personalization.FullName,
        BirthDate = personalization.BirthDate,
        BirthTime = personalization.BirthTime,
        BirthTimeZone = personalization.BirthTimeZone,
        ConversationLanguage = personalization.ConversationLanguage,
        QuotationLanguage = personalization.QuotationLanguage,
        ReligiousMovement = personalization.ReligiousMovement,
        JewishHeritage = personalization.JewishHeritage,
        AdditionalContext = personalization.AdditionalContext,
    };

    private static PersonalizationSettings ToDomain(MongoPersonalizationDocument document) => new()
    {
        FullName = document.FullName,
        BirthDate = document.BirthDate,
        BirthTime = document.BirthTime,
        BirthTimeZone = document.BirthTimeZone,
        ConversationLanguage = document.ConversationLanguage,
        QuotationLanguage = document.QuotationLanguage,
        ReligiousMovement = document.ReligiousMovement,
        JewishHeritage = document.JewishHeritage,
        AdditionalContext = document.AdditionalContext,
    };
}
