using System.Net;
using System.Reflection;
using AskARabbiLIB.Persistence.Mongo;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using MongoDB.Bson;
using MongoDB.Driver;
using MongoDB.Driver.Core.Clusters;
using MongoDB.Driver.Core.Connections;
using MongoDB.Driver.Core.Servers;

namespace AskARabbiLIB.Tests;

[TestClass]
public sealed class MongoUserAccountStoreTests
{
    [TestMethod]
    public async Task UpsertWithRetryAsync_SameIdentityWinsConcurrentInsert_RetriesUniqueIndexRace()
    {
        var calls = 0;
        var id = Guid.Parse("11111111-1111-1111-1111-111111111111");
        var collection = Stub.Create<IMongoCollection<BsonDocument>>((method, args) =>
        {
            Assert.AreEqual("FindOneAndUpdateAsync", method.Name);
            calls++;
            Assert.IsTrue(((FindOneAndUpdateOptions<BsonDocument, BsonDocument>)args[2]!).IsUpsert);
            if (calls == 1)
            {
                var connection = new ConnectionId(new ServerId(new ClusterId(), new DnsEndPoint("mongo.example.test", 27017)));
                throw new MongoCommandException(connection, "Duplicate identity", new BsonDocument("findAndModify", "users"), new BsonDocument { { "code", 11000 }, { "errmsg", "Duplicate identity" } });
            }
            return Task.FromResult(new BsonDocument("_id", id.ToString("D")));
        });

        var account = await MongoUserAccountStore.UpsertWithRetryAsync(collection, new BsonDocument("providerUserId", "verified-user"), Builders<BsonDocument>.Update.Set("email", "verified@example.test"), CancellationToken.None);

        Assert.AreEqual(id.ToString("D"), account["_id"].AsString);
        Assert.AreEqual(2, calls);
    }

    public class Stub : DispatchProxy
    {
        private Func<MethodInfo, object?[], object?> handler = null!;
        internal static T Create<T>(Func<MethodInfo, object?[], object?> handler) where T : class
        {
            var proxy = Create<T, Stub>();
            ((Stub)(object)proxy).handler = handler;
            return proxy;
        }
        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args) => handler(targetMethod!, args ?? []);
    }
}
