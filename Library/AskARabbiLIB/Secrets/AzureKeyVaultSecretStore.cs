using System.Collections.Concurrent;
using Azure.Core;
using Azure.Identity;
using Azure.Security.KeyVault.Secrets;

namespace AskARabbiLIB.Secrets;

/// <summary>Provides lazy, cancellation-aware Azure Key Vault access with bounded in-memory caching.</summary>
public sealed class AzureKeyVaultSecretStore : ISecretStore, IDisposable
{
    /// <summary>Gets the duration for which a successfully retrieved value remains cached.</summary>
    public static readonly TimeSpan CacheDuration = TimeSpan.FromMinutes(15);

    private readonly IKeyVaultClient client;
    private readonly TimeProvider timeProvider;
    private readonly ConcurrentDictionary<string, SecretCacheEntry> cache = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<string, SemaphoreSlim> gates = new(StringComparer.OrdinalIgnoreCase);
    private int disposeState;

    /// <summary>Creates a lazy Key Vault store without issuing a network request.</summary>
    /// <param name="endpoint">HTTPS Key Vault endpoint.</param>
    /// <param name="credential">Optional Entra credential; defaults to DefaultAzureCredential.</param>
    /// <param name="timeProvider">Optional time source for cache expiration.</param>
    public AzureKeyVaultSecretStore(Uri endpoint, TokenCredential? credential = null, TimeProvider? timeProvider = null)
    {
        if (endpoint is null || !endpoint.IsAbsoluteUri || !string.Equals(endpoint.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException("Key Vault endpoint must be an absolute HTTPS URI.", nameof(endpoint));
        }
        client = new AzureKeyVaultClient(new SecretClient(endpoint, credential ?? new DefaultAzureCredential()));
        this.timeProvider = timeProvider ?? TimeProvider.System;
    }

    internal AzureKeyVaultSecretStore(IKeyVaultClient client, TimeProvider timeProvider)
    {
        ArgumentNullException.ThrowIfNull(client);
        ArgumentNullException.ThrowIfNull(timeProvider);
        this.client = client;
        this.timeProvider = timeProvider;
    }

    /// <inheritdoc cref="ISecretStore.GetSecretAsync"/>
    public Task<string> GetSecretAsync(string name, CancellationToken cancellationToken = default) => GetCoreAsync(name, false, cancellationToken);

    /// <inheritdoc cref="ISecretStore.RefreshAsync"/>
    public Task<string> RefreshAsync(string name, CancellationToken cancellationToken = default) => GetCoreAsync(name, true, cancellationToken);

    /// <summary>Marks the store as disposed and clears cached secret values.</summary>
    public void Dispose()
    {
        if (Interlocked.Exchange(ref disposeState, 1) != 0)
        {
            return;
        }

        // SemaphoreSlim has no unmanaged resources. Leaving active gates undisposed avoids
        // racing with requests that were already in flight when the host began shutting down.
        gates.Clear();
        cache.Clear();
    }

    private async Task<string> GetCoreAsync(string name, bool forceRefresh, CancellationToken cancellationToken)
    {
        ObjectDisposedException.ThrowIf(Volatile.Read(ref disposeState) != 0, this);
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        var normalizedName = name.Trim();
        var now = timeProvider.GetUtcNow();
        if (!forceRefresh && cache.TryGetValue(normalizedName, out var cached) && cached.ExpiresAtUtc > now)
        {
            return cached.Value;
        }

        var gate = gates.GetOrAdd(normalizedName, static _ => new SemaphoreSlim(1, 1));
        await gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            now = timeProvider.GetUtcNow();
            if (!forceRefresh && cache.TryGetValue(normalizedName, out cached) && cached.ExpiresAtUtc > now)
            {
                return cached.Value;
            }

            var value = await client.GetSecretAsync(normalizedName, cancellationToken).ConfigureAwait(false);
            if (string.IsNullOrWhiteSpace(value))
            {
                throw new InvalidDataException($"Key Vault secret '{normalizedName}' has no value.");
            }

            var expiresAtUtc = timeProvider.GetUtcNow().Add(CacheDuration);
            cache[normalizedName] = new SecretCacheEntry(value, expiresAtUtc);
            return value;
        }
        finally
        {
            gate.Release();
        }
    }

    private sealed record SecretCacheEntry(string Value, DateTimeOffset ExpiresAtUtc);
}
