using AskARabbiLIB.DvarTorah;
using AskARabbiLIB.Persistence.Mongo;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using MongoDB.Bson;
using MongoDB.Bson.Serialization;
using MongoDB.Driver;

namespace AskARabbiLIB.Tests;

[TestClass]
[TestCategory("Regression")]
public sealed class MongoTeachingReadStateTests
{
    [TestMethod]
    public void ReadState_LegacyDocument_DefaultsToUnreadWithoutMigration()
    {
        var document = BsonSerializer.Deserialize<MongoConversationSettingsDocument>(new BsonDocument("_id", "reader"));
        Assert.HasCount(0, document.ReadDvarTorahWeekKeys);
    }

    [TestMethod]
    public void ReadState_Document_RoundTripsKeys()
    {
        var document = new MongoConversationSettingsDocument { UserId = "reader", ReadDvarTorahWeekKeys = ["diaspora:2026-08-22"] };
        var restored = BsonSerializer.Deserialize<MongoConversationSettingsDocument>(document.ToBsonDocument());
        CollectionAssert.AreEqual(document.ReadDvarTorahWeekKeys, restored.ReadDvarTorahWeekKeys);
    }

    [TestMethod]
    [DataRow(true, "$addToSet")]
    [DataRow(false, "$pull")]
    public void SetReadState_AtomicUpdate_ChangesOnlyRequestedTeaching(bool isRead, string operation)
    {
        var now = new DateTimeOffset(2026, 9, 11, 12, 0, 0, TimeSpan.Zero);
        var update = MongoConversationSettingsStore.CreateReadStateUpdate("reader", "diaspora:2026-08-22", isRead, now);
        var rendered = update.Render(new RenderArgs<MongoConversationSettingsDocument>(BsonSerializer.LookupSerializer<MongoConversationSettingsDocument>(), BsonSerializer.SerializerRegistry));
        Assert.AreEqual("diaspora:2026-08-22", rendered[operation]["readDvarTorahWeekKeys"].AsString);
        Assert.AreEqual("reader", rendered["$setOnInsert"]["_id"].AsString);
        Assert.IsFalse(rendered["$set"].AsBsonDocument.Contains("readDvarTorahWeekKeys"));
        Assert.IsFalse(rendered["$set"].AsBsonDocument.Contains("preferences"));
    }

    [TestMethod]
    [DataRow(true, "$in")]
    [DataRow(false, "$nin")]
    public void ArchiveFilter_ReadStatus_PreservesPublicationBoundsAndSearch(bool isRead, string operation)
    {
        var filter = MongoWeeklyDvarTorahStore.CreateArchiveFilter(false, new DateOnly(2026, 9, 12), "community", new WeeklyDvarTorahReadFilter(["diaspora:2026-08-22"], isRead));
        var rendered = filter.Render(new RenderArgs<MongoWeeklyDvarTorahDocument>(BsonSerializer.LookupSerializer<MongoWeeklyDvarTorahDocument>(), BsonSerializer.SerializerRegistry));
        Assert.AreEqual("diaspora:2026-08-22", rendered["_id"][operation][0].AsString);
        Assert.AreEqual(MongoWeeklyDvarTorahStore.PublishedStatus, rendered["status"].AsString);
        Assert.AreEqual("2026-09-12", rendered["shabbatDate"]["$lt"].AsString);
        Assert.IsFalse(rendered["inIsrael"].AsBoolean);
        Assert.IsTrue(rendered.Contains("$or"));
    }
}
