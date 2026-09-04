using AskARabbiLIB.DvarTorah.Audio;
using MongoDB.Bson.Serialization.Attributes;

namespace AskARabbiLIB.Persistence.Mongo;

[BsonIgnoreExtraElements]
internal sealed record MongoWeeklyDvarTorahAudioDocument
{
    [BsonElement("version")]
    public required string Version { get; init; }
    [BsonElement("voice")]
    public required string Voice { get; init; }
    [BsonElement("durationMs")]
    public double DurationMs { get; init; }
    [BsonElement("blobName")]
    public required string BlobName { get; init; }
    [BsonElement("blobUri")]
    public required string BlobUri { get; init; }
    [BsonElement("timingsBlobName")]
    public required string TimingsBlobName { get; init; }
    [BsonElement("audioLength")]
    public long AudioLength { get; init; }
    [BsonElement("createdAtUtc")]
    public DateTime CreatedAtUtc { get; init; }

    internal WeeklyDvarTorahAudioMetadata? ToDomain()
    {
        // Optional audio corruption must not make an otherwise published article unreadable.
        if (string.IsNullOrWhiteSpace(Version) || string.IsNullOrWhiteSpace(Voice) || string.IsNullOrWhiteSpace(BlobName) || string.IsNullOrWhiteSpace(BlobUri) || string.IsNullOrWhiteSpace(TimingsBlobName) || !double.IsFinite(DurationMs) || DurationMs <= 0 || AudioLength <= 0)
        {
            return null;
        }
        return new WeeklyDvarTorahAudioMetadata
        {
            Version = Version, Voice = Voice, DurationMs = DurationMs, BlobName = BlobName, BlobUri = BlobUri,
            TimingsBlobName = TimingsBlobName, AudioLength = AudioLength, CreatedAtUtc = new DateTimeOffset(DateTime.SpecifyKind(CreatedAtUtc, DateTimeKind.Utc)),
        };
    }

    internal static MongoWeeklyDvarTorahAudioDocument FromDomain(WeeklyDvarTorahAudioMetadata audio) => new()
    {
        Version = audio.Version, Voice = audio.Voice, DurationMs = audio.DurationMs, BlobName = audio.BlobName, BlobUri = audio.BlobUri,
        TimingsBlobName = audio.TimingsBlobName, AudioLength = audio.AudioLength, CreatedAtUtc = audio.CreatedAtUtc.UtcDateTime,
    };
}
