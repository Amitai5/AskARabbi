namespace AskARabbiLIB.DvarTorah.Audio;

/// <summary>Describes an MP3 without downloading its content.</summary>
public sealed record DvarTorahAudioBlobInfo(long Length, string ETag, DateTimeOffset LastModified);
