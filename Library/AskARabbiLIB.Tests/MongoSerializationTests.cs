using AskARabbiLIB.Persistence.Mongo;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using MongoDB.Bson;
using MongoDB.Bson.Serialization;
using MongoDB.Bson.Serialization.Attributes;

namespace AskARabbiLIB.Tests;

[TestClass]
public sealed class MongoSerializationTests
{
    [TestMethod]
    [TestCategory("Unit")]
    public void DateOnlySerializer_RoundTrip_PreservesInvariantDate()
    {
        var expected = new DateOnly(2001, 12, 17);
        var document = new TemporalDocument { Date = expected, Time = new TimeOnly(15, 30) };

        var bson = document.ToBsonDocument();
        var result = BsonSerializer.Deserialize<TemporalDocument>(bson);

        Assert.AreEqual("2001-12-17", bson[nameof(TemporalDocument.Date)].AsString);
        Assert.AreEqual(expected, result.Date);
    }

    [TestMethod]
    [TestCategory("Unit")]
    public void TimeOnlySerializer_RoundTrip_PreservesTicks()
    {
        var expected = new TimeOnly(15, 30, 45).Add(TimeSpan.FromTicks(1234));
        var document = new TemporalDocument { Date = new DateOnly(2001, 12, 17), Time = expected };

        var bson = document.ToBsonDocument();
        var result = BsonSerializer.Deserialize<TemporalDocument>(bson);

        Assert.AreEqual("15:30:45.0001234", bson[nameof(TemporalDocument.Time)].AsString);
        Assert.AreEqual(expected, result.Time);
    }

    [TestMethod]
    [TestCategory("Unit")]
    public void DateOnlySerializer_NonStringBson_Throws()
    {
        using var reader = new MongoDB.Bson.IO.BsonDocumentReader(new BsonDocument("value", 42));
        reader.ReadStartDocument();
        Assert.AreEqual("value", reader.ReadName());
        var context = BsonDeserializationContext.CreateRoot(reader);

        Assert.ThrowsExactly<NotSupportedException>(() => new DateOnlySerializer().Deserialize(context, default));
    }

    [TestMethod]
    [TestCategory("Unit")]
    public void TimeOnlySerializer_NonStringBson_Throws()
    {
        using var reader = new MongoDB.Bson.IO.BsonDocumentReader(new BsonDocument("value", true));
        reader.ReadStartDocument();
        Assert.AreEqual("value", reader.ReadName());
        var context = BsonDeserializationContext.CreateRoot(reader);

        Assert.ThrowsExactly<NotSupportedException>(() => new TimeOnlySerializer().Deserialize(context, default));
    }

    public sealed class TemporalDocument
    {
        [BsonSerializer(typeof(DateOnlySerializer))]
        public DateOnly Date { get; init; }

        [BsonSerializer(typeof(TimeOnlySerializer))]
        public TimeOnly Time { get; init; }
    }
}
