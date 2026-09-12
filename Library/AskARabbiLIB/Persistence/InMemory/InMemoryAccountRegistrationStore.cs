using AskARabbiLIB.Accounts;

namespace AskARabbiLIB.Persistence.InMemory;

/// <summary>Coordinates deterministic registration for local demo services and tests, not production replicas.</summary>
public sealed class InMemoryAccountRegistrationStore : IAccountRegistrationStore
{
    private readonly object synchronization = new();
    private readonly Func<IReadOnlyList<string>> getAccountIdentities;
    private readonly Dictionary<Guid, string> reservations = [];
    private long revision;

    /// <summary>Initializes admission against the local account list.</summary>
    /// <param name="getAccountIdentities">Reads the identities currently stored locally.</param>
    public InMemoryAccountRegistrationStore(Func<IReadOnlyList<string>> getAccountIdentities)
    {
        this.getAccountIdentities = getAccountIdentities ?? throw new ArgumentNullException(nameof(getAccountIdentities));
    }

    /// <inheritdoc/>
    public Task<AccountRegistrationSnapshot> ReadAsync(string? providerUserId, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        lock (synchronization)
        {
            var identities = getAccountIdentities().Concat(reservations.Values).ToHashSet(StringComparer.Ordinal);
            return Task.FromResult(new AccountRegistrationSnapshot(revision, identities.Count, providerUserId is not null && identities.Contains(providerUserId)));
        }
    }

    /// <inheritdoc/>
    public Task<bool> TryReserveAsync(long revision, Guid operationId, string providerUserId, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        lock (synchronization)
        {
            if (this.revision != revision)
            {
                return Task.FromResult(false);
            }
            reservations.Add(operationId, providerUserId);
            this.revision++;
            return Task.FromResult(true);
        }
    }

    /// <inheritdoc/>
    public Task CompleteAsync(Guid operationId, string providerUserId, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        lock (synchronization)
        {
            reservations.Remove(operationId);
            revision++;
        }
        return Task.CompletedTask;
    }
}
