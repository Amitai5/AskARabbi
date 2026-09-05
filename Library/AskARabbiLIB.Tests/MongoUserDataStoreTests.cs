using System.Reflection;
using AskARabbiLIB.Persistence.Mongo;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using MongoDB.Bson;
using MongoDB.Bson.Serialization;
using MongoDB.Driver;

namespace AskARabbiLIB.Tests;

[TestClass]
public sealed class MongoUserDataStoreTests
{
    private static readonly Guid UserId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly DateTimeOffset Now = new(2026, 9, 5, 12, 0, 0, TimeSpan.Zero);

    [TestMethod]
    public async Task DeleteChats_OwnerScope_DeletesHeadersAndAllMessagesOnly()
    {
        var database = new RecordingDatabase();
        var store = database.CreateStore();

        await store.DeleteChatsAsync(UserId);

        var deletions = database.Calls.Where(call => call.Method == "DeleteManyAsync").ToArray();
        CollectionAssert.AreEqual(new[] { "conversations", "conversationMessages" }, deletions.Select(call => call.Collection).ToArray());
        foreach (var call in deletions)
        {
            Assert.AreEqual(new BsonDocument("userId", UserId.ToString("D")).ToJson(), call.Filter.ToJson());
        }
    }

    [TestMethod]
    public async Task CompleteDeletion_OwnedCollections_UsesSettingsIdAndDeletesRecoveryRecordLast()
    {
        var database = new RecordingDatabase();
        var store = database.CreateStore();

        await store.CompleteDeletionAsync(UserId);

        var calls = database.Calls.Where(call => call.Method.StartsWith("Delete", StringComparison.Ordinal)).ToArray();
        CollectionAssert.AreEqual(new[] { "conversations", "conversationMessages", "conversationSettings", "usage", "users" }, calls.Select(call => call.Collection).ToArray());
        Assert.AreEqual(new BsonDocument("_id", UserId.ToString("D")).ToJson(), calls[2].Filter.ToJson());
        Assert.AreEqual(new BsonDocument("userId", UserId.ToString("D")).ToJson(), calls[3].Filter.ToJson());
        Assert.AreEqual(UserId.ToString("D"), calls[4].Filter["_id"].AsString);
        Assert.AreEqual("date", calls[4].Filter["deletionRequestedAtUtc"]["$type"].AsString);
        Assert.IsFalse(calls.Any(call => call.Collection == "WeeklyAIDvarTorahs"));
    }

    [TestMethod]
    public async Task CompleteDeletion_SettingsFailure_PreservesRecoveryRecordAndRetriesIdempotently()
    {
        var database = new RecordingDatabase { FailingCollection = "conversationSettings" };
        var store = database.CreateStore();

        await Assert.ThrowsAsync<InvalidOperationException>(() => store.CompleteDeletionAsync(UserId));

        Assert.IsFalse(database.Calls.Any(call => call.Method == "DeleteOneAsync"));
        database.FailingCollection = null;
        await store.CompleteDeletionAsync(UserId);
        Assert.AreEqual("users", database.Calls.Last().Collection);
        Assert.AreEqual("DeleteOneAsync", database.Calls.Last().Method);
    }

    [TestMethod]
    public async Task CompleteDeletion_AccountNotMarked_DoesNotEraseAnything()
    {
        var database = new RecordingDatabase { IsPending = false };
        var store = database.CreateStore();

        await store.CompleteDeletionAsync(UserId);

        Assert.IsFalse(database.Calls.Any(call => call.Method.StartsWith("Delete", StringComparison.Ordinal)));
    }

    [TestMethod]
    [DataRow(true)]
    [DataRow(false)]
    public async Task TryAcquire_LeaseMode_AtomicallyExcludesIncompatibleLiveRequests(bool exclusive)
    {
        var database = new RecordingDatabase();
        var store = database.CreateStore();

        var acquired = await store.TryAcquireAsync(UserId, Guid.Parse("22222222-2222-2222-2222-222222222222"), exclusive, Now, Now.AddMinutes(30));

        Assert.IsTrue(acquired);
        var call = database.Calls.Last();
        Assert.AreEqual(UserId.ToString("D"), call.Filter["_id"].AsString);
        Assert.AreEqual(BsonNull.Value, call.Filter["deletionRequestedAtUtc"]);
        var incompatible = call.Filter["dataOperations"]["$not"]["$elemMatch"].AsBsonDocument;
        Assert.AreEqual(!exclusive, incompatible.Contains("exclusive"));
        Assert.AreEqual(Now.UtcDateTime, incompatible["expiresAtUtc"]["$gt"].ToUniversalTime());
        Assert.AreEqual(exclusive, call.Update!["$push"]["dataOperations"]["exclusive"].AsBoolean);
    }

    [TestMethod]
    public async Task TryRequestDeletion_LiveOperations_UsesAtomicIdleAccountPredicate()
    {
        var database = new RecordingDatabase();
        var store = database.CreateStore();

        var pending = await store.TryRequestDeletionAsync(UserId, Now);

        Assert.IsNotNull(pending);
        Assert.AreEqual(UserId, pending.UserId);
        var call = database.Calls.Single();
        Assert.AreEqual(BsonNull.Value, call.Filter["deletionRequestedAtUtc"]);
        Assert.AreEqual(Now.UtcDateTime, call.Filter["dataOperations"]["$not"]["$elemMatch"]["expiresAtUtc"]["$gt"].ToUniversalTime());
        Assert.AreEqual(Now.UtcDateTime, call.Update!["$set"]["deletionRequestedAtUtc"].ToUniversalTime());
    }

    [TestMethod]
    public async Task DeleteChats_EmptyOwner_RejectsBeforeAnyDatabaseOperation()
    {
        var database = new RecordingDatabase();
        var store = database.CreateStore();

        await Assert.ThrowsAsync<ArgumentException>(() => store.DeleteChatsAsync(Guid.Empty));

        Assert.HasCount(0, database.Calls);
    }

    [TestMethod]
    public async Task ListPendingDeletions_MoreThanOneBatch_DoesNotStarveLaterAccounts()
    {
        var database = new RecordingDatabase
        {
            PendingDocuments = Enumerable.Range(1, 40).Select(index => new BsonDocument { { "_id", new Guid(index, 0, 0, new byte[8]).ToString("D") }, { "providerUserId", $"user_{index}" }, { "deletionRequestedAtUtc", Now.UtcDateTime } }).ToArray(),
        };
        var store = database.CreateStore();

        var pending = await store.ListPendingDeletionsAsync();

        Assert.HasCount(40, pending);
        Assert.AreEqual("user_40", pending.Last().ProviderUserId);
        Assert.AreEqual("date", database.Calls.Single().Filter["deletionRequestedAtUtc"]["$type"].AsString);
    }

    private sealed record Call(string Collection, string Method, BsonDocument Filter, BsonDocument? Update);

    private sealed class RecordingDatabase
    {
        internal List<Call> Calls { get; } = [];
        internal string? FailingCollection { get; set; }
        internal bool IsPending { get; set; } = true;
        internal IReadOnlyList<BsonDocument>? PendingDocuments { get; set; }

        internal MongoUserDataStore CreateStore()
        {
            var database = Stub<IMongoDatabase>.Create((method, args) => method.Name == "GetCollection" ? CreateCollection((string)args[0]!) : throw new NotSupportedException(method.Name));
            return new MongoUserDataStore(database, new MongoDatabaseOptions());
        }

        private IMongoCollection<BsonDocument> CreateCollection(string name) => Stub<IMongoCollection<BsonDocument>>.Create((method, args) =>
        {
            if (method.Name == "get_DocumentSerializer")
            {
                return BsonSerializer.SerializerRegistry.GetSerializer<BsonDocument>();
            }
            var filter = (FilterDefinition<BsonDocument>)args[0]!;
            var render = new RenderArgs<BsonDocument>(BsonSerializer.SerializerRegistry.GetSerializer<BsonDocument>(), BsonSerializer.SerializerRegistry);
            var update = args.Length > 1 && args[1] is UpdateDefinition<BsonDocument> value ? value.Render(render).AsBsonDocument : null;
            Calls.Add(new(name, method.Name, filter.Render(render), update));
            if (name == FailingCollection)
            {
                throw new InvalidOperationException("Simulated storage failure.");
            }
            var document = new BsonDocument { { "_id", UserId.ToString("D") }, { "providerUserId", "user_test" }, { "deletionRequestedAtUtc", Now.UtcDateTime } };
            var limit = args.OfType<FindOptions<BsonDocument, BsonDocument>>().FirstOrDefault()?.Limit;
            var documents = (IsPending ? PendingDocuments ?? [document] : []).Take(limit ?? int.MaxValue).ToArray();
            return method.Name switch
            {
                "UpdateOneAsync" => Task.FromResult<UpdateResult>(new UpdateResult.Acknowledged(1, 1, null)),
                "DeleteManyAsync" or "DeleteOneAsync" => Task.FromResult<DeleteResult>(new DeleteResult.Acknowledged(1)),
                "FindOneAndUpdateAsync" => Task.FromResult(document),
                "FindAsync" => Task.FromResult<IAsyncCursor<BsonDocument>>(new Cursor(documents)),
                _ => throw new NotSupportedException(method.Name),
            };
        });
    }

    private sealed class Cursor(IReadOnlyList<BsonDocument> documents) : IAsyncCursor<BsonDocument>
    {
        private bool hasMoved;
        public IEnumerable<BsonDocument> Current => documents;
        public void Dispose() { }
        public bool MoveNext(CancellationToken cancellationToken = default)
        {
            var result = !hasMoved;
            hasMoved = true;
            return result;
        }
        public Task<bool> MoveNextAsync(CancellationToken cancellationToken = default) => Task.FromResult(MoveNext(cancellationToken));
    }

    public class Stub<T> : DispatchProxy where T : class
    {
        private Func<MethodInfo, object?[], object?>? handler;
        internal static T Create(Func<MethodInfo, object?[], object?> handler)
        {
            var proxy = Create<T, Stub<T>>();
            ((Stub<T>)(object)proxy).handler = handler;
            return proxy;
        }
        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args) => handler!(targetMethod!, args ?? []);
    }
}
