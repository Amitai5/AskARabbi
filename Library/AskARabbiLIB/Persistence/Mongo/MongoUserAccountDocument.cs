using MongoDB.Bson.Serialization.Attributes;

namespace AskARabbiLIB.Persistence.Mongo;

[BsonIgnoreExtraElements]
internal sealed class MongoUserAccountDocument
{
    [BsonId]
    public required string Id { get; init; }

    [BsonElement("providerUserId")]
    public required string ProviderUserId { get; init; }

    [BsonElement("email")]
    public required string Email { get; init; }

    [BsonElement("isEmailVerified")]
    public bool IsEmailVerified { get; init; }

    [BsonElement("firstName")]
    public string? FirstName { get; init; }

    [BsonElement("lastName")]
    public string? LastName { get; init; }

    [BsonElement("profileImageUrl")]
    public string? ProfileImageUrl { get; init; }

    [BsonElement("createdAtUtc")]
    public DateTime CreatedAtUtc { get; init; }

    [BsonElement("updatedAtUtc")]
    public DateTime UpdatedAtUtc { get; init; }
}
