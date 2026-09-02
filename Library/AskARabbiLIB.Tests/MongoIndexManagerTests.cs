using AskARabbiLIB.Persistence.Mongo;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using MongoDB.Bson.Serialization;
using MongoDB.Driver;

namespace AskARabbiLIB.Tests;

[TestClass]
public sealed class MongoIndexManagerTests
{
    [TestMethod]
    [TestCategory("Unit")]
    public void CreateMessageIndex_ChronologicalMessageQuery_MatchesEveryOrderedField()
    {
        var serializerRegistry = BsonSerializer.SerializerRegistry;
        var serializer = serializerRegistry.GetSerializer<MongoConversationMessageDocument>();

        var index = MongoIndexManager.CreateMessageIndex();
        var keys = index.Keys.Render(new RenderArgs<MongoConversationMessageDocument>(serializer, serializerRegistry));
        var sort = MongoConversationStore.CreateMessageSort().Render(new RenderArgs<MongoConversationMessageDocument>(serializer, serializerRegistry));

        CollectionAssert.AreEqual(new[] { "createdAtUtc", "_id" }, keys.Names.ToArray());
        CollectionAssert.AreEqual(keys.Names.ToArray(), sort.Names.ToArray());
        Assert.IsTrue(keys.Values.All(value => value.ToInt32() == 1));
        Assert.IsTrue(sort.Values.All(value => value.ToInt32() == 1));
        Assert.IsNull(index.Options?.Name);
    }

    [TestMethod]
    [TestCategory("Regression")]
    public void CreateMetadataUpdate_FirstAssistantMessage_PersistsGeneratedTitleWithTimestamp()
    {
        var serializerRegistry = BsonSerializer.SerializerRegistry;
        var serializer = serializerRegistry.GetSerializer<MongoConversationDocument>();

        var update = MongoConversationStore.CreateMetadataUpdate(new DateTimeOffset(2026, 8, 25, 12, 30, 0, TimeSpan.Zero), "Generated title");
        var rendered = update.Render(new RenderArgs<MongoConversationDocument>(serializer, serializerRegistry));
        var set = rendered["$set"].AsBsonDocument;

        CollectionAssert.AreEquivalent(new[] { "updatedAtUtc", "title" }, set.Names.ToArray());
        Assert.AreEqual("Generated title", set["title"].AsString);
    }
}
