namespace AskARabbiLIB.Accounts;

/// <summary>Coordinates owner-scoped erasure with concurrent application writes across API replicas.</summary>
public interface IUserDataStore
{
    /// <summary>Acquires a bounded write lease unless deletion or an incompatible operation is active.</summary>
    /// <param name="userId">Authenticated application account ID.</param>
    /// <param name="operationId">Unique request ID.</param>
    /// <param name="exclusive">Whether all other writes must be excluded.</param>
    /// <param name="now">Current UTC time.</param>
    /// <param name="expiresAt">Crash-recovery deadline, later than the request timeout.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Whether the lease was acquired.</returns>
    Task<bool> TryAcquireAsync(Guid userId, Guid operationId, bool exclusive, DateTimeOffset now, DateTimeOffset expiresAt, CancellationToken cancellationToken = default);

    /// <summary>Releases only the specified request's write lease.</summary>
    /// <param name="userId">Account ID.</param>
    /// <param name="operationId">Lease ID.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The completion task.</returns>
    Task ReleaseAsync(Guid userId, Guid operationId, CancellationToken cancellationToken = default);

    /// <summary>Durably disables an idle account and queues its erasure.</summary>
    /// <param name="userId">Account ID.</param>
    /// <param name="now">Current UTC time.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The pending deletion, or null if missing, pending, or busy.</returns>
    Task<PendingAccountDeletion?> TryRequestDeletionAsync(Guid userId, DateTimeOffset now, CancellationToken cancellationToken = default);

    /// <summary>Lists unfinished erasures for recovery after an interrupted request.</summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Pending deletions, including requests after repeatedly failing accounts.</returns>
    Task<IReadOnlyList<PendingAccountDeletion>> ListPendingDeletionsAsync(CancellationToken cancellationToken = default);

    /// <summary>Deletes every conversation and message for one owner, including orphan messages.</summary>
    /// <param name="userId">Account ID, never a client-supplied owner override.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The completion task.</returns>
    Task DeleteChatsAsync(Guid userId, CancellationToken cancellationToken = default);

    /// <summary>Erases owned application data, then removes the pending account record last.</summary>
    /// <param name="userId">Account ID.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The completion task.</returns>
    Task CompleteDeletionAsync(Guid userId, CancellationToken cancellationToken = default);
}
