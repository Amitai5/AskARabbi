using System.Globalization;
using MongoDB.Bson;
using MongoDB.Bson.Serialization;
using MongoDB.Bson.Serialization.Serializers;

namespace AskARabbiLIB.Persistence.Mongo;

/// <summary>Serializes <see cref="TimeOnly"/> values as culture-invariant BSON strings.</summary>
public sealed class TimeOnlySerializer : StructSerializerBase<TimeOnly>
{
    private const string Format = "HH:mm:ss.fffffff";

    /// <inheritdoc/>
    public override TimeOnly Deserialize(BsonDeserializationContext context, BsonDeserializationArgs args)
    {
        ArgumentNullException.ThrowIfNull(context);
        var type = context.Reader.GetCurrentBsonType();
        return type switch
        {
            BsonType.String => TimeOnly.ParseExact(context.Reader.ReadString(), Format, CultureInfo.InvariantCulture),
            _ => throw new NotSupportedException($"{type} is not supported for TimeOnly deserialization."),
        };
    }

    /// <inheritdoc/>
    public override void Serialize(BsonSerializationContext context, BsonSerializationArgs args, TimeOnly value)
    {
        ArgumentNullException.ThrowIfNull(context);
        context.Writer.WriteString(value.ToString(Format, CultureInfo.InvariantCulture));
    }
}
