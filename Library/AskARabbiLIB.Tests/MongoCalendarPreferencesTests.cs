using AskARabbiLIB.Calendar;
using AskARabbiLIB.Persistence.Mongo;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using MongoDB.Bson;
using MongoDB.Bson.Serialization;
using MongoDB.Driver;

namespace AskARabbiLIB.Tests;

[TestClass]
public sealed class MongoCalendarPreferencesTests
{
    [TestMethod]
    public void CalendarPreferences_AtomicFieldUpdate_RoundTripsWithoutReplacingOtherSettings()
    {
        var preferences = new CalendarPreferences { InIsrael = true, Location = new() { Kind = "city", Id = "281184", Label = "Jerusalem", TimeZone = "Asia/Jerusalem", DefaultCandleLightingMinutes = 40 } };
        var serializer = BsonSerializer.SerializerRegistry.GetSerializer<MongoConversationSettingsDocument>();
        var update = Builders<MongoConversationSettingsDocument>.Update.Set(value => value.CalendarPreferences, preferences);

        var command = update.Render(new(serializer, BsonSerializer.SerializerRegistry)).AsBsonDocument;
        var document = BsonSerializer.Deserialize<MongoConversationSettingsDocument>(new BsonDocument { { "_id", "owner" }, { "calendarPreferences", command["$set"]["calendarPreferences"] }, { "preferences", new BsonDocument("showSourceContextByDefault", false) } });

        CollectionAssert.AreEqual(new[] { "calendarPreferences" }, command["$set"].AsBsonDocument.Names.ToArray());
        Assert.AreEqual(preferences, document.CalendarPreferences);
        Assert.IsNotNull(document.Preferences);
        Assert.IsFalse(document.Preferences.ToDomain().ShowSourceContextByDefault);
    }

    [TestMethod]
    public void Deserialize_ExistingAccountWithoutCalendar_KeepsLegacyPreferencesAndDefaultsMissing()
    {
        var stored = new BsonDocument { { "_id", "existing-owner" }, { "preferences", new BsonDocument("showSourceContextByDefault", false) } };

        var account = BsonSerializer.Deserialize<MongoConversationSettingsDocument>(stored);

        Assert.IsNull(account.CalendarPreferences);
        Assert.IsNotNull(account.Preferences);
        Assert.IsFalse(account.Preferences.ToDomain().ShowSourceContextByDefault);
        Assert.IsFalse(account.ToBsonDocument().Contains("calendarPreferences"));
    }
}
