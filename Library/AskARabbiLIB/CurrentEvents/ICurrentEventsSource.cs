namespace AskARabbiLIB.CurrentEvents;

/// <summary>Retrieves recent public current-events metadata without requiring a paid news subscription.</summary>
public interface ICurrentEventsSource
{
    /// <summary>Gets bounded items published within an inclusive UTC research window.</summary>
    /// <param name="fromUtc">Inclusive beginning of the research window.</param>
    /// <param name="throughUtc">Inclusive end of the research window.</param>
    /// <param name="cancellationToken">Token that can cancel feed retrieval.</param>
    /// <returns>Deduplicated current-events items ordered from newest to oldest.</returns>
    Task<IReadOnlyList<CurrentEventItem>> GetRecentAsync(DateTimeOffset fromUtc, DateTimeOffset throughUtc, CancellationToken cancellationToken = default);
}
