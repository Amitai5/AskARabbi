namespace AskARabbiLIB.DvarTorah.Audio;

/// <summary>Maps one spoken word to UTF-16 positions in the exact normalized title or body.</summary>
public sealed record DvarTorahAudioWord(string Section, string Text, int TextOffset, int TextLength, double AudioOffsetMs, double DurationMs);
