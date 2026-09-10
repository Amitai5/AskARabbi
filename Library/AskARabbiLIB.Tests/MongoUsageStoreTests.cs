using AskARabbiLIB.Persistence.Mongo;
using AskARabbiLIB.Usage;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using MongoDB.Bson;
using MongoDB.Bson.Serialization;
using MongoDB.Driver;

namespace AskARabbiLIB.Tests;

[TestClass]
public sealed class MongoUsageStoreTests
{
    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    [TestCategory("Regression")]
    public async Task GetTokenCountAsync_MonthlyDocument_ReturnsTokensOrZero(bool hasDocument)
    {
        var database = new RecordingDatabase();
        database.Reads.Enqueue(hasDocument ? Document(80, 100) : null);
        var lease = CreateLease();

        var tokens = await database.CreateStore().GetTokenCountAsync(lease.UserId, lease.PeriodStartUtc, lease.PeriodEndUtc);

        Assert.AreEqual(hasDocument ? 100L : 0L, tokens);
        Assert.HasCount(0, database.Updates);
    }

    [TestMethod]
    [DataRow(0)]
    [DataRow(1)]
    [TestCategory("Regression")]
    public async Task TryAcquireChatAsync_NewOrExistingMonth_ClaimsLeaseWithoutResettingTokens(int matches)
    {
        var database = new RecordingDatabase();
        database.UpdateMatches.Enqueue(matches);
        var lease = CreateLease();

        var acquired = await database.CreateStore().TryAcquireChatAsync(lease, lease.PeriodStartUtc);

        Assert.IsTrue(acquired);
        Assert.HasCount(matches == 0 ? 1 : 0, database.Inserts);
        var update = database.Updates.Single()["$set"].AsBsonDocument;
        Assert.AreEqual(lease.Id.ToString("D"), update["chatLeaseId"].AsString);
        Assert.AreEqual(0L, update["chatLeaseTokens"].ToInt64());
        Assert.IsFalse(update.Contains("tokenCount"));
    }

    [TestMethod]
    [TestCategory("Regression")]
    public async Task RecordTokensAsync_ConcurrentAccountingWrite_RecomputesOnlyTheRemainingDelta()
    {
        var database = new RecordingDatabase();
        database.Reads.Enqueue(Document(100, 5_000));
        database.Reads.Enqueue(Document(200, 5_100));
        database.UpdateMatches.Enqueue(0);
        database.UpdateMatches.Enqueue(1);

        var recorded = await database.CreateStore().RecordTokensAsync(CreateLease(), 300);

        Assert.IsTrue(recorded);
        CollectionAssert.AreEqual(new long[] { 200, 100 }, database.Updates.Select(update => update["$inc"]["tokenCount"].ToInt64()).ToArray());
        CollectionAssert.AreEqual(new long[] { 100, 200 }, database.UpdateFilters.Select(filter => filter["chatLeaseTokens"].ToInt64()).ToArray());
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    [TestCategory("Regression")]
    public async Task RecordTokensAsync_ReplayedOrLostLease_DoesNotIncrement(bool lostLease)
    {
        var database = new RecordingDatabase();
        database.Reads.Enqueue(lostLease ? null : Document(300, 500));

        var recorded = await database.CreateStore().RecordTokensAsync(CreateLease(), 200);

        Assert.AreEqual(!lostLease, recorded);
        Assert.HasCount(0, database.Updates);
    }

    [TestMethod]
    [TestCategory("Regression")]
    public async Task ReleaseChatAsync_CompletedTurn_ClearsOnlyOwnedLeaseNotUsage()
    {
        var database = new RecordingDatabase();
        database.UpdateMatches.Enqueue(1);
        var lease = CreateLease();

        await database.CreateStore().ReleaseChatAsync(lease);

        Assert.AreEqual(lease.Id.ToString("D"), database.UpdateFilters.Single()["chatLeaseId"].AsString);
        var fields = database.Updates.Single()["$set"].AsBsonDocument;
        Assert.IsTrue(fields["chatLeaseId"].IsBsonNull);
        Assert.IsTrue(fields["chatLeaseExpiresAtUtc"].IsBsonNull);
        Assert.IsFalse(fields.Contains("tokenCount"));
    }

    [TestMethod]
    [TestCategory("Regression")]
    public void LegacyUsageDocument_WithoutTokenFields_DoesNotGuessTokensFromAnswerCount()
    {
        var document = BsonSerializer.Deserialize<MongoUsageDocument>(new BsonDocument
        {
            ["_id"] = "11111111-1111-1111-1111-111111111111:202608",
            ["userId"] = "11111111-1111-1111-1111-111111111111",
            ["answerCount"] = 42,
        });

        Assert.AreEqual(42, document.AnswerCount);
        Assert.AreEqual(0L, document.TokenCount);
        Assert.IsNull(document.ChatLeaseId);
        Assert.IsNull(document.ChatLeaseExpiresAtUtc);
    }

    [TestMethod]
    [TestCategory("Regression")]
    public void TokenCount_AboveIntegerRange_RoundTripsAsInt64()
    {
        var document = new MongoUsageDocument { Id = "monthly", UserId = "user", TokenCount = 4_000_000_000 };

        var bson = document.ToBsonDocument();
        var restored = BsonSerializer.Deserialize<MongoUsageDocument>(bson);

        Assert.AreEqual(BsonType.Int64, bson["tokenCount"].BsonType);
        Assert.AreEqual(4_000_000_000L, restored.TokenCount);
    }

    [TestMethod]
    [TestCategory("Regression")]
    public void CreateAcquireFilter_QuotaAndUnexpiredLease_EnforcedInSameAtomicWrite()
    {
        var lease = CreateLease();

        var filter = Render(MongoUsageStore.CreateAcquireFilter(lease, lease.PeriodStartUtc));

        var json = filter.ToJson();
        StringAssert.Contains(json, "11111111-1111-1111-1111-111111111111:202609");
        StringAssert.Contains(json, "tokenCount");
        StringAssert.Contains(json, "$lt");
        StringAssert.Contains(json, "10000000");
        StringAssert.Contains(json, "$exists");
        StringAssert.Contains(json, "chatLeaseExpiresAtUtc");
        StringAssert.Contains(json, "$lte");
        StringAssert.Contains(json, "periodEndUtc");
    }

    [TestMethod]
    [TestCategory("Regression")]
    public void CreateOwnedFilter_ExpiredReplica_CannotMutateReplacementLease()
    {
        var lease = CreateLease();

        var filter = Render(MongoUsageStore.CreateOwnedFilter(lease));

        Assert.AreEqual(lease.Id.ToString("D"), filter["chatLeaseId"].AsString);
        Assert.AreEqual("11111111-1111-1111-1111-111111111111:202609", filter["_id"].AsString);
    }

    private static ChatUsageLease CreateLease() => new(Guid.Parse("11111111-1111-1111-1111-111111111111"), Guid.Parse("22222222-2222-2222-2222-222222222222"), new(2026, 9, 1, 0, 0, 0, TimeSpan.Zero), new(2026, 10, 1, 0, 0, 0, TimeSpan.Zero), new(2026, 9, 1, 0, 10, 0, TimeSpan.Zero), 10_000_000);
    private static BsonDocument Render(FilterDefinition<MongoUsageDocument> filter) => filter.Render(new RenderArgs<MongoUsageDocument>(BsonSerializer.SerializerRegistry.GetSerializer<MongoUsageDocument>(), BsonSerializer.SerializerRegistry));

    private static MongoUsageDocument Document(long leaseTokens, long tokens) => new() { Id = "monthly", UserId = CreateLease().UserId.ToString("D"), TokenCount = tokens, ChatLeaseTokens = leaseTokens };

    private sealed class RecordingDatabase
    {
        internal Queue<MongoUsageDocument?> Reads { get; } = new();
        internal Queue<long> UpdateMatches { get; } = new();
        internal List<BsonDocument> Updates { get; } = [];
        internal List<BsonDocument> UpdateFilters { get; } = [];
        internal List<MongoUsageDocument> Inserts { get; } = [];

        internal MongoUsageStore CreateStore() => new(
            (_, _) => Task.FromResult(Reads.Dequeue()),
            (filter, update, _) =>
            {
                var render = new RenderArgs<MongoUsageDocument>(BsonSerializer.SerializerRegistry.GetSerializer<MongoUsageDocument>(), BsonSerializer.SerializerRegistry);
                UpdateFilters.Add(filter.Render(render));
                Updates.Add(update.Render(render).AsBsonDocument);
                var matches = UpdateMatches.Dequeue();
                return Task.FromResult<UpdateResult>(new UpdateResult.Acknowledged(matches, matches, null));
            },
            (document, _) =>
            {
                Inserts.Add(document);
                return Task.CompletedTask;
            });
    }
}
