using AskARabbiLIB.Accounts;
using AskARabbiLIB.Persistence.InMemory;
using AskARabbiLIB.Persistence.Mongo;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace AskARabbiLIB.Tests;

[TestClass]
public sealed class AccountRegistrationServiceTests
{
    private static readonly DateTimeOffset Now = new(2026, 9, 11, 12, 0, 0, TimeSpan.Zero);

    [TestMethod]
    public void Options_DefaultAndUnlimited_AreValidButNegativeIsRejected()
    {
        Assert.AreEqual(100, new AccountRegistrationOptions().AccountLimit);
        new AccountRegistrationOptions { AccountLimit = 0 }.Validate();
        Assert.Throws<InvalidOperationException>(() => new AccountRegistrationOptions { AccountLimit = -1 }.Validate());
    }

    [TestMethod]
    public async Task TrySignInAsync_LastPlace_Accepts100thAndRejects101st()
    {
        var accounts = new Accounts(99);
        var service = Create(accounts);

        var last = await service.TrySignInAsync(Identity("last"), Now);
        var excess = await service.TrySignInAsync(Identity("excess"), Now);

        Assert.IsNotNull(last);
        Assert.IsNull(excess);
        Assert.AreEqual(100, accounts.Identities().Count);
        Assert.IsFalse(await service.IsOpenAsync());
        Assert.AreEqual(1, accounts.Writes);
    }

    [TestMethod]
    [DataRow(100)]
    [DataRow(10)]
    public async Task TrySignInAsync_ExistingIdentityAtOrAboveLimit_StillSignsIn(int limit)
    {
        var accounts = new Accounts(100);
        var service = Create(accounts, limit);

        var user = await service.TrySignInAsync(Identity("user-1") with { FirstName = "Updated" }, Now);
        var newUser = await service.TrySignInAsync(Identity("new"), Now);

        Assert.IsNotNull(user);
        Assert.AreEqual("Updated", user.FirstName);
        Assert.IsNull(newUser);
        Assert.AreEqual(100, accounts.Identities().Count);
    }

    [TestMethod]
    public async Task TrySignInAsync_ZeroLimit_AcceptsAccountsBeyondDefaultCeiling()
    {
        var accounts = new Accounts(150);
        var service = Create(accounts, 0);

        Assert.IsTrue(await service.IsOpenAsync());
        Assert.IsNotNull(await service.TrySignInAsync(Identity("new"), Now));
        Assert.AreEqual(151, accounts.Identities().Count);
    }

    [TestMethod]
    public async Task TrySignInAsync_SeparateServicesCompeteForLastPlace_OnlyOneIsAdmitted()
    {
        var accounts = new Accounts(99);
        var registrations = new RacingRegistrations(new InMemoryAccountRegistrationStore(accounts.Identities));
        var first = new AccountRegistrationService(registrations, accounts, new());
        var second = new AccountRegistrationService(registrations, accounts, new());

        var results = await Task.WhenAll(first.TrySignInAsync(Identity("first"), Now), second.TrySignInAsync(Identity("second"), Now));

        Assert.AreEqual(1, results.Count(value => value is not null));
        Assert.AreEqual(100, accounts.Identities().Count);
        Assert.AreEqual(1, accounts.Writes);
    }

    [TestMethod]
    public async Task TrySignInAsync_ConcurrentCallbacksForSameIdentity_ConsumeOnePlace()
    {
        var accounts = new Accounts(99);
        var registrations = new RacingRegistrations(new InMemoryAccountRegistrationStore(accounts.Identities));
        var service = new AccountRegistrationService(registrations, accounts, new());

        var results = await Task.WhenAll(service.TrySignInAsync(Identity("same"), Now), service.TrySignInAsync(Identity("same"), Now));

        Assert.IsTrue(results.All(value => value is not null));
        Assert.AreEqual(results[0]?.Id, results[1]?.Id);
        Assert.AreEqual(100, accounts.Identities().Count);
    }

    [TestMethod]
    public async Task TrySignInAsync_UncertainPersistenceFailure_RetainsPlaceAndAllowsVerifiedIdentityToRetry()
    {
        var accounts = new Accounts(99) { FailNextWrite = true };
        var service = Create(accounts);

        await Assert.ThrowsAsync<IOException>(() => service.TrySignInAsync(Identity("interrupted"), Now));

        Assert.IsFalse(await service.IsOpenAsync());
        Assert.IsNull(await service.TrySignInAsync(Identity("other"), Now));
        Assert.IsNotNull(await service.TrySignInAsync(Identity("interrupted"), Now));
        Assert.AreEqual(100, accounts.Identities().Count);
    }

    [TestMethod]
    public async Task IsOpenAsync_AccountErased_ReopensRegistration()
    {
        var accounts = new Accounts(100);
        var service = Create(accounts);
        Assert.IsFalse(await service.IsOpenAsync());

        accounts.Remove("user-1");

        Assert.IsTrue(await service.IsOpenAsync());
        Assert.IsNotNull(await service.TrySignInAsync(Identity("replacement"), Now));
        Assert.IsFalse(await service.IsOpenAsync());
    }

    [TestMethod]
    public async Task TrySignInAsync_StorageUnavailable_DoesNotWriteAccount()
    {
        var accounts = new Accounts();
        var service = new AccountRegistrationService(new UnavailableApplicationStore(), accounts, new());

        await Assert.ThrowsAsync<PersistenceUnavailableException>(() => service.TrySignInAsync(Identity("new"), Now));

        Assert.AreEqual(0, accounts.Writes);
    }

    [TestMethod]
    public async Task TrySignInAsync_RepeatedReservationConflicts_FailsClosedWithoutUnboundedRetry()
    {
        var accounts = new Accounts();
        var registrations = new ConflictingRegistrations();
        var service = new AccountRegistrationService(registrations, accounts, new());

        await Assert.ThrowsAsync<PersistenceUnavailableException>(() => service.TrySignInAsync(Identity("new"), Now));

        Assert.AreEqual(64, registrations.Attempts);
        Assert.AreEqual(0, accounts.Writes);
    }

    [TestMethod]
    public async Task TrySignInAsync_CancelledOrInvalidIdentity_DoesNotReserveOrWrite()
    {
        var accounts = new Accounts();
        var service = Create(accounts);

        await Assert.ThrowsAsync<OperationCanceledException>(() => service.TrySignInAsync(Identity("new"), Now, new CancellationToken(true)));
        await Assert.ThrowsAsync<ArgumentException>(() => service.TrySignInAsync(Identity(" "), Now));
        await Assert.ThrowsAsync<ArgumentNullException>(() => service.TrySignInAsync(null!, Now));

        Assert.AreEqual(0, accounts.Writes);
        Assert.IsTrue(await service.IsOpenAsync());
    }

    private static AccountRegistrationService Create(Accounts accounts, int limit = 100) => new(new InMemoryAccountRegistrationStore(accounts.Identities), accounts, new() { AccountLimit = limit });
    private static ExternalUserIdentity Identity(string id) => new() { ProviderUserId = id, Email = $"{id}@example.test", IsEmailVerified = true };

    private sealed class ConflictingRegistrations : IAccountRegistrationStore
    {
        internal int Attempts { get; private set; }
        public Task<AccountRegistrationSnapshot> ReadAsync(string? providerUserId, CancellationToken cancellationToken = default) => Task.FromResult(new AccountRegistrationSnapshot(0, 0, false));
        public Task<bool> TryReserveAsync(long revision, Guid operationId, string providerUserId, CancellationToken cancellationToken = default)
        {
            Attempts++;
            return Task.FromResult(false);
        }
        public Task CompleteAsync(Guid operationId, string providerUserId, CancellationToken cancellationToken = default) => throw new InvalidOperationException("No place was reserved.");
    }

    private sealed class RacingRegistrations(IAccountRegistrationStore inner) : IAccountRegistrationStore
    {
        private readonly TaskCompletionSource ready = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private int reads;

        public async Task<AccountRegistrationSnapshot> ReadAsync(string? providerUserId, CancellationToken cancellationToken = default)
        {
            var snapshot = await inner.ReadAsync(providerUserId, cancellationToken);
            var read = Interlocked.Increment(ref reads);
            if (read <= 2)
            {
                if (read == 2)
                {
                    ready.SetResult();
                }
                await ready.Task;
            }
            return snapshot;
        }

        public Task<bool> TryReserveAsync(long revision, Guid operationId, string providerUserId, CancellationToken cancellationToken = default) => inner.TryReserveAsync(revision, operationId, providerUserId, cancellationToken);
        public Task CompleteAsync(Guid operationId, string providerUserId, CancellationToken cancellationToken = default) => inner.CompleteAsync(operationId, providerUserId, cancellationToken);
    }

    private sealed class Accounts : IUserAccountStore
    {
        private readonly object synchronization = new();
        private readonly Dictionary<string, UserAccount> users = [];
        internal int Writes { get; private set; }
        internal bool FailNextWrite { get; set; }

        internal Accounts(int count = 0)
        {
            for (var index = 1; index <= count; index++)
            {
                var id = $"user-{index}";
                users[id] = new() { Id = new Guid(index, 0, 0, new byte[8]), ProviderUserId = id, Email = $"{id}@example.test", IsEmailVerified = true, CreatedAtUtc = Now, UpdatedAtUtc = Now };
            }
        }

        internal IReadOnlyList<string> Identities()
        {
            lock (synchronization)
            {
                return users.Keys.ToArray();
            }
        }

        internal void Remove(string id) => users.Remove(id);

        public Task<UserAccount> UpsertAsync(ExternalUserIdentity identity, DateTimeOffset updatedAtUtc, CancellationToken cancellationToken = default)
        {
            lock (synchronization)
            {
                Writes++;
                if (FailNextWrite)
                {
                    FailNextWrite = false;
                    throw new IOException("Simulated uncertain account write.");
                }
                users.TryGetValue(identity.ProviderUserId, out var previous);
                var account = new UserAccount { Id = previous?.Id ?? new Guid(users.Count + 1, 0, 0, new byte[8]), ProviderUserId = identity.ProviderUserId, Email = identity.Email, FirstName = identity.FirstName, IsEmailVerified = true, CreatedAtUtc = previous?.CreatedAtUtc ?? updatedAtUtc, UpdatedAtUtc = updatedAtUtc };
                users[identity.ProviderUserId] = account;
                return Task.FromResult(account);
            }
        }

        public Task<UserAccount?> GetByIdAsync(Guid userId, CancellationToken cancellationToken = default) => Task.FromResult(users.Values.FirstOrDefault(value => value.Id == userId));
    }
}
