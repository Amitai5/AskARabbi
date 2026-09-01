using AskARabbiLIB.Persistence.Mongo;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using MongoDB.Bson;
using MongoDB.Bson.Serialization;
using MongoDB.Driver;

namespace AskARabbiLIB.Tests;

[TestClass]
public sealed class MongoWeeklyDvarTorahTests
{
    [TestMethod]
    [TestCategory("Unit")]
    public void MongoDatabaseOptions_DefaultDvarTorahCollection_UsesProductionCollectionName()
    {
        var options = new MongoDatabaseOptions();

        Assert.AreEqual("WeeklyAIDvarTorahs", options.DvarTorahCollectionName);
    }

    [TestMethod]
    [TestCategory("Unit")]
    public void WeeklyDocument_ShabbatDate_RoundTripsAsSortableInvariantString()
    {
        var document = new MongoWeeklyDvarTorahDocument
        {
            Id = "diaspora:2026-09-05",
            ShabbatDate = new DateOnly(2026, 9, 5),
            HebrewDate = "23 Elul, 5786",
            Parashah = "Nitzavim-Vayeilech",
            InIsrael = false,
            Status = MongoWeeklyDvarTorahStore.PublishedStatus,
            Title = "A weekly teaching",
            Body = "Body",
            GeneratorVersion = "test-v1",
            GeneratedAtUtc = new DateTime(2026, 8, 31, 18, 0, 0, DateTimeKind.Utc),
            PublishedAtUtc = new DateTime(2026, 8, 31, 18, 0, 0, DateTimeKind.Utc),
            LastAttemptedAtUtc = new DateTime(2026, 8, 31, 17, 0, 0, DateTimeKind.Utc),
            UpdatedAtUtc = new DateTime(2026, 8, 31, 18, 0, 0, DateTimeKind.Utc),
        };

        var bson = document.ToBsonDocument();
        var result = BsonSerializer.Deserialize<MongoWeeklyDvarTorahDocument>(bson);

        Assert.AreEqual("2026-09-05", bson["shabbatDate"].AsString);
        Assert.AreEqual(document.ShabbatDate, result.ShabbatDate);
        Assert.AreEqual(document.Body, result.Body);
    }

    [TestMethod]
    [TestCategory("Unit")]
    public void CreateLatestPublishedIndex_LatestQuery_UsesFilterFieldsBeforeDescendingDate()
    {
        var registry = BsonSerializer.SerializerRegistry;
        var serializer = registry.GetSerializer<MongoWeeklyDvarTorahDocument>();

        var index = MongoWeeklyDvarTorahStore.CreateLatestPublishedIndex();
        var keys = index.Keys.Render(new RenderArgs<MongoWeeklyDvarTorahDocument>(serializer, registry));
        var sort = MongoWeeklyDvarTorahStore.CreateLatestPublishedSort().Render(new RenderArgs<MongoWeeklyDvarTorahDocument>(serializer, registry));

        CollectionAssert.AreEqual(new[] { "inIsrael", "status", "shabbatDate" }, keys.Names.ToArray());
        Assert.AreEqual(-1, keys["shabbatDate"].AsInt32);
        CollectionAssert.AreEqual(new[] { "shabbatDate" }, sort.Names.ToArray());
        Assert.AreEqual(-1, sort["shabbatDate"].AsInt32);
    }

    [TestMethod]
    [TestCategory("Unit")]
    public void CreateActiveOwnedLeaseFilter_RequiresExactUnexpiredLease()
    {
        var registry = BsonSerializer.SerializerRegistry;
        var serializer = registry.GetSerializer<MongoWeeklyDvarTorahDocument>();
        var week = new AskARabbiLIB.DvarTorah.WeeklyDvarTorahWeek(new DateOnly(2026, 9, 5), "23 Elul, 5786", "Nitzavim", null, false);
        var expiration = new DateTimeOffset(2026, 8, 31, 18, 30, 0, TimeSpan.Zero);
        var lease = new AskARabbiLIB.DvarTorah.WeeklyDvarTorahGenerationLease(week, "lease-1", expiration);

        var filter = MongoWeeklyDvarTorahStore.CreateActiveOwnedLeaseFilter(lease, expiration.AddTicks(-1));
        var bson = filter.Render(new RenderArgs<MongoWeeklyDvarTorahDocument>(serializer, registry));
        var serialized = bson.ToJson();

        StringAssert.Contains(serialized, week.WeekKey);
        StringAssert.Contains(serialized, MongoWeeklyDvarTorahStore.GeneratingStatus);
        StringAssert.Contains(serialized, "lease-1");
        StringAssert.Contains(serialized, "\"generationLeaseExpiresAtUtc\"");
        StringAssert.Contains(serialized, "\"$gt\"");
    }

    [TestMethod]
    [TestCategory("Unit")]
    public async Task PublicWriteBoundaries_InvalidLeaseOrArticle_ThrowBeforeDatabaseIo()
    {
        var store = CreateStore();
        var week = new AskARabbiLIB.DvarTorah.WeeklyDvarTorahWeek(new DateOnly(2026, 9, 5), "23 Elul, 5786", "Nitzavim", null, false);
        var lease = new AskARabbiLIB.DvarTorah.WeeklyDvarTorahGenerationLease(week, "lease-1", new DateTimeOffset(2026, 8, 31, 18, 30, 0, TimeSpan.Zero));
        var article = new AskARabbiLIB.DvarTorah.WeeklyDvarTorahArticle(week, "Title", "Body", "test-v1", new DateTimeOffset(2026, 8, 31, 18, 0, 0, TimeSpan.Zero), new DateTimeOffset(2026, 8, 31, 18, 0, 0, TimeSpan.Zero));
        var acquiredAtUtc = new DateTimeOffset(2026, 8, 31, 18, 0, 0, TimeSpan.Zero);

        await Assert.ThrowsExactlyAsync<ArgumentNullException>(() => store.GetPublishedAsync(null!));
        await Assert.ThrowsExactlyAsync<ArgumentNullException>(() => store.TryAcquireGenerationLeaseAsync(null!, "lease", DateTimeOffset.UtcNow, DateTimeOffset.UtcNow.AddMinutes(1)));
        await Assert.ThrowsExactlyAsync<ArgumentException>(() => store.TryAcquireGenerationLeaseAsync(week, " ", DateTimeOffset.UtcNow, DateTimeOffset.UtcNow.AddMinutes(1)));
        await Assert.ThrowsExactlyAsync<ArgumentException>(() => store.TryAcquireGenerationLeaseAsync(week, new string('x', 161), DateTimeOffset.UtcNow, DateTimeOffset.UtcNow.AddMinutes(1)));
        await Assert.ThrowsExactlyAsync<ArgumentException>(() => store.TryAcquireGenerationLeaseAsync(week, "lease", acquiredAtUtc, acquiredAtUtc));
        await Assert.ThrowsExactlyAsync<ArgumentNullException>(() => store.PublishAsync(null!, article));
        await Assert.ThrowsExactlyAsync<ArgumentNullException>(() => store.PublishAsync(lease, null!));
        await Assert.ThrowsExactlyAsync<ArgumentNullException>(() => store.RecordGenerationFailureAsync(null!, "Failure", DateTimeOffset.UtcNow));
        await Assert.ThrowsExactlyAsync<ArgumentException>(() => store.RecordGenerationFailureAsync(lease, " ", DateTimeOffset.UtcNow));
        await Assert.ThrowsExactlyAsync<ArgumentException>(() => store.RecordGenerationFailureAsync(lease, new string('x', 121), DateTimeOffset.UtcNow));
    }

    [TestMethod]
    [TestCategory("Unit")]
    public async Task Publish_ArticleForDifferentWeek_ThrowsBeforeDatabaseIo()
    {
        var store = CreateStore();
        var leasedWeek = new AskARabbiLIB.DvarTorah.WeeklyDvarTorahWeek(new DateOnly(2026, 9, 5), "23 Elul, 5786", "Nitzavim", null, false);
        var articleWeek = new AskARabbiLIB.DvarTorah.WeeklyDvarTorahWeek(new DateOnly(2026, 9, 12), "30 Elul, 5786", "Vayeilech", null, false);
        var lease = new AskARabbiLIB.DvarTorah.WeeklyDvarTorahGenerationLease(leasedWeek, "lease-1", DateTimeOffset.UtcNow.AddMinutes(30));
        var article = new AskARabbiLIB.DvarTorah.WeeklyDvarTorahArticle(articleWeek, "Title", "Body", "test-v1", DateTimeOffset.UtcNow, DateTimeOffset.UtcNow);

        await Assert.ThrowsExactlyAsync<ArgumentException>(() => store.PublishAsync(lease, article));
    }

    private static MongoWeeklyDvarTorahStore CreateStore()
    {
        var client = new MongoClient("mongodb://localhost:27017");
        return new MongoWeeklyDvarTorahStore(client.GetDatabase("askarabbi-tests"), new MongoDatabaseOptions());
    }
}
