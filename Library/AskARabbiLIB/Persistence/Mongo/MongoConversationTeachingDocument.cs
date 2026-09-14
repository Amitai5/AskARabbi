using MongoDB.Bson.Serialization.Attributes;

namespace AskARabbiLIB.Persistence.Mongo;

[BsonIgnoreExtraElements]
internal sealed record MongoConversationTeachingDocument
{
    [BsonElement("weekKey")]
    public required string WeekKey { get; init; }

    [BsonElement("title")]
    public required string Title { get; init; }

    [BsonElement("body")]
    public required string Body { get; init; }

    [BsonElement("sourceReferences")]
    public required string SourceReferences { get; init; }

    [BsonElement("selectedText")]
    public string? SelectedText { get; init; }
}
