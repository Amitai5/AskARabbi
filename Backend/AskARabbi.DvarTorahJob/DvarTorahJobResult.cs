using AskARabbiLIB.DvarTorah;
using AskARabbiLIB.DvarTorah.Audio;

namespace AskARabbi.DvarTorahJob;

internal sealed record DvarTorahJobResult(WeeklyDvarTorahGenerationResult Generation, WeeklyDvarTorahAudioResult? Audio, string? AudioFailureCode);
