using AskARabbiLIB.Secrets;
using Azure.Core;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace AskARabbiLIB.Tests;

[TestClass]
public sealed class AzureKeyVaultSecretStoreTests
{
    [TestMethod]
    [TestCategory("Unit")]
    public void Constructor_InvalidEndpoints_ThrowArgumentException()
    {
        // Act and assert
        Assert.ThrowsExactly<ArgumentException>(() => new AzureKeyVaultSecretStore(null!));
        Assert.ThrowsExactly<ArgumentException>(() => new AzureKeyVaultSecretStore(new Uri("relative", UriKind.Relative)));
        Assert.ThrowsExactly<ArgumentException>(() => new AzureKeyVaultSecretStore(new Uri("http://vault.example.test")));
    }

    [TestMethod]
    [TestCategory("Unit")]
    public void Constructor_ValidEndpointWithExplicitOrDefaultDependencies_DoesNotPerformNetworkWork()
    {
        // Arrange
        var endpoint = new Uri("https://vault.example.test");
        var time = new ManualTimeProvider(new DateTimeOffset(2026, 8, 22, 0, 0, 0, TimeSpan.Zero));

        // Act
        using var explicitDependencies = new AzureKeyVaultSecretStore(endpoint, new NoOpTokenCredential(), time);
        using var defaultDependencies = new AzureKeyVaultSecretStore(endpoint);

        // Assert
        Assert.IsNotNull(explicitDependencies);
        Assert.IsNotNull(defaultDependencies);
    }

    [TestMethod]
    [TestCategory("Unit")]
    public void Constructor_NullInternalDependencies_ThrowArgumentNullException()
    {
        // Arrange
        var time = new ManualTimeProvider(new DateTimeOffset(2026, 8, 22, 0, 0, 0, TimeSpan.Zero));
        var client = new QueueKeyVaultClient("value");

        // Act and assert
        Assert.ThrowsExactly<ArgumentNullException>(() => new AzureKeyVaultSecretStore(null!, time));
        Assert.ThrowsExactly<ArgumentNullException>(() => new AzureKeyVaultSecretStore(client, null!));
    }

    [TestMethod]
    [TestCategory("Unit")]
    public async Task GetSecretAsync_WithinCacheWindow_LoadsOnlyOnce()
    {
        // Arrange
        var client = new QueueKeyVaultClient("value-one");
        var time = new ManualTimeProvider(new DateTimeOffset(2026, 8, 22, 0, 0, 0, TimeSpan.Zero));
        using var store = new AzureKeyVaultSecretStore(client, time);

        // Act
        var first = await store.GetSecretAsync("ApiKey");
        time.Advance(TimeSpan.FromMinutes(14));
        var second = await store.GetSecretAsync("ApiKey");

        // Assert
        Assert.AreEqual("value-one", first);
        Assert.AreEqual(first, second);
        Assert.AreEqual(1, client.CallCount);
    }

    [TestMethod]
    [TestCategory("Unit")]
    public async Task GetSecretAsync_AfterExpiration_RefreshesValue()
    {
        // Arrange
        var client = new QueueKeyVaultClient("value-one", "value-two");
        var time = new ManualTimeProvider(new DateTimeOffset(2026, 8, 22, 0, 0, 0, TimeSpan.Zero));
        using var store = new AzureKeyVaultSecretStore(client, time);
        await store.GetSecretAsync("ApiKey");
        time.Advance(TimeSpan.FromMinutes(16));

        // Act
        var value = await store.GetSecretAsync("ApiKey");

        // Assert
        Assert.AreEqual("value-two", value);
        Assert.AreEqual(2, client.CallCount);
    }

    [TestMethod]
    [TestCategory("Unit")]
    public async Task GetSecretAsync_SlowProvider_CacheWindowStartsAfterProviderCompletes()
    {
        // Arrange
        var time = new ManualTimeProvider(new DateTimeOffset(2026, 8, 22, 0, 0, 0, TimeSpan.Zero));
        var client = new AdvancingKeyVaultClient(time, TimeSpan.FromMinutes(10), "value-one", "value-two");
        using var store = new AzureKeyVaultSecretStore(client, time);

        // Act
        var first = await store.GetSecretAsync("ApiKey");
        time.Advance(TimeSpan.FromMinutes(14));
        var cached = await store.GetSecretAsync("ApiKey");
        time.Advance(TimeSpan.FromMinutes(1));
        var refreshed = await store.GetSecretAsync("ApiKey");

        // Assert
        Assert.AreEqual("value-one", first);
        Assert.AreEqual(first, cached);
        Assert.AreEqual("value-two", refreshed);
        Assert.AreEqual(2, client.CallCount);
    }

    [TestMethod]
    [TestCategory("Unit")]
    public async Task RefreshAsync_ValidCachedValue_ForcesProviderCall()
    {
        // Arrange
        var client = new QueueKeyVaultClient("value-one", "value-two");
        var time = new ManualTimeProvider(new DateTimeOffset(2026, 8, 22, 0, 0, 0, TimeSpan.Zero));
        using var store = new AzureKeyVaultSecretStore(client, time);
        await store.GetSecretAsync("ApiKey");

        // Act
        var value = await store.RefreshAsync("ApiKey");

        // Assert
        Assert.AreEqual("value-two", value);
        Assert.AreEqual(2, client.CallCount);
    }

    [TestMethod]
    [TestCategory("Unit")]
    public async Task GetSecretAsync_ConcurrentMisses_CoalescesProviderCall()
    {
        // Arrange
        var client = new BlockingKeyVaultClient();
        var time = new ManualTimeProvider(new DateTimeOffset(2026, 8, 22, 0, 0, 0, TimeSpan.Zero));
        using var store = new AzureKeyVaultSecretStore(client, time);

        // Act
        var firstTask = store.GetSecretAsync("ApiKey");
        await client.Started.Task;
        var secondTask = store.GetSecretAsync("ApiKey");
        client.Release.TrySetResult();
        var values = await Task.WhenAll(firstTask, secondTask);

        // Assert
        CollectionAssert.AreEqual(new[] { "shared", "shared" }, values);
        Assert.AreEqual(1, client.CallCount);
    }

    [TestMethod]
    [TestCategory("Unit")]
    public async Task GetSecretAsync_EmptyProviderValue_DoesNotCacheFailure()
    {
        // Arrange
        var client = new QueueKeyVaultClient(null, "recovered");
        var time = new ManualTimeProvider(new DateTimeOffset(2026, 8, 22, 0, 0, 0, TimeSpan.Zero));
        using var store = new AzureKeyVaultSecretStore(client, time);

        // Act + Assert
        await Assert.ThrowsExactlyAsync<InvalidDataException>(() => store.GetSecretAsync("ApiKey"));
        Assert.AreEqual("recovered", await store.GetSecretAsync("ApiKey"));
        Assert.AreEqual(2, client.CallCount);
    }

    [TestMethod]
    [TestCategory("Unit")]
    public async Task GetSecretAsync_Canceled_PropagatesCancellation()
    {
        // Arrange
        using var cancellationSource = new CancellationTokenSource();
        cancellationSource.Cancel();
        var client = new CancellationKeyVaultClient();
        var time = new ManualTimeProvider(new DateTimeOffset(2026, 8, 22, 0, 0, 0, TimeSpan.Zero));
        using var store = new AzureKeyVaultSecretStore(client, time);

        // Act + Assert
        await Assert.ThrowsAsync<OperationCanceledException>(() => store.GetSecretAsync("ApiKey", cancellationSource.Token));
    }

    [TestMethod]
    [TestCategory("Unit")]
    public async Task GetSecretAsync_TrimmedCaseInsensitiveName_ReusesCachedValueAtBoundary()
    {
        // Arrange
        var client = new QueueKeyVaultClient("value-one", "value-two");
        var time = new ManualTimeProvider(new DateTimeOffset(2026, 8, 22, 0, 0, 0, TimeSpan.Zero));
        using var store = new AzureKeyVaultSecretStore(client, time);

        // Act
        var first = await store.GetSecretAsync(" ApiKey ");
        var cached = await store.GetSecretAsync("apikey");
        time.Advance(AzureKeyVaultSecretStore.CacheDuration);
        var expired = await store.GetSecretAsync("APIKEY");

        // Assert
        Assert.AreEqual("value-one", first);
        Assert.AreEqual(first, cached);
        Assert.AreEqual("value-two", expired);
        Assert.AreEqual(2, client.CallCount);
    }

    [TestMethod]
    [DataRow(null)]
    [DataRow("")]
    [DataRow("   ")]
    [TestCategory("Unit")]
    public async Task GetSecretAsync_InvalidName_ThrowsArgumentException(string? name)
    {
        // Arrange
        var client = new QueueKeyVaultClient("value");
        var time = new ManualTimeProvider(new DateTimeOffset(2026, 8, 22, 0, 0, 0, TimeSpan.Zero));
        using var store = new AzureKeyVaultSecretStore(client, time);

        // Act and assert
        if (name is null)
        {
            await Assert.ThrowsExactlyAsync<ArgumentNullException>(() => store.GetSecretAsync(name!));
        }
        else
        {
            await Assert.ThrowsExactlyAsync<ArgumentException>(() => store.GetSecretAsync(name));
        }
        Assert.AreEqual(0, client.CallCount);
    }

    [TestMethod]
    [TestCategory("Unit")]
    public async Task Dispose_CalledTwiceThenUsed_ThrowsObjectDisposedException()
    {
        // Arrange
        var client = new QueueKeyVaultClient("value");
        var time = new ManualTimeProvider(new DateTimeOffset(2026, 8, 22, 0, 0, 0, TimeSpan.Zero));
        var store = new AzureKeyVaultSecretStore(client, time);

        // Act
        store.Dispose();
        store.Dispose();

        // Assert
        await Assert.ThrowsExactlyAsync<ObjectDisposedException>(() => store.GetSecretAsync("ApiKey"));
    }

    private sealed class ManualTimeProvider : TimeProvider
    {
        private DateTimeOffset utcNow;

        internal ManualTimeProvider(DateTimeOffset utcNow)
        {
            this.utcNow = utcNow;
        }

        public override DateTimeOffset GetUtcNow() => utcNow;

        internal void Advance(TimeSpan duration) => utcNow = utcNow.Add(duration);
    }

    private sealed class QueueKeyVaultClient : IKeyVaultClient
    {
        private readonly Queue<string?> values;

        internal QueueKeyVaultClient(params string?[] values)
        {
            this.values = new Queue<string?>(values);
        }

        internal int CallCount { get; private set; }

        public Task<string?> GetSecretAsync(string name, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            CallCount++;
            return Task.FromResult(values.Dequeue());
        }
    }

    private sealed class BlockingKeyVaultClient : IKeyVaultClient
    {
        internal TaskCompletionSource Started { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        internal TaskCompletionSource Release { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        internal int CallCount { get; private set; }

        public async Task<string?> GetSecretAsync(string name, CancellationToken cancellationToken)
        {
            CallCount++;
            Started.TrySetResult();
            await Release.Task.WaitAsync(cancellationToken);
            return "shared";
        }
    }

    private sealed class AdvancingKeyVaultClient : IKeyVaultClient
    {
        private readonly ManualTimeProvider timeProvider;
        private readonly TimeSpan firstCallDuration;
        private readonly Queue<string> values;

        internal AdvancingKeyVaultClient(ManualTimeProvider timeProvider, TimeSpan firstCallDuration, params string[] values)
        {
            this.timeProvider = timeProvider;
            this.firstCallDuration = firstCallDuration;
            this.values = new Queue<string>(values);
        }

        internal int CallCount { get; private set; }

        public Task<string?> GetSecretAsync(string name, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            CallCount++;
            if (CallCount == 1)
            {
                timeProvider.Advance(firstCallDuration);
            }

            return Task.FromResult<string?>(values.Dequeue());
        }
    }

    private sealed class CancellationKeyVaultClient : IKeyVaultClient
    {
        public Task<string?> GetSecretAsync(string name, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult<string?>("unexpected");
        }
    }

    private sealed class NoOpTokenCredential : TokenCredential
    {
        public override AccessToken GetToken(TokenRequestContext requestContext, CancellationToken cancellationToken) => new("token", DateTimeOffset.MaxValue);

        public override ValueTask<AccessToken> GetTokenAsync(TokenRequestContext requestContext, CancellationToken cancellationToken) => ValueTask.FromResult(new AccessToken("token", DateTimeOffset.MaxValue));
    }
}
