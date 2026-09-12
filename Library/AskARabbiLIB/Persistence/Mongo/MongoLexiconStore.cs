using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using AskARabbiLIB.Lexicon;
using MongoDB.Driver;

namespace AskARabbiLIB.Persistence.Mongo;

/// <summary>Stores versioned dictionary articles with Cosmos-compatible, exact multikey search indexes.</summary>
public sealed class MongoLexiconStore : ILexiconStore
{
    private readonly IMongoDatabase database;
    private readonly IMongoCollection<MongoLexiconEntryDocument> entries;
    private readonly string collectionName;

    /// <summary>Initializes dictionary persistence without creating or modifying collections.</summary>
    /// <param name="database">Application MongoDB database.</param>
    /// <param name="options">Collection configuration.</param>
    public MongoLexiconStore(IMongoDatabase database, MongoDatabaseOptions options)
    {
        this.database = database ?? throw new ArgumentNullException(nameof(database));
        ArgumentNullException.ThrowIfNull(options);
        collectionName = options.LexiconCollectionName;
        ArgumentException.ThrowIfNullOrWhiteSpace(collectionName);
        entries = database.GetCollection<MongoLexiconEntryDocument>(collectionName);
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<LexiconEntry>> SearchAsync(string dictionaryId, string revision, string query, int limit, CancellationToken cancellationToken = default)
    {
        ValidateScope(dictionaryId, revision);
        ArgumentException.ThrowIfNullOrWhiteSpace(query);
        if (limit is < 1 or > 5)
        {
            throw new ArgumentOutOfRangeException(nameof(limit), "Return between one and five dictionary articles.");
        }
        query = query.Trim();
        if (Regex.IsMatch(query, @"^(BDB\d+|ABBR\.\d+)$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant, TimeSpan.FromSeconds(1)))
        {
            var article = await FindAsync(dictionaryId, revision, query.ToUpperInvariant(), cancellationToken).ConfigureAwait(false);
            return article is null ? [] : [article];
        }
        var keys = LexiconSearchText.QueryKeys(query);
        await RequirePublishedAsync(dictionaryId, revision, cancellationToken).ConfigureAwait(false);
        var exact = await ReadAsync(CreateSearchFilter(dictionaryId, revision, ["head:" + LexiconSearchText.HeadwordKey(query)]), limit, cancellationToken).ConfigureAwait(false);
        if (exact.Count > 0)
        {
            return exact;
        }
        var leads = await ReadAsync(CreateSearchFilter(dictionaryId, revision, keys.Select(key => "lead:" + key).ToArray()), limit, cancellationToken).ConfigureAwait(false);
        if (leads.Count == limit)
        {
            return leads;
        }
        var filter = CreateSearchFilter(dictionaryId, revision, keys);
        if (leads.Count > 0)
        {
            filter &= Builders<MongoLexiconEntryDocument>.Filter.Nin(entry => entry.Id, leads.Select(entry => DocumentId(dictionaryId, revision, entry.EntryId)));
        }
        var related = await ReadAsync(filter, limit - leads.Count, cancellationToken).ConfigureAwait(false);
        return [.. leads, .. related];
    }

    /// <inheritdoc/>
    public async Task<LexiconEntry?> FindAsync(string dictionaryId, string revision, string entryId, CancellationToken cancellationToken = default)
    {
        ValidateScope(dictionaryId, revision);
        ArgumentException.ThrowIfNullOrWhiteSpace(entryId);
        if (entryId.Length > 80 || !Regex.IsMatch(entryId, @"^[A-Za-z0-9._-]+$", RegexOptions.CultureInvariant, TimeSpan.FromSeconds(1)))
        {
            throw new ArgumentException("Invalid dictionary article identifier.", nameof(entryId));
        }
        await RequirePublishedAsync(dictionaryId, revision, cancellationToken).ConfigureAwait(false);
        return (await entries.Find(entry => entry.Id == DocumentId(dictionaryId, revision, entryId)).FirstOrDefaultAsync(cancellationToken).ConfigureAwait(false))?.Entry;
    }

    /// <summary>Validates and imports the approved BDB revision, publishing it only after every batch succeeds; reruns are idempotent.</summary>
    /// <param name="articles">All validated original articles in the edition.</param>
    /// <param name="cancellationToken">Operation cancellation.</param>
    /// <returns>The number of articles in the published edition.</returns>
    public async Task<int> ImportBdbAsync(IReadOnlyList<LexiconEntry> articles, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(articles);
        if (articles.Count == 0 || articles.Any(article => article.DictionaryId != BdbCorpus.DictionaryId || article.Revision != BdbCorpus.Revision || article.License != BdbCorpus.License || string.IsNullOrWhiteSpace(article.EntryId) || string.IsNullOrWhiteSpace(article.Text)) || articles.Select(article => article.EntryId).Distinct(StringComparer.Ordinal).Count() != articles.Count)
        {
            throw new ArgumentException("Import requires unique, nonempty articles from the approved BDB revision and license.", nameof(articles));
        }
        cancellationToken.ThrowIfCancellationRequested();
        var fingerprint = CreateImportFingerprint(articles);
        var manifestId = DocumentId(BdbCorpus.DictionaryId, BdbCorpus.Revision, "manifest");
        await EnsureIndexesAsync(cancellationToken).ConfigureAwait(false);
        var previous = await entries.Find(entry => entry.Id == manifestId).FirstOrDefaultAsync(cancellationToken).ConfigureAwait(false);
        if (previous?.PublishedFingerprint is { } published)
        {
            if (published != fingerprint || previous.EntryCount != articles.Count)
            {
                throw new InvalidOperationException("This BDB revision is already published with different content. Use a new reviewed revision; do not overwrite a live dictionary.");
            }
            return articles.Count;
        }
        // Reserve the content identity before any batches, so concurrent or interrupted imports
        // cannot mix two different representations under the same immutable edition.
        var reservation = Builders<MongoLexiconEntryDocument>.Update
            .SetOnInsert(entry => entry.DictionaryId, BdbCorpus.DictionaryId)
            .SetOnInsert(entry => entry.Revision, BdbCorpus.Revision)
            .SetOnInsert(entry => entry.ImportFingerprint, fingerprint);
        await entries.UpdateOneAsync(entry => entry.Id == manifestId, reservation, new UpdateOptions { IsUpsert = true }, cancellationToken).ConfigureAwait(false);
        var pending = await entries.Find(entry => entry.Id == manifestId).FirstOrDefaultAsync(cancellationToken).ConfigureAwait(false);
        if (pending?.ImportFingerprint != fingerprint)
        {
            throw new InvalidOperationException("A different import already reserved this dictionary revision. Resume the original import or use a new reviewed revision.");
        }
        foreach (var batch in articles.Chunk(100))
        {
            var writes = batch.Select(article => new ReplaceOneModel<MongoLexiconEntryDocument>(Builders<MongoLexiconEntryDocument>.Filter.Eq(entry => entry.Id, DocumentId(article.DictionaryId, article.Revision, article.EntryId)), new MongoLexiconEntryDocument
            {
                Id = DocumentId(article.DictionaryId, article.Revision, article.EntryId),
                DictionaryId = article.DictionaryId,
                Revision = article.Revision,
                Entry = article,
                LookupKeys = LexiconSearchText.CreateKeys(article),
            }) { IsUpsert = true }).ToArray();
            await entries.BulkWriteAsync(writes, new BulkWriteOptions { IsOrdered = true }, cancellationToken).ConfigureAwait(false);
        }
        var manifest = new MongoLexiconEntryDocument { Id = manifestId, DictionaryId = BdbCorpus.DictionaryId, Revision = BdbCorpus.Revision, ImportFingerprint = fingerprint, PublishedFingerprint = fingerprint, EntryCount = articles.Count };
        await entries.ReplaceOneAsync(entry => entry.Id == manifestId, manifest, new ReplaceOptions { IsUpsert = true }, cancellationToken).ConfigureAwait(false);
        return articles.Count;
    }

    /// <summary>Creates the dedicated collection and supported indexes during explicit import, not API startup.</summary>
    /// <param name="cancellationToken">Operation cancellation.</param>
    /// <returns>A task representing index creation.</returns>
    public async Task EnsureIndexesAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            await database.CreateCollectionAsync(collectionName, cancellationToken: cancellationToken).ConfigureAwait(false);
        }
        catch (MongoCommandException exception) when (exception.Code == 48 || exception.CodeName == "NamespaceExists")
        {
            // An existing collection is expected on repeat imports; other failures must remain visible.
        }
        await entries.Indexes.CreateManyAsync(CreateIndexes(), cancellationToken).ConfigureAwait(false);
    }

    internal static IReadOnlyList<CreateIndexModel<MongoLexiconEntryDocument>> CreateIndexes() =>
    [
        new(Builders<MongoLexiconEntryDocument>.IndexKeys.Ascending(entry => entry.DictionaryId), new CreateIndexOptions { Name = "ix_lexicon_dictionaryId" }),
        new(Builders<MongoLexiconEntryDocument>.IndexKeys.Ascending(entry => entry.Revision), new CreateIndexOptions { Name = "ix_lexicon_revision" }),
        new(Builders<MongoLexiconEntryDocument>.IndexKeys.Ascending(entry => entry.LookupKeys), new CreateIndexOptions { Name = "ix_lexicon_lookupKeys" }),
    ];

    internal static FilterDefinition<MongoLexiconEntryDocument> CreateSearchFilter(string dictionaryId, string revision, IReadOnlyList<string> keys) => Builders<MongoLexiconEntryDocument>.Filter.Eq(entry => entry.DictionaryId, dictionaryId) & Builders<MongoLexiconEntryDocument>.Filter.Eq(entry => entry.Revision, revision) & Builders<MongoLexiconEntryDocument>.Filter.All(entry => entry.LookupKeys, keys);

    private async Task<IReadOnlyList<LexiconEntry>> ReadAsync(FilterDefinition<MongoLexiconEntryDocument> filter, int limit, CancellationToken cancellationToken)
    {
        var documents = await entries.Find(filter, new FindOptions { MaxTime = TimeSpan.FromSeconds(5) }).SortBy(entry => entry.Id).Limit(limit).ToListAsync(cancellationToken).ConfigureAwait(false);
        return documents.Select(document => document.Entry ?? throw new InvalidDataException("An indexed dictionary record is missing its article.")).ToArray();
    }

    private async Task RequirePublishedAsync(string dictionaryId, string revision, CancellationToken cancellationToken)
    {
        var manifest = await entries.Find(entry => entry.Id == DocumentId(dictionaryId, revision, "manifest")).FirstOrDefaultAsync(cancellationToken).ConfigureAwait(false);
        if (manifest?.PublishedFingerprint is null)
        {
            throw new InvalidOperationException("This dictionary edition has not finished importing. Do not infer that the requested word does not exist.");
        }
    }

    private static string DocumentId(string dictionaryId, string revision, string entryId) => $"{dictionaryId}:{revision}:{entryId}";

    private static string CreateImportFingerprint(IReadOnlyList<LexiconEntry> articles)
    {
        using var fingerprint = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        fingerprint.AppendData(Encoding.UTF8.GetBytes("AskARabbiLexiconIndex/v1\n"));
        foreach (var article in articles.OrderBy(article => article.EntryId, StringComparer.Ordinal))
        {
            fingerprint.AppendData(JsonSerializer.SerializeToUtf8Bytes(article));
            fingerprint.AppendData("\n"u8);
        }
        return Convert.ToHexStringLower(fingerprint.GetHashAndReset());
    }

    private static void ValidateScope(string dictionaryId, string revision)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(dictionaryId);
        ArgumentException.ThrowIfNullOrWhiteSpace(revision);
        if (dictionaryId.Length > 40 || revision.Length > 80 || dictionaryId.Contains(':') || revision.Contains(':'))
        {
            throw new ArgumentException("Invalid dictionary scope.");
        }
    }
}
