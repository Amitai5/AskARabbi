using System.Reflection;
using AskARabbiLIB.Lexicon;
using AskARabbiLIB.Persistence.Mongo;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using MongoDB.Bson;
using MongoDB.Bson.Serialization;
using MongoDB.Driver;

namespace AskARabbiLIB.Tests;

[TestClass]
public sealed class MongoLexiconStoreTests
{
    private static RenderArgs<MongoLexiconEntryDocument> RenderArgs => new(BsonSerializer.SerializerRegistry.GetSerializer<MongoLexiconEntryDocument>(), BsonSerializer.SerializerRegistry);

    [TestMethod]
    public void CreateIndexes_CosmosCompatibility_UsesSingleFieldIndexesWithoutTextOrCompoundArrays()
    {
        var indexes = MongoLexiconStore.CreateIndexes();

        var keys = indexes.Select(index => index.Keys.Render(RenderArgs)).ToArray();

        Assert.HasCount(3, indexes);
        CollectionAssert.AreEquivalent(new[] { "dictionaryId", "revision", "lookupKeys" }, keys.Select(key => key.GetElement(0).Name).ToArray());
        Assert.IsTrue(keys.All(key => key.ElementCount == 1 && key.GetElement(0).Value == 1));
    }

    [TestMethod]
    public void CreateSearchFilter_MultiwordQuery_UsesAllKeysAndPinsDictionaryEdition()
    {
        var filter = MongoLexiconStore.CreateSearchFilter("bdb", "reviewed-revision", LexiconSearchText.QueryKeys("read and call"));

        var bson = filter.Render(RenderArgs);

        Assert.AreEqual("bdb", bson["dictionaryId"].AsString);
        Assert.AreEqual("reviewed-revision", bson["revision"].AsString);
        CollectionAssert.AreEqual(new[] { "en:read", "en:call" }, bson["lookupKeys"]["$all"].AsBsonArray.Select(value => value.AsString).ToArray());
        Assert.IsFalse(bson.ToJson().Contains("$regex", StringComparison.Ordinal));
    }

    [TestMethod]
    public void Serialization_DictionaryArticle_RoundTripsAllProvenance()
    {
        var document = new MongoLexiconEntryDocument { Id = "bdb:revision:BDB00001", DictionaryId = "bdb", Revision = "revision", LookupKeys = ["head:קרא"], Entry = LexiconTestData.Entry() };

        var bson = document.ToBsonDocument();
        var restored = BsonSerializer.Deserialize<MongoLexiconEntryDocument>(bson);

        Assert.AreEqual(document.Entry, restored.Entry);
        CollectionAssert.Contains(bson.Names.ToArray(), "lookupKeys");
        Assert.AreEqual("bdb:revision:BDB00001", bson["_id"].AsString);
    }

    [TestMethod]
    public async Task ImportBdbAsync_InterruptedBatch_DoesNotPublishAndCanResumeWithoutDeletingData()
    {
        var database = new RecordingDatabase { FailWrites = true };
        var store = database.Store();

        await Assert.ThrowsAsync<InvalidOperationException>(() => store.ImportBdbAsync([LexiconTestData.Entry()]));

        Assert.IsNull(database.Manifest?.PublishedFingerprint);
        Assert.IsNotNull(database.Manifest?.ImportFingerprint);
        database.FailWrites = false;
        await store.ImportBdbAsync([LexiconTestData.Entry()]);
        Assert.IsNotNull(database.Manifest?.PublishedFingerprint);
        Assert.AreEqual(1, database.Manifest.EntryCount);
        Assert.AreEqual("publish", database.Operations[^1]);
    }

    [TestMethod]
    public async Task ImportBdbAsync_IdenticalRerun_DoesNotDuplicateOrRewriteArticles()
    {
        var database = new RecordingDatabase();
        var store = database.Store();
        await store.ImportBdbAsync([LexiconTestData.Entry()]);
        var writes = database.Operations.Count(operation => operation == "batch");

        var count = await store.ImportBdbAsync([LexiconTestData.Entry()]);

        Assert.AreEqual(1, count);
        Assert.AreEqual(writes, database.Operations.Count(operation => operation == "batch"));
    }

    [TestMethod]
    public async Task ImportBdbAsync_ChangedPublishedRevision_RejectsOverwrite()
    {
        var database = new RecordingDatabase();
        var store = database.Store();
        await store.ImportBdbAsync([LexiconTestData.Entry()]);

        await Assert.ThrowsAsync<InvalidOperationException>(() => store.ImportBdbAsync([LexiconTestData.Entry() with { SourceSha256 = new string('b', 64) }]));

        Assert.AreEqual(1, database.Operations.Count(operation => operation == "publish"));
    }

    [TestMethod]
    public async Task ImportBdbAsync_DifferentPendingContent_RejectsBeforeWritingAnotherBatch()
    {
        var database = new RecordingDatabase { FailWrites = true };
        var store = database.Store();
        await Assert.ThrowsAsync<InvalidOperationException>(() => store.ImportBdbAsync([LexiconTestData.Entry()]));
        database.FailWrites = false;

        await Assert.ThrowsAsync<InvalidOperationException>(() => store.ImportBdbAsync([LexiconTestData.Entry() with { Text = "Altered text with the same upstream file hash." }]));

        Assert.IsNull(database.Manifest?.PublishedFingerprint);
        Assert.AreEqual(1, database.Operations.Count(operation => operation == "batch"));
    }

    [TestMethod]
    public async Task ImportBdbAsync_InvalidLicense_RejectsBeforeDatabaseCalls()
    {
        var database = new RecordingDatabase();

        await Assert.ThrowsAsync<ArgumentException>(() => database.Store().ImportBdbAsync([LexiconTestData.Entry() with { License = "unknown" }]));

        Assert.HasCount(0, database.Operations);
    }

    [TestMethod]
    public async Task SearchAsync_UnpublishedEdition_DoesNotReadPartialArticles()
    {
        var database = new RecordingDatabase();

        await Assert.ThrowsAsync<InvalidOperationException>(() => database.Store().SearchAsync("bdb", BdbCorpus.Revision, "read", 3));

        Assert.HasCount(1, database.Operations);
        Assert.AreEqual("manifest-read", database.Operations[0]);
    }

    [TestMethod]
    public async Task SearchAsync_ExactHeadword_DoesNotFillResultsWithPassingMentions()
    {
        var database = new RecordingDatabase();
        var article = Document(LexiconTestData.Entry());
        database.ReadResults.Enqueue([PublishedManifest()]);
        database.ReadResults.Enqueue([article]);

        var found = await database.Store().SearchAsync("bdb", BdbCorpus.Revision, "קרא", 3);

        Assert.HasCount(1, found);
        Assert.AreEqual(article.Entry, found[0]);
        Assert.HasCount(2, database.ReadFilters);
        Assert.AreEqual("head:קרא", database.ReadFilters[1]["lookupKeys"]["$all"][0].AsString);
    }

    [TestMethod]
    public async Task SearchAsync_EnglishMeaning_RanksParagraphOpeningsBeforeOtherArticleMentions()
    {
        var database = new RecordingDatabase();
        var definition = Document(LexiconTestData.Entry() with { EntryId = "BDB00002" });
        var mention = Document(LexiconTestData.Entry() with { EntryId = "BDB00001" });
        database.ReadResults.Enqueue([PublishedManifest()]);
        database.ReadResults.Enqueue([]);
        database.ReadResults.Enqueue([definition]);
        database.ReadResults.Enqueue([mention]);

        var found = await database.Store().SearchAsync("bdb", BdbCorpus.Revision, "read", 3);

        CollectionAssert.AreEqual(new[] { "BDB00002", "BDB00001" }, found.Select(entry => entry.EntryId).ToArray());
        Assert.AreEqual("lead:en:read", database.ReadFilters[2]["lookupKeys"]["$all"][0].AsString);
        Assert.AreEqual("en:read", database.ReadFilters[3]["lookupKeys"]["$all"][0].AsString);
        CollectionAssert.Contains(database.ReadFilters[3]["_id"]["$nin"].AsBsonArray.Select(value => value.AsString).ToArray(), definition.Id);
    }

    private static MongoLexiconEntryDocument PublishedManifest() => new() { Id = "manifest", DictionaryId = "bdb", Revision = BdbCorpus.Revision, PublishedFingerprint = "validated", EntryCount = 1 };

    private static MongoLexiconEntryDocument Document(LexiconEntry entry) => new() { Id = $"{entry.DictionaryId}:{entry.Revision}:{entry.EntryId}", DictionaryId = entry.DictionaryId, Revision = entry.Revision, Entry = entry, LookupKeys = LexiconSearchText.CreateKeys(entry) };

    [TestMethod]
    [DataRow(true)]
    [DataRow(false)]
    public async Task FindAsync_PublishedEdition_UsesScopedPointReadAndReturnsOnlyTheRequestedArticle(bool exists)
    {
        var database = new RecordingDatabase();
        var article = LexiconTestData.Entry();
        database.ReadResults.Enqueue([PublishedManifest()]);
        database.ReadResults.Enqueue(exists ? [Document(article)] : []);

        var found = await database.Store().FindAsync("bdb", BdbCorpus.Revision, article.EntryId);

        Assert.AreEqual(exists ? article : null, found);
        Assert.AreEqual($"bdb:{BdbCorpus.Revision}:{article.EntryId}", database.ReadFilters[1]["_id"].AsString);
    }

    [TestMethod]
    public async Task SearchAsync_LowercaseArticleId_UsesCanonicalIdentifierInsteadOfWordSearch()
    {
        var database = new RecordingDatabase();
        database.ReadResults.Enqueue([PublishedManifest()]);
        database.ReadResults.Enqueue([Document(LexiconTestData.Entry())]);

        var found = await database.Store().SearchAsync("bdb", BdbCorpus.Revision, "bdb00001", 3);

        Assert.HasCount(1, found);
        Assert.AreEqual($"bdb:{BdbCorpus.Revision}:BDB00001", database.ReadFilters[1]["_id"].AsString);
        Assert.HasCount(2, database.ReadFilters);
    }

    [TestMethod]
    public async Task FindAsync_UnsafeIdentifier_DoesNotQueryDatabase()
    {
        var database = new RecordingDatabase();

        await Assert.ThrowsAsync<ArgumentException>(() => database.Store().FindAsync("bdb", BdbCorpus.Revision, "../BDB00001"));

        Assert.HasCount(0, database.ReadFilters);
    }

    [TestMethod]
    [DataRow(0)]
    [DataRow(6)]
    public async Task SearchAsync_InvalidLimit_RejectsBeforeDatabaseReads(int limit)
    {
        var database = new RecordingDatabase();

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => database.Store().SearchAsync("bdb", BdbCorpus.Revision, "read", limit));

        Assert.HasCount(0, database.Operations);
    }

    private sealed class RecordingDatabase
    {
        internal List<string> Operations { get; } = [];
        internal Queue<IReadOnlyList<MongoLexiconEntryDocument>> ReadResults { get; } = new();
        internal List<BsonDocument> ReadFilters { get; } = [];
        internal bool FailWrites { get; set; }
        internal MongoLexiconEntryDocument? Manifest { get; private set; }

        internal MongoLexiconStore Store()
        {
            var indexes = Proxy.Create<IMongoIndexManager<MongoLexiconEntryDocument>>((method, args) =>
            {
                Assert.AreEqual("CreateManyAsync", method.Name);
                Operations.Add("indexes");
                return Task.FromResult<IEnumerable<string>>(((IEnumerable<CreateIndexModel<MongoLexiconEntryDocument>>)args[0]!).Select(index => index.Options.Name).ToArray());
            });
            var collection = Proxy.Create<IMongoCollection<MongoLexiconEntryDocument>>((method, args) =>
            {
                switch (method.Name)
                {
                    case "get_DocumentSerializer": return RenderArgs.DocumentSerializer;
                    case "get_Indexes": return indexes;
                    case "FindAsync":
                        Operations.Add("manifest-read");
                        ReadFilters.Add(((FilterDefinition<MongoLexiconEntryDocument>)args[0]!).Render(RenderArgs));
                        return Task.FromResult<IAsyncCursor<MongoLexiconEntryDocument>>(new Cursor(ReadResults.TryDequeue(out var result) ? result : Manifest is null ? [] : [Manifest]));
                    case "BulkWriteAsync":
                        Operations.Add("batch");
                        if (FailWrites)
                        {
                            throw new InvalidOperationException("Simulated interrupted import.");
                        }
                        var writes = ((IEnumerable<WriteModel<MongoLexiconEntryDocument>>)args[0]!).ToArray();
                        Assert.IsTrue(writes.Cast<ReplaceOneModel<MongoLexiconEntryDocument>>().All(write => write.IsUpsert));
                        return Task.FromResult<BulkWriteResult<MongoLexiconEntryDocument>>(new BulkWriteResult<MongoLexiconEntryDocument>.Unacknowledged(writes.Length, writes));
                    case "ReplaceOneAsync":
                        Operations.Add("publish");
                        Manifest = (MongoLexiconEntryDocument)args[1]!;
                        return Task.FromResult<ReplaceOneResult>(new ReplaceOneResult.Acknowledged(1, 1, null));
                    case "UpdateOneAsync":
                        Operations.Add("reserve");
                        var update = ((UpdateDefinition<MongoLexiconEntryDocument>)args[1]!).Render(RenderArgs);
                        Manifest ??= new MongoLexiconEntryDocument { Id = "manifest", DictionaryId = "bdb", Revision = BdbCorpus.Revision, ImportFingerprint = update["$setOnInsert"]["importFingerprint"].AsString };
                        return Task.FromResult<UpdateResult>(new UpdateResult.Acknowledged(1, 0, null));
                    default: throw new AssertFailedException("Unexpected Mongo operation: " + method.Name);
                }
            });
            var database = Proxy.Create<IMongoDatabase>((method, args) =>
            {
                if (method.Name == "GetCollection")
                {
                    Assert.AreEqual("lexiconEntries", args[0]);
                    return collection;
                }
                Assert.AreEqual("CreateCollectionAsync", method.Name);
                Operations.Add("collection");
                return Task.CompletedTask;
            });
            return new MongoLexiconStore(database, new MongoDatabaseOptions());
        }
    }

    private sealed class Cursor(IReadOnlyList<MongoLexiconEntryDocument> documents) : IAsyncCursor<MongoLexiconEntryDocument>
    {
        private bool moved;
        public IEnumerable<MongoLexiconEntryDocument> Current => documents;
        public void Dispose() { }
        public bool MoveNext(CancellationToken cancellationToken = default)
        {
            var result = !moved;
            moved = true;
            return result;
        }
        public Task<bool> MoveNextAsync(CancellationToken cancellationToken = default) => Task.FromResult(MoveNext(cancellationToken));
    }

    public class Proxy : DispatchProxy
    {
        private Func<MethodInfo, object?[], object?>? handler;
        internal static T Create<T>(Func<MethodInfo, object?[], object?> handler) where T : class
        {
            var instance = Create<T, Proxy>();
            ((Proxy)(object)instance).handler = handler;
            return instance;
        }
        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args) => handler?.Invoke(targetMethod ?? throw new InvalidOperationException("Missing method."), args ?? []);
    }
}
