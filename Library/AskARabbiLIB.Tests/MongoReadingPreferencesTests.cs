using AskARabbiLIB.ConversationSettings;
using AskARabbiLIB.Persistence.Mongo;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using MongoDB.Bson;
using MongoDB.Bson.Serialization;

namespace AskARabbiLIB.Tests;

[TestClass]
[TestCategory("Regression")]
public sealed class MongoReadingPreferencesTests
{
    [TestMethod]
    public void ReadingPreferences_Serialization_PreservesAllPresets()
    {
        var expected = new ReadingPreferences { TextSize = "extra-large", LineSpacing = "relaxed", Theme = "dark", FocusLongContent = true };
        var document = new MongoConversationSettingsDocument { UserId = "reader", ReadingPreferences = expected };

        var bson = document.ToBsonDocument();
        var restored = BsonSerializer.Deserialize<MongoConversationSettingsDocument>(bson);

        Assert.IsTrue(bson.Contains("readingPreferences"));
        Assert.AreEqual(expected, restored.ReadingPreferences);
    }

    [TestMethod]
    public void ReadingPreferences_LegacyDocument_RemainsReadableWithoutMigration()
    {
        var bson = new MongoConversationSettingsDocument { UserId = "reader" }.ToBsonDocument();

        var restored = BsonSerializer.Deserialize<MongoConversationSettingsDocument>(bson);

        Assert.IsFalse(bson.Contains("readingPreferences"));
        Assert.IsNull(restored.ReadingPreferences);
        Assert.AreEqual("reader", restored.UserId);
    }
}
