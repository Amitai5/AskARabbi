namespace AskARabbiLIB.Retrieval;

/// <summary>Controls the bounded in-process cache for corpus retrieval results.</summary>
public sealed record SourceRetrieverCacheOptions
{
    /// <summary>Gets how long successful retrieval results remain reusable.</summary>
    public TimeSpan Duration { get; init; } = TimeSpan.FromMinutes(10);

    /// <summary>Gets the maximum number of distinct retrieval queries retained by one process.</summary>
    public int MaximumEntries { get; init; } = 256;

    /// <summary>Validates cache duration and capacity.</summary>
    public void Validate()
    {
        if (Duration < TimeSpan.FromSeconds(1) || Duration > TimeSpan.FromHours(1))
        {
            throw new ArgumentOutOfRangeException(nameof(Duration), "Retrieval cache duration must be between one second and one hour.");
        }

        if (MaximumEntries is < 1 or > 10_000)
        {
            throw new ArgumentOutOfRangeException(nameof(MaximumEntries), "Retrieval cache capacity must be between 1 and 10,000 entries.");
        }
    }
}
