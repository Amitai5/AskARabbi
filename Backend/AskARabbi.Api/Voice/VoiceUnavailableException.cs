namespace AskARabbi.Api.Voice;

/// <summary>Reports a speech boundary failure without provider diagnostics or private text.</summary>
public sealed class VoiceUnavailableException : Exception
{
    /// <summary>Creates a safe optional-audio failure.</summary>
    public VoiceUnavailableException() : base("Voice is unavailable right now. You can still type your question and read the answer.")
    {
    }
}
