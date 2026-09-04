namespace AskARabbiLIB.DvarTorah.Audio;

/// <summary>Indicates whether a recording was generated or deferred without affecting text publication.</summary>
public enum WeeklyDvarTorahAudioStatus
{
    Disabled,
    AlreadyGenerated,
    GenerationInProgress,
    Generated,
    LostLease,
}
