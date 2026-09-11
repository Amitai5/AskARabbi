using AskARabbiLIB.Persistence.Mongo;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using MongoDB.Bson;
using MongoDB.Bson.Serialization;

namespace AskARabbiLIB.Tests;

[TestClass]
[TestCategory("Regression")]
public sealed class MongoPersonalizationLocationTests
{
    [TestMethod]
    public void PersonalizationLocations_Serialization_RetainsIndependentResolvedLocations()
    {
        var document = Profile();
        document = new MongoPersonalizationDocument
        {
            FullName = document.FullName, BirthDate = document.BirthDate, BirthTime = document.BirthTime, BirthTimeZone = document.BirthTimeZone,
            ConversationLanguage = document.ConversationLanguage, QuotationLanguage = document.QuotationLanguage, ReligiousMovement = document.ReligiousMovement, JewishHeritage = document.JewishHeritage,
            BirthLocation = new() { Kind = "zip", Id = "91302", Label = "Calabasas", TimeZone = "America/Los_Angeles", Latitude = 34.15778, Longitude = -118.63842 },
            CurrentLocation = new() { Kind = "city", Id = "281184", Label = "Jerusalem", TimeZone = "Asia/Jerusalem", Latitude = 31.76904, Longitude = 35.21633, DefaultCandleLightingMinutes = 40 },
        };

        var bson = document.ToBsonDocument();
        var restored = BsonSerializer.Deserialize<MongoPersonalizationDocument>(bson);

        Assert.IsTrue(bson.Contains("birthLocation"));
        Assert.IsTrue(bson.Contains("currentLocation"));
        Assert.AreEqual(document.BirthLocation, restored.BirthLocation);
        Assert.AreEqual(document.CurrentLocation, restored.CurrentLocation);
        Assert.AreEqual(document.BirthTimeZone, restored.BirthTimeZone);
        Assert.AreEqual(document.BirthDate, restored.BirthDate);
        Assert.AreEqual(document.BirthTime, restored.BirthTime);
    }

    [TestMethod]
    public void PersonalizationLocations_LegacyDocument_LeavesNewLocationsUnset()
    {
        var bson = Profile().ToBsonDocument();
        bson.Remove("birthLocation");
        bson.Remove("currentLocation");

        var restored = BsonSerializer.Deserialize<MongoPersonalizationDocument>(bson);

        Assert.IsNull(restored.BirthLocation);
        Assert.IsNull(restored.CurrentLocation);
        Assert.AreEqual("America/Los_Angeles", restored.BirthTimeZone);
    }

    private static MongoPersonalizationDocument Profile() => new() { FullName = "Reader", BirthDate = new(2001, 12, 17), BirthTime = new(20, 0), BirthTimeZone = "America/Los_Angeles", ConversationLanguage = "English", QuotationLanguage = "Hebrew", ReligiousMovement = "Traditional", JewishHeritage = "Mizrahi" };
}
