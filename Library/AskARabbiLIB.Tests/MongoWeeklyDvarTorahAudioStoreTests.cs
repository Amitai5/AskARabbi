using AskARabbiLIB.DvarTorah.Audio;
using AskARabbiLIB.DvarTorah;
using AskARabbiLIB.Persistence.Mongo;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using MongoDB.Bson;
using MongoDB.Bson.Serialization;
using MongoDB.Driver;

namespace AskARabbiLIB.Tests;

[TestClass]
public sealed class MongoWeeklyDvarTorahAudioStoreTests
{
    [TestMethod]
    [TestCategory("Unit")]
    public void CreateAcquireFilter_AtomicAudioLease_RequiresPublishedExactContentAndMissingVersion()
    {
        var article = DvarTorahAudioTestData.Article();
        var version = DvarTorahAudioTestData.Timings().Version;

        var filter = MongoWeeklyDvarTorahAudioStore.CreateAcquireFilter(article, version, DvarTorahAudioTestData.Now);
        var bson = Render(filter);

        Assert.AreEqual(article.Week.WeekKey, bson["_id"].AsString);
        Assert.AreEqual("Published", bson["status"].AsString);
        Assert.AreEqual(article.Title, bson["title"].AsString);
        Assert.AreEqual(article.Body, bson["body"].AsString);
        Assert.AreEqual(version, bson["audio.version"]["$ne"].AsString);
        StringAssert.Contains(bson.ToJson(), "audioLeaseExpiresAtUtc");
        StringAssert.Contains(bson.ToJson(), "$lte");
    }

    [TestMethod]
    [TestCategory("Unit")]
    public void CreateOwnedFilter_StaleWorker_RequiresExactLeaseVersionAndExpiration()
    {
        var lease = new WeeklyDvarTorahAudioLease("diaspora:2026-09-05", DvarTorahAudioTestData.Timings().Version, "worker-1", DvarTorahAudioTestData.Now.AddMinutes(30));

        var bson = Render(MongoWeeklyDvarTorahAudioStore.CreateOwnedFilter(lease));

        Assert.AreEqual("Published", bson["status"].AsString);
        Assert.AreEqual(lease.LeaseId, bson["audioLeaseId"].AsString);
        Assert.AreEqual(lease.Version, bson["audioLeaseVersion"].AsString);
        Assert.AreEqual(lease.ExpiresAtUtc.UtcDateTime, bson["audioLeaseExpiresAtUtc"].ToUniversalTime());
    }

    [TestMethod]
    [TestCategory("Unit")]
    public void AudioDocument_Metadata_RoundTripsStablePrivateUriAndUtc()
    {
        var audio = DvarTorahAudioTestData.Metadata();

        var bson = MongoWeeklyDvarTorahAudioDocument.FromDomain(audio).ToBsonDocument();
        var restored = BsonSerializer.Deserialize<MongoWeeklyDvarTorahAudioDocument>(bson).ToDomain();

        Assert.AreEqual(audio, restored);
        Assert.IsTrue(bson.Contains("blobUri"));
        Assert.IsFalse(bson.Contains("BlobUri"));
    }

    [TestMethod]
    [TestCategory("Unit")]
    public void AudioDocument_CorruptOptionalMetadata_DoesNotBlockPublishedText()
    {
        var document = MongoWeeklyDvarTorahAudioDocument.FromDomain(DvarTorahAudioTestData.Metadata());

        Assert.IsNull((document with { DurationMs = double.NaN }).ToDomain());
        Assert.IsNull((document with { BlobUri = "" }).ToDomain());
        Assert.IsNull((document with { AudioLength = 0 }).ToDomain());
        Assert.IsNull((document with { Version = "" }).ToDomain());
        Assert.IsNull((document with { Voice = "" }).ToDomain());
        Assert.IsNull((document with { BlobName = "" }).ToDomain());
        Assert.IsNull((document with { TimingsBlobName = "" }).ToDomain());
        Assert.IsNull((document with { DurationMs = 0 }).ToDomain());
    }

    [TestMethod]
    [TestCategory("Unit")]
    public async Task WriteBoundaries_InvalidArguments_RejectBeforeDatabaseIo()
    {
        var database = new MongoClient("mongodb://127.0.0.1:1").GetDatabase("hermetic-no-io");
        var store = new MongoWeeklyDvarTorahAudioStore(database, new MongoDatabaseOptions());
        var article = DvarTorahAudioTestData.Article();
        var version = DvarTorahAudioTestData.Timings().Version;
        var lease = new WeeklyDvarTorahAudioLease(article.Week.WeekKey, version, "lease", DvarTorahAudioTestData.Now.AddMinutes(30));

        await Assert.ThrowsExactlyAsync<ArgumentNullException>(() => store.TryAcquireAudioLeaseAsync(null!, version, "lease", DvarTorahAudioTestData.Now, DvarTorahAudioTestData.Now.AddMinutes(30)));
        await Assert.ThrowsExactlyAsync<ArgumentException>(() => store.TryAcquireAudioLeaseAsync(article, "wrong", "lease", DvarTorahAudioTestData.Now, DvarTorahAudioTestData.Now.AddMinutes(30)));
        await Assert.ThrowsExactlyAsync<ArgumentException>(() => store.TryAcquireAudioLeaseAsync(article, version, "lease", DvarTorahAudioTestData.Now, DvarTorahAudioTestData.Now));
        await Assert.ThrowsExactlyAsync<ArgumentException>(() => store.PublishAudioAsync(lease, article, DvarTorahAudioTestData.Metadata() with { Version = new string('a', 64) }, DvarTorahAudioTestData.Now));
        await Assert.ThrowsExactlyAsync<ArgumentException>(() => store.RecordAudioFailureAsync(lease, new string('a', 121), DvarTorahAudioTestData.Now));
        await Assert.ThrowsExactlyAsync<ArgumentException>(() => store.TryAcquireAudioLeaseAsync(article, version, new string('l', 161), DvarTorahAudioTestData.Now, DvarTorahAudioTestData.Now.AddMinutes(30)));
        await Assert.ThrowsExactlyAsync<ArgumentException>(() => store.PublishAudioAsync(lease with { WeekKey = "diaspora:2026-09-12" }, article, DvarTorahAudioTestData.Metadata(), DvarTorahAudioTestData.Now));
        await Assert.ThrowsExactlyAsync<ArgumentException>(() => store.PublishAudioAsync(lease, article, DvarTorahAudioTestData.Metadata() with { AudioLength = 0 }, DvarTorahAudioTestData.Now));
        await Assert.ThrowsExactlyAsync<ArgumentException>(() => store.PublishAudioAsync(lease, article, DvarTorahAudioTestData.Metadata() with { DurationMs = 0 }, DvarTorahAudioTestData.Now));
        await Assert.ThrowsExactlyAsync<ArgumentException>(() => store.PublishAudioAsync(lease, article, DvarTorahAudioTestData.Metadata() with { DurationMs = double.NaN }, DvarTorahAudioTestData.Now));
    }

    [TestMethod]
    [DataRow(0L)]
    [DataRow(1L)]
    [TestCategory("Unit")]
    public async Task AudioWrites_ConditionalMatch_OnlyChangesAudioFields(long matchedCount)
    {
        var updates = new List<UpdateDefinition<MongoWeeklyDvarTorahDocument>>();
        var store = new MongoWeeklyDvarTorahAudioStore((_, update, _) =>
        {
            updates.Add(update);
            return Task.FromResult<UpdateResult>(new UpdateResult.Acknowledged(matchedCount, matchedCount, null));
        });
        var article = DvarTorahAudioTestData.Article();
        var version = DvarTorahAudioTestData.Timings().Version;
        var lease = new WeeklyDvarTorahAudioLease(article.Week.WeekKey, version, "worker", DvarTorahAudioTestData.Now.AddMinutes(30));

        var acquired = await store.TryAcquireAudioLeaseAsync(article, version, lease.LeaseId, DvarTorahAudioTestData.Now, lease.ExpiresAtUtc);
        var published = await store.PublishAudioAsync(lease, article, DvarTorahAudioTestData.Metadata(), DvarTorahAudioTestData.Now);
        await store.RecordAudioFailureAsync(lease, "SpeechAuthenticationFailure", DvarTorahAudioTestData.Now);

        Assert.AreEqual(matchedCount == 1, acquired is not null);
        Assert.AreEqual(matchedCount == 1, published);
        Assert.HasCount(3, updates);
        var registry = BsonSerializer.SerializerRegistry;
        var args = new RenderArgs<MongoWeeklyDvarTorahDocument>(registry.GetSerializer<MongoWeeklyDvarTorahDocument>(), registry);
        foreach (var update in updates)
        {
            var fields = update.Render(args)["$set"].AsBsonDocument.Names;
            Assert.IsTrue(fields.All(field => field.StartsWith("audio", StringComparison.Ordinal)), "Audio retries must never change text status, body, publication timestamp, or the text-generation lease.");
        }
    }

    [TestMethod]
    [TestCategory("Unit")]
    public async Task PublicationLookup_InvalidBackfillOrArchiveRequest_RejectsBeforeDatabaseIo()
    {
        var store = new MongoWeeklyDvarTorahStore(new MongoClient("mongodb://127.0.0.1:1").GetDatabase("hermetic-no-io"), new MongoDatabaseOptions());
        var before = new DateOnly(2026, 9, 5);

        await Assert.ThrowsExactlyAsync<ArgumentException>(() => store.GetPublishedByWeekKeyAsync(" "));
        await Assert.ThrowsExactlyAsync<ArgumentException>(() => store.GetPublishedByWeekKeyAsync(new string('a', 65)));
        await Assert.ThrowsExactlyAsync<ArgumentOutOfRangeException>(() => store.SearchPublishedAsync(false, before, null, -1, 10));
        await Assert.ThrowsExactlyAsync<ArgumentOutOfRangeException>(() => store.SearchPublishedAsync(false, before, null, 0, 0));
        await Assert.ThrowsExactlyAsync<ArgumentOutOfRangeException>(() => store.SearchPublishedAsync(false, before, null, 0, WeeklyDvarTorahService.MaximumArchivePageSize + 1));
        await Assert.ThrowsExactlyAsync<ArgumentException>(() => store.SearchPublishedAsync(false, before, new string('s', WeeklyDvarTorahService.MaximumArchiveSearchCharacters + 1), 0, 10));
    }

    [TestMethod]
    [TestCategory("Unit")]
    public void ToDomain_PublicationWithAudio_KeepsTextAndNarrationTogether()
    {
        var article = DvarTorahAudioTestData.Article();
        var document = new MongoWeeklyDvarTorahDocument
        {
            Id = article.Week.WeekKey, ShabbatDate = article.Week.ShabbatDate, HebrewDate = article.Week.HebrewDate, Parashah = article.Week.Parashah,
            Status = "Published", Title = article.Title, Body = article.Body, GeneratorVersion = article.GeneratorVersion,
            GeneratedAtUtc = article.GeneratedAtUtc.UtcDateTime, PublishedAtUtc = article.PublishedAtUtc.UtcDateTime,
            Audio = MongoWeeklyDvarTorahAudioDocument.FromDomain(DvarTorahAudioTestData.Metadata()),
        };

        var restored = MongoWeeklyDvarTorahStore.ToDomain(document);

        Assert.AreEqual(article.Body, restored.Body);
        Assert.AreEqual(DvarTorahAudioTestData.Metadata(), restored.Audio);
    }

    [TestMethod]
    [DataRow("id")]
    [DataRow("hebrewDate")]
    [DataRow("title")]
    [DataRow("publishedAtUtc")]
    [DataRow("wrongWeek")]
    [TestCategory("Unit")]
    public void ToArchiveItem_IncompleteRecordingPublication_RejectsInvalidArticleIdentity(string missing)
    {
        var document = new MongoWeeklyDvarTorahArchiveDocument
        {
            Id = missing == "id" ? null : missing == "wrongWeek" ? "diaspora:2026-09-12" : "diaspora:2026-09-05",
            ShabbatDate = new DateOnly(2026, 9, 5), HebrewDate = missing == "hebrewDate" ? null : "23 Elul 5786", Parashah = "Nitzavim",
            Title = missing == "title" ? null : "Title", PublishedAtUtc = missing == "publishedAtUtc" ? null : DvarTorahAudioTestData.Now.UtcDateTime,
        };

        Assert.ThrowsExactly<InvalidOperationException>(() => MongoWeeklyDvarTorahStore.ToArchiveItem(document));
    }

    [TestMethod]
    [TestCategory("Unit")]
    public void ToArchiveItem_LegacyRecordingPublicationWithoutTags_IsStillReadable()
    {
        var document = new MongoWeeklyDvarTorahArchiveDocument
        {
            Id = "diaspora:2026-09-05", ShabbatDate = new DateOnly(2026, 9, 5), HebrewDate = "23 Elul 5786", Parashah = "Nitzavim",
            Title = "Title", PublishedAtUtc = DvarTorahAudioTestData.Now.UtcDateTime,
        };

        Assert.HasCount(0, MongoWeeklyDvarTorahStore.ToArchiveItem(document).Tags);
    }


    private static BsonDocument Render(FilterDefinition<MongoWeeklyDvarTorahDocument> filter)
    {
        var registry = BsonSerializer.SerializerRegistry;
        return filter.Render(new RenderArgs<MongoWeeklyDvarTorahDocument>(registry.GetSerializer<MongoWeeklyDvarTorahDocument>(), registry));
    }
}
