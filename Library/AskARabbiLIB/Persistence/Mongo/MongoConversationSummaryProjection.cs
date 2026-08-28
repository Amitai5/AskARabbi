namespace AskARabbiLIB.Persistence.Mongo;

internal sealed class MongoConversationSummaryProjection
{
    public required string Id { get; init; }

    public required string Title { get; init; }

    public required List<string> EnabledSourceKeys { get; init; }

    public DateTime UpdatedAtUtc { get; init; }
}
