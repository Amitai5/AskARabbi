namespace AskARabbiLIB.DvarTorah.Audio;

internal sealed record DvarTorahSpeechWord(string Text, uint SsmlOffset, double AudioOffsetMs, double DurationMs);
