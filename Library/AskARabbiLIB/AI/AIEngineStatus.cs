namespace AskARabbiLIB.AI;

/// <summary>Identifies the typed outcome of an AI request.</summary>
public enum AIEngineStatus
{
    Success,
    RateLimited,
    TimedOut,
    Unauthorized,
    InvalidResponse,
    ProviderFailure,
}
