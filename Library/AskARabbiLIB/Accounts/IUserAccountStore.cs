namespace AskARabbiLIB.Accounts;

/// <summary>Stores and resolves AskRabbi user accounts.</summary>
public interface IUserAccountStore
{
    /// <summary>Creates or updates the account connected to a verified external identity.</summary>
    /// <param name="identity">Verified external identity.</param>
    /// <param name="updatedAtUtc">UTC time of the identity update.</param>
    /// <param name="cancellationToken">Token that can cancel the operation.</param>
    /// <returns>The resulting AskRabbi account.</returns>
    Task<UserAccount> UpsertAsync(ExternalUserIdentity identity, DateTimeOffset updatedAtUtc, CancellationToken cancellationToken = default);

    /// <summary>Gets an account by its AskRabbi user ID.</summary>
    /// <param name="userId">AskRabbi user ID.</param>
    /// <param name="cancellationToken">Token that can cancel the operation.</param>
    /// <returns>The account when found; otherwise, <see langword="null"/>.</returns>
    Task<UserAccount?> GetByIdAsync(Guid userId, CancellationToken cancellationToken = default);
}
