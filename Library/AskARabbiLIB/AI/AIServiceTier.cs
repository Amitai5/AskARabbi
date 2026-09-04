namespace AskARabbiLIB.AI;

/// <summary>Specifies the provider processing tier requested for AI generation.</summary>
public enum AIServiceTier
{
    /// <summary>Uses the service tier configured by the provider deployment.</summary>
    Auto,

    /// <summary>Uses standard pay-as-you-go processing.</summary>
    Standard,

    /// <summary>Requests lower-latency priority processing when provider capacity permits.</summary>
    Priority,
}
