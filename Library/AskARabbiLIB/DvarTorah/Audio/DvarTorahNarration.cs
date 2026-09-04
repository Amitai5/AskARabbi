namespace AskARabbiLIB.DvarTorah.Audio;

/// <summary>Contains one encoded MP3 and validated timings ready for private storage.</summary>
public sealed record DvarTorahNarration(ReadOnlyMemory<byte> Mp3, DvarTorahAudioTimings Timings);
