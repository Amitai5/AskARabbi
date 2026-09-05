using AskARabbi.Api.Authentication;
using AskARabbiLIB.Accounts;

namespace AskARabbi.Api.Accounts;

/// <summary>Finishes durable erasures without losing the recovery record on partial failure.</summary>
/// <param name="data">Owner-scoped data erasure and recovery storage.</param>
/// <param name="authentication">Provider identity management.</param>
/// <param name="logger">Structured recovery logger.</param>
public sealed class AccountDeletionService(IUserDataStore data, IUserAuthenticationService authentication, ILogger<AccountDeletionService> logger)
{
    /// <summary>Deletes the provider identity and then every owned application record.</summary>
    /// <param name="deletion">Previously accepted deletion request.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>True when complete; false when the persisted request needs another attempt.</returns>
    public async Task<bool> TryCompleteAsync(PendingAccountDeletion deletion, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(deletion);
        try
        {
            await authentication.DeleteUserAsync(deletion.ProviderUserId, cancellationToken).ConfigureAwait(false);
            await data.CompleteDeletionAsync(deletion.UserId, cancellationToken).ConfigureAwait(false);
            logger.LogInformation("Account erasure completed for {UserId}.", deletion.UserId);
            return true;
        }
        catch (Exception exception) when (exception is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
        {
            // This is a recovery boundary, not a successful deletion. The durable marker stays in MongoDB.
            logger.LogError(exception, "Account erasure for {UserId} is pending retry.", deletion.UserId);
            return false;
        }
    }
}
