namespace AskARabbiLIB.DvarTorah.Audio;

internal sealed record DvarTorahSpeechAudio(ReadOnlyMemory<byte> Pcm, IReadOnlyList<DvarTorahSpeechWord> Words);
