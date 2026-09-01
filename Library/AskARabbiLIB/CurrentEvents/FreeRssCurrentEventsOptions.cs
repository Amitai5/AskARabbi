namespace AskARabbiLIB.CurrentEvents;

/// <summary>Bounds free RSS and Atom research work.</summary>
public sealed record FreeRssCurrentEventsOptions
{
    /// <summary>Gets the timeout for one feed request.</summary>
    public TimeSpan RequestTimeout { get; init; } = TimeSpan.FromSeconds(30);

    /// <summary>Gets the maximum accepted response size for one feed.</summary>
    public int MaximumFeedBytes { get; init; } = 2_000_000;

    /// <summary>Gets the maximum recent items retained from one publisher.</summary>
    public int MaximumItemsPerFeed { get; init; } = 50;

    /// <summary>Gets the maximum combined items returned to research.</summary>
    public int MaximumTotalItems { get; init; } = 150;

    /// <summary>Validates request and result bounds.</summary>
    public void Validate()
    {
        if (RequestTimeout <= TimeSpan.Zero || RequestTimeout > TimeSpan.FromMinutes(2))
        {
            throw new ArgumentOutOfRangeException(nameof(RequestTimeout), "A feed timeout must be greater than zero and no more than two minutes.");
        }
        if (MaximumFeedBytes is < 16_384 or > 10_000_000)
        {
            throw new ArgumentOutOfRangeException(nameof(MaximumFeedBytes), "Maximum feed bytes must be between 16 KiB and 10 MB.");
        }
        if (MaximumItemsPerFeed is < 1 or > 200)
        {
            throw new ArgumentOutOfRangeException(nameof(MaximumItemsPerFeed), "Maximum items per feed must be between one and two hundred.");
        }
        if (MaximumTotalItems is < 2 or > 500)
        {
            throw new ArgumentOutOfRangeException(nameof(MaximumTotalItems), "Maximum total items must be between two and five hundred.");
        }
    }
}
