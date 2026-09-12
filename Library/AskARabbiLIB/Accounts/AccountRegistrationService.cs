using AskARabbiLIB.Persistence.Mongo;

namespace AskARabbiLIB.Accounts;

/// <summary>Admits public signups within capacity while allowing existing identities to sign in.</summary>
public sealed class AccountRegistrationService
{
    private readonly IAccountRegistrationStore registrations;
    private readonly IUserAccountStore accounts;
    private readonly AccountRegistrationOptions options;

    /// <summary>Initializes account admission.</summary>
    /// <param name="registrations">Durable admission coordinator.</param>
    /// <param name="accounts">Account persistence.</param>
    /// <param name="options">Registration ceiling.</param>
    public AccountRegistrationService(IAccountRegistrationStore registrations, IUserAccountStore accounts, AccountRegistrationOptions options)
    {
        this.registrations = registrations ?? throw new ArgumentNullException(nameof(registrations));
        this.accounts = accounts ?? throw new ArgumentNullException(nameof(accounts));
        this.options = options ?? throw new ArgumentNullException(nameof(options));
        options.Validate();
    }

    /// <summary>Checks whether the public signup entry point should be offered; final admission is checked separately.</summary>
    /// <param name="cancellationToken">Token that can cancel the operation.</param>
    /// <returns>True when a new identity may attempt registration.</returns>
    public async Task<bool> IsOpenAsync(CancellationToken cancellationToken = default)
    {
        var snapshot = await registrations.ReadAsync(null, cancellationToken).ConfigureAwait(false);
        return options.AccountLimit == 0 || snapshot.OccupiedPlaces < options.AccountLimit;
    }

    /// <summary>Creates or updates a verified identity, returning null when new registrations are full.</summary>
    /// <param name="identity">Identity obtained from the authentication provider, never from browser input.</param>
    /// <param name="updatedAtUtc">Time of the account update.</param>
    /// <param name="cancellationToken">Token that can cancel the operation.</param>
    /// <returns>The persisted account, or null if there is no place for a new identity.</returns>
    public async Task<UserAccount?> TrySignInAsync(ExternalUserIdentity identity, DateTimeOffset updatedAtUtc, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(identity);
        ArgumentException.ThrowIfNullOrWhiteSpace(identity.ProviderUserId);
        ArgumentException.ThrowIfNullOrWhiteSpace(identity.Email);
        var operationId = Guid.NewGuid();
        for (var attempt = 0; attempt < 64; attempt++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var snapshot = await registrations.ReadAsync(identity.ProviderUserId, cancellationToken).ConfigureAwait(false);
            if (!snapshot.HasPlace && options.AccountLimit != 0 && snapshot.OccupiedPlaces >= options.AccountLimit)
            {
                return null;
            }
            if (!await registrations.TryReserveAsync(snapshot.Revision, operationId, identity.ProviderUserId, cancellationToken).ConfigureAwait(false))
            {
                continue;
            }

            // Do not release on cancellation/unknown write failure: the write may have committed on another replica.
            // Reservations are durable, and a retry of this verified identity still has its place at capacity.
            var account = await accounts.UpsertAsync(identity, updatedAtUtc, cancellationToken).ConfigureAwait(false);
            await registrations.CompleteAsync(operationId, identity.ProviderUserId, cancellationToken).ConfigureAwait(false);
            return account;
        }

        throw new PersistenceUnavailableException();
    }
}
