using System.Reflection;
using AskARabbiLIB.Persistence.Mongo;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using MongoDB.Bson;
using MongoDB.Bson.Serialization;
using MongoDB.Driver;

namespace AskARabbiLIB.Tests;

[TestClass]
public sealed class MongoAccountRegistrationStoreTests
{
    private static readonly Guid OperationId = Guid.Parse("11111111-2222-3333-4444-555555555555");

    [TestMethod]
    public async Task ReadAsync_PreexistingAccountsAndPendingIdentities_CountsBothWithoutDoubleCounting()
    {
        var database = new Database
        {
            State = new BsonDocument { { "_id", "account-registration" }, { "revision", 4L }, { "accounts", new BsonArray(Enumerable.Range(1, 99).Select(index => $"user-{index}")) }, { "reservations", new BsonArray
            {
                new BsonDocument { { "id", "one" }, { "providerUserId", "pending" } },
                new BsonDocument { { "id", "two" }, { "providerUserId", "pending" } },
            } } },
        };

        var snapshot = await database.Create().ReadAsync("pending");

        Assert.AreEqual(4L, snapshot.Revision);
        Assert.AreEqual(100L, snapshot.OccupiedPlaces);
        Assert.IsTrue(snapshot.HasPlace);
        Assert.AreEqual("conversationSettings", database.Calls[0].Collection);
        Assert.HasCount(1, database.Calls, "Capacity decisions must not depend on a separate, potentially stale accounts query.");
    }

    [TestMethod]
    public async Task ReadAsync_ExistingIdentityWithoutReservation_HasPlaceAtCapacity()
    {
        var database = new Database();
        database.State!["accounts"] = new BsonArray(Enumerable.Range(1, 99).Select(index => $"user-{index}").Append("existing"));

        var snapshot = await database.Create().ReadAsync("existing");

        Assert.IsTrue(snapshot.HasPlace);
        Assert.AreEqual(100L, snapshot.OccupiedPlaces);
        Assert.HasCount(1, database.Calls);
    }

    [TestMethod]
    public async Task ReadAsync_FirstUse_CreatesSingletonAndStillCountsExistingAccounts()
    {
        var database = new Database { State = null, ExistingAccounts = Enumerable.Range(1, 7).Select(index => new BsonDocument("providerUserId", $"existing-{index}")).ToArray() };

        var snapshot = await database.Create().ReadAsync(null);

        Assert.AreEqual(7L, snapshot.OccupiedPlaces);
        Assert.AreEqual(0L, snapshot.Revision);
        Assert.IsFalse(snapshot.HasPlace);
        Assert.IsNotNull(database.State);
        Assert.AreEqual("account-registration", database.State["_id"].AsString);
        Assert.AreEqual(0, database.State["reservations"].AsBsonArray.Count);
    }

    [TestMethod]
    [DataRow("conversationSettings")]
    [DataRow("customSettings")]
    public async Task ReadAsync_CosmosCollectionCapacityReached_UsesExistingSettingsCollection(string settingsCollection)
    {
        var database = new Database
        {
            State = null,
            RejectNewCollections = true,
            Options = new MongoDatabaseOptions { ConversationSettingsCollectionName = settingsCollection },
            ExistingAccounts = [new BsonDocument("providerUserId", "existing-user")],
        };

        var snapshot = await database.Create().ReadAsync("existing-user");

        Assert.IsTrue(snapshot.HasPlace);
        Assert.AreEqual(1L, snapshot.OccupiedPlaces);
        Assert.AreEqual(settingsCollection, database.Calls[0].Collection);
        Assert.IsNotNull(database.State);
        Assert.IsFalse(Guid.TryParse(database.State["_id"].AsString, out _), "The admission record must not collide with any account's GUID settings key.");
        Assert.IsTrue(database.Calls.Where(call => call.Collection == settingsCollection).All(call => call.Filter["_id"] == "account-registration"));
    }

    [TestMethod]
    public async Task ReadAsync_ExplicitCollectionOverride_PreservesConfiguredLedgerLocation()
    {
        var database = new Database { Options = new MongoDatabaseOptions { RegistrationCollectionName = "existingAdmission" } };

        await database.Create().ReadAsync(null);

        Assert.AreEqual("existingAdmission", database.Calls.Single().Collection);
    }

    [TestMethod]
    [DataRow("")]
    [DataRow(" ")]
    public void Validate_BlankRegistrationCollectionOverride_RejectsInvalidConfiguration(string collectionName)
    {
        var options = new MongoDatabaseOptions { ConnectionString = "mongodb://localhost", RegistrationCollectionName = collectionName };

        Assert.Throws<InvalidOperationException>(options.Validate);
    }

    [TestMethod]
    [DataRow(1L, true)]
    [DataRow(0L, false)]
    public async Task TryReserveAsync_RevisionCompareAndSwap_ReturnsWhetherItWon(long matches, bool expected)
    {
        var database = new Database { MatchedCount = matches };

        var reserved = await database.Create().TryReserveAsync(12, OperationId, "new-user");

        Assert.AreEqual(expected, reserved);
        var call = database.Calls.Single();
        Assert.AreEqual("account-registration", call.Filter["_id"].AsString);
        Assert.AreEqual(12L, call.Filter["revision"].ToInt64());
        Assert.AreEqual(1L, call.Update!["$inc"]["revision"].ToInt64());
        Assert.AreEqual("new-user", call.Update["$push"]["reservations"]["providerUserId"].AsString);
        Assert.AreEqual(OperationId.ToString("D"), call.Update["$push"]["reservations"]["id"].AsString);
    }

    [TestMethod]
    public async Task CompleteAsync_OperationScopedRelease_InvalidatesEarlierSnapshots()
    {
        var database = new Database();

        await database.Create().CompleteAsync(OperationId, "completed-user");

        var update = database.Calls.Single().Update!;
        Assert.AreEqual(1L, update["$inc"]["revision"].ToInt64());
        Assert.AreEqual("completed-user", update["$addToSet"]["accounts"].AsString);
        Assert.AreEqual(OperationId.ToString("D"), update["$pull"]["reservations"]["id"].AsString);
        Assert.IsFalse(update["$pull"]["reservations"].AsBsonDocument.Contains("providerUserId"));
    }

    [TestMethod]
    public async Task ReleaseAccountAsync_CompletedErasure_RemovesAccountWithoutRemovingOtherCallbacksReservations()
    {
        var database = new Database();

        await database.Create().ReleaseAccountAsync("deleted-user");

        var update = database.Calls.Single().Update!;
        Assert.AreEqual("deleted-user", update["$pull"]["accounts"].AsString);
        Assert.IsFalse(update["$pull"].AsBsonDocument.Contains("reservations"));
        Assert.AreEqual(1L, update["$inc"]["revision"].ToInt64());
    }

    [TestMethod]
    public async Task TryReserveAsync_InvalidArguments_DoesNotTouchDatabase()
    {
        var database = new Database();
        var store = database.Create();

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => store.TryReserveAsync(-1, OperationId, "user"));
        await Assert.ThrowsAsync<ArgumentException>(() => store.TryReserveAsync(0, Guid.Empty, "user"));
        await Assert.ThrowsAsync<ArgumentException>(() => store.TryReserveAsync(0, OperationId, " "));

        Assert.HasCount(0, database.Calls);
    }

    private sealed record Call(string Collection, BsonDocument Filter, BsonDocument? Update = null);

    private sealed class Database
    {
        internal BsonDocument? State { get; set; } = new() { { "_id", "account-registration" }, { "revision", 0L }, { "accounts", new BsonArray() }, { "reservations", new BsonArray() } };
        internal IReadOnlyList<BsonDocument> ExistingAccounts { get; init; } = [];
        internal MongoDatabaseOptions Options { get; init; } = new();
        internal bool RejectNewCollections { get; init; }
        internal long MatchedCount { get; init; } = 1;
        internal List<Call> Calls { get; } = [];

        internal MongoAccountRegistrationStore Create() => new(Stub<IMongoDatabase>.Create((method, args) => method.Name == "GetCollection" ? Collection((string)args[0]!) : throw new NotSupportedException(method.Name)), Options);

        private IMongoCollection<BsonDocument> Collection(string name) => Stub<IMongoCollection<BsonDocument>>.Create((method, args) =>
        {
            if (method.Name == "get_DocumentSerializer")
            {
                return BsonSerializer.SerializerRegistry.GetSerializer<BsonDocument>();
            }
            if (method.Name == "InsertOneAsync")
            {
                if (RejectNewCollections && name != Options.ConversationSettingsCollectionName)
                {
                    throw new InvalidOperationException("Creating a collection would exceed the provisioned Cosmos DB capacity.");
                }
                State = (BsonDocument)args[0]!;
                return Task.CompletedTask;
            }
            var render = new RenderArgs<BsonDocument>(BsonSerializer.SerializerRegistry.GetSerializer<BsonDocument>(), BsonSerializer.SerializerRegistry);
            var filter = ((FilterDefinition<BsonDocument>)args[0]!).Render(render);
            var update = args.Length > 1 && args[1] is UpdateDefinition<BsonDocument> value ? value.Render(render).AsBsonDocument : null;
            Calls.Add(new(name, filter, update));
            return method.Name switch
            {
                "UpdateOneAsync" => Task.FromResult<UpdateResult>(new UpdateResult.Acknowledged(MatchedCount, MatchedCount, null)),
                "FindAsync" => Task.FromResult<IAsyncCursor<BsonDocument>>(new Cursor(name == Options.UsersCollectionName ? ExistingAccounts : State is null ? [] : [State])),
                _ => throw new NotSupportedException(method.Name),
            };
        });
    }

    private sealed class Cursor(IReadOnlyList<BsonDocument> documents) : IAsyncCursor<BsonDocument>
    {
        private bool read;
        public IEnumerable<BsonDocument> Current => documents;
        public bool MoveNext(CancellationToken cancellationToken = default)
        {
            var result = !read;
            read = true;
            return result;
        }
        public Task<bool> MoveNextAsync(CancellationToken cancellationToken = default) => Task.FromResult(MoveNext(cancellationToken));
        public void Dispose() { }
    }

    public class Stub<T> : DispatchProxy where T : class
    {
        private Func<MethodInfo, object?[], object?> handler = null!;
        internal static T Create(Func<MethodInfo, object?[], object?> handler)
        {
            var proxy = Create<T, Stub<T>>();
            ((Stub<T>)(object)proxy).handler = handler;
            return proxy;
        }
        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args) => handler(targetMethod!, args ?? []);
    }
}
