using AskARabbiLIB.Conversations;
using AskARabbiLIB.DvarTorah;
using AskARabbiLIB.Persistence.Mongo;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using MongoDB.Bson;
using MongoDB.Bson.Serialization;

namespace AskARabbiLIB.Tests;

[TestClass]
public sealed class ConversationTeachingContextTests
{
    [TestMethod]
    public void FromPublished_DisplaySelection_NormalizesPunctuationReferencesAndWhitespace()
    {
        var date = new DateTimeOffset(2026, 8, 24, 12, 0, 0, TimeSpan.Zero);
        var source = new WeeklyDvarTorahSource("TA", WeeklyDvarTorahSourceKind.Torah, "Deuteronomy", "JPS", "https://www.sefaria.org/Deuteronomy.30.19", "Choose life.", date, "Deuteronomy 30:19");
        var metadata = new WeeklyDvarTorahContentMetadata("Choose life", ["life", "community", "torah"], [source], 80, "test", "test", date.AddDays(-7), date);
        var article = new WeeklyDvarTorahArticle(new WeeklyDvarTorahWeek(new DateOnly(2026, 8, 29), "16 Elul, 5786", "Ki Tavo", null, false), "Life\u0014together", "Choose life\u0019s gifts [TA].\n\nCare for others.", "test", date, date, metadata);

        var context = ConversationTeachingContext.FromPublished(article, "life’s gifts [1].\nCare for others.");

        Assert.AreEqual("Life—together", context.Title);
        Assert.AreEqual("life’s gifts [1]. Care for others.", context.SelectedText);
        StringAssert.Contains(context.Body, "Choose life’s gifts [1].");
        StringAssert.Contains(context.SourceReferences, "[1] Deuteronomy 30:19");
    }

    [TestMethod]
    public void MongoTeachingContext_RoundTrip_PreservesSnapshotAndSupportsOldDocuments()
    {
        var document = new MongoConversationDocument { Id = "id", UserId = "user", Title = "Chat", EnabledSourceKeys = [], TeachingContext = new MongoConversationTeachingDocument { WeekKey = "diaspora:2026-08-29", Title = "Teaching", Body = "Full teaching.", SourceReferences = "[1] Deuteronomy 30:19", SelectedText = "Full" } };

        var bson = document.ToBsonDocument();
        var result = BsonSerializer.Deserialize<MongoConversationDocument>(bson);

        Assert.AreEqual(document.TeachingContext, result.TeachingContext);
        bson.Remove("teachingContext");
        Assert.IsNull(BsonSerializer.Deserialize<MongoConversationDocument>(bson).TeachingContext);
    }

    [TestMethod]
    [DataRow(100_001, 0)]
    [DataRow(1, 4_001)]
    [DataRow(0, 0)]
    public void Validate_InvalidBounds_RejectsContext(int bodyLength, int selectionLength)
    {
        var context = new ConversationTeachingContext("diaspora:2026-08-29", "Title", new string('a', bodyLength), "", selectionLength == 0 ? null : new string('a', selectionLength));

        Assert.ThrowsExactly<ArgumentException>(() => context.Validate());
    }
}
