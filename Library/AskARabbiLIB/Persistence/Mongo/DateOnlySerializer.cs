using System.Globalization;
using MongoDB.Bson;
using MongoDB.Bson.Serialization;
using MongoDB.Bson.Serialization.Serializers;

namespace AskARabbiLIB.Persistence.Mongo;

/// <summary>Serializes <see cref="DateOnly"/> values as culture-invariant BSON strings.</summary>
public sealed class DateOnlySerializer : StructSerializerBase<DateOnly>
{
    private const string Format = "yyyy-MM-dd";

    /// <inheritdoc/>
    public override DateOnly Deserialize(BsonDeserializationContext context, BsonDeserializationArgs args)
    {
        ArgumentNullException.ThrowIfNull(context);
        var type = context.Reader.GetCurrentBsonType();
        return type switch
        {
            BsonType.String => DateOnly.ParseExact(context.Reader.ReadString(), Format, CultureInfo.InvariantCulture),
            _ => throw new NotSupportedException($"{type} is not supported for DateOnly deserialization."),
        };
    }

    /// <inheritdoc/>
    public override void Serialize(BsonSerializationContext context, BsonSerializationArgs args, DateOnly value)
    {
        ArgumentNullException.ThrowIfNull(context);
        context.Writer.WriteString(value.ToString(Format, CultureInfo.InvariantCulture));
    }
}
