using Azure;
using Azure.Security.KeyVault.Secrets;

namespace AskARabbiLIB.Secrets;

internal sealed class AzureKeyVaultClient : IKeyVaultClient
{
    private readonly SecretClient client;

    internal AzureKeyVaultClient(SecretClient client)
    {
        ArgumentNullException.ThrowIfNull(client);
        this.client = client;
    }

    /// <inheritdoc cref="IKeyVaultClient.GetSecretAsync"/>
    public async Task<string?> GetSecretAsync(string name, CancellationToken cancellationToken)
    {
        Response<KeyVaultSecret> response = await client.GetSecretAsync(name, cancellationToken: cancellationToken).ConfigureAwait(false);
        return response.Value.Value;
    }
}
