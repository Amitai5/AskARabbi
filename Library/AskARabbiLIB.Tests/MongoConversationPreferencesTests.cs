using AskARabbiLIB.ConversationSettings;
using AskARabbiLIB.Persistence.Mongo;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using MongoDB.Bson;
using MongoDB.Bson.Serialization;

namespace AskARabbiLIB.Tests;

[TestClass]
[TestCategory("Regression")]
public sealed class MongoConversationPreferencesTests
{
    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void EnterSendsMessage_BsonRoundTrip_PreservesAllPreferences(bool enterSendsMessage)
    {
        var expected = new ConversationPreferences { ShowSourceContextByDefault = true, EmailProductUpdates = true, EnterSendsMessage = enterSendsMessage };
        var bson = MongoConversationPreferencesDocument.FromDomain(expected).ToBsonDocument();

        var restored = BsonSerializer.Deserialize<MongoConversationPreferencesDocument>(bson).ToDomain();

        Assert.AreEqual(enterSendsMessage, bson["enterSendsMessage"].AsBoolean);
        Assert.AreEqual(expected, restored);
    }

    [TestMethod]
    public void EnterSendsMessage_LegacyDocument_DefaultsToNewLineWithoutMigration()
    {
        var bson = new BsonDocument { { "defaultsVersion", 1 }, { "showSourceContextByDefault", true }, { "emailProductUpdates", true } };

        var restored = BsonSerializer.Deserialize<MongoConversationPreferencesDocument>(bson).ToDomain();

        Assert.IsFalse(restored.EnterSendsMessage);
        Assert.IsTrue(restored.ShowSourceContextByDefault);
        Assert.IsTrue(restored.EmailProductUpdates);
    }
}
