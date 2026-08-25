namespace AskARabbiLIB.Secrets;

/// <summary>Retrieves optional service secrets through a provider-neutral boundary.</summary>
public interface ISecretStore
{
    /// <summary>Gets a secret, using a valid cached value when available.</summary>
    /// <param name="name">Secret name.</param>
    /// <param name="cancellationToken">Token used to cancel provider access.</param>
    /// <returns>The nonempty secret value.</returns>
    Task<string> GetSecretAsync(string name, CancellationToken cancellationToken = default);

    /// <summary>Forces a provider refresh and replaces any cached value.</summary>
    /// <param name="name">Secret name.</param>
    /// <param name="cancellationToken">Token used to cancel provider access.</param>
    /// <returns>The refreshed nonempty secret value.</returns>
    Task<string> RefreshAsync(string name, CancellationToken cancellationToken = default);
}
