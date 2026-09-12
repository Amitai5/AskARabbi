namespace AskARabbiLIB.Accounts;

/// <summary>Coordinates account admission across API replicas using durable, revision-checked reservations.</summary>
public interface IAccountRegistrationStore
{
    /// <summary>Reads a revisioned snapshot of registered and reserved places.</summary>
    /// <param name="providerUserId">Verified identity to check, or null for anonymous availability.</param>
    /// <param name="cancellationToken">Token that can cancel the operation.</param>
    /// <returns>A snapshot whose revision must be checked before admitting an identity.</returns>
    Task<AccountRegistrationSnapshot> ReadAsync(string? providerUserId, CancellationToken cancellationToken = default);

    /// <summary>Atomically reserves a place only if the observed admission revision is still current.</summary>
    /// <param name="revision">Observed revision.</param>
    /// <param name="operationId">Unique registration operation ID.</param>
    /// <param name="providerUserId">Verified identity being admitted.</param>
    /// <param name="cancellationToken">Token that can cancel the operation.</param>
    /// <returns>True when reserved; false when the caller must read and check capacity again.</returns>
    Task<bool> TryReserveAsync(long revision, Guid operationId, string providerUserId, CancellationToken cancellationToken = default);

    /// <summary>Releases only this operation's reservation after the account write is acknowledged.</summary>
    /// <param name="operationId">Completed registration operation.</param>
    /// <param name="providerUserId">Identity whose account write was acknowledged.</param>
    /// <param name="cancellationToken">Token that can cancel the operation.</param>
    /// <returns>A task representing durable reservation completion.</returns>
    Task CompleteAsync(Guid operationId, string providerUserId, CancellationToken cancellationToken = default);
}
