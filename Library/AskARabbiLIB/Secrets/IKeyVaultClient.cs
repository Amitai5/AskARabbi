namespace AskARabbiLIB.Secrets;

internal interface IKeyVaultClient
{
    /// <summary>Gets a secret value from the backing provider.</summary>
    /// <param name="name">Secret name.</param>
    /// <param name="cancellationToken">Token used to cancel the request.</param>
    /// <returns>The secret value, or null when the provider returns no value.</returns>
    Task<string?> GetSecretAsync(string name, CancellationToken cancellationToken);
}
