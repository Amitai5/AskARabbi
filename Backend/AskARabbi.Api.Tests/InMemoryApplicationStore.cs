using AskARabbiLIB.Accounts;
using AskARabbiLIB.Calendar;
using AskARabbiLIB.Conversations;
using AskARabbiLIB.ConversationSettings;
using AskARabbiLIB.Usage;
using AskARabbiLIB.Persistence.InMemory;

namespace AskARabbi.Api.Tests;

internal sealed class InMemoryApplicationStore : IUserAccountStore, IConversationStore, IConversationSettingsStore, IUsageStore, IUserDataStore, ICalendarPreferencesStore
{
    private readonly object dataSynchronization = new();
    private static readonly Guid StableUserId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private readonly Dictionary<Guid, Conversation> conversations = [];
    private readonly Dictionary<Guid, PersonalizationSettings> personalization = [];
    private readonly Dictionary<Guid, ConversationPreferences> preferences = [];
    private readonly Dictionary<Guid, ReadingPreferences> readingPreferences = [];
    private readonly Dictionary<Guid, CalendarPreferences> calendarPreferences = [];
    private UserAccount? account;
    internal InMemoryUsageStore TokenUsage { get; } = new();
    private readonly Dictionary<Guid, (DateTimeOffset ExpiresAt, bool Exclusive)> dataOperations = [];
    private Guid nextAccountId = StableUserId;

    public Task<CalendarPreferences?> GetCalendarPreferencesAsync(Guid userId, CancellationToken cancellationToken = default) => Task.FromResult(calendarPreferences.GetValueOrDefault(userId));
    public Task UpsertCalendarPreferencesAsync(Guid userId, CalendarPreferences preferences, DateTimeOffset updatedAtUtc, CancellationToken cancellationToken = default)
    {
        calendarPreferences[userId] = preferences;
        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public Task<bool> TryAcquireAsync(Guid userId, Guid operationId, bool exclusive, DateTimeOffset now, DateTimeOffset expiresAt, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        lock (dataSynchronization)
        {
            if (account?.Id != userId || account.IsDeletionPending || dataOperations.Values.Any(value => value.ExpiresAt > now && (exclusive || value.Exclusive)))
            {
                return Task.FromResult(false);
            }
            dataOperations[operationId] = (expiresAt, exclusive);
            return Task.FromResult(true);
        }
    }

    /// <inheritdoc/>
    public Task ReleaseAsync(Guid userId, Guid operationId, CancellationToken cancellationToken = default)
    {
        lock (dataSynchronization)
        {
            if (account?.Id == userId)
            {
                dataOperations.Remove(operationId);
            }
        }
        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public Task<PendingAccountDeletion?> TryRequestDeletionAsync(Guid userId, DateTimeOffset now, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        lock (dataSynchronization)
        {
            if (account?.Id != userId || account.IsDeletionPending || dataOperations.Values.Any(value => value.ExpiresAt > now))
            {
                return Task.FromResult<PendingAccountDeletion?>(null);
            }
            account = account with { IsDeletionPending = true };
            return Task.FromResult<PendingAccountDeletion?>(new(userId, account.ProviderUserId));
        }
    }

    /// <inheritdoc/>
    public Task<IReadOnlyList<PendingAccountDeletion>> ListPendingDeletionsAsync(CancellationToken cancellationToken = default)
    {
        lock (dataSynchronization)
        {
            return Task.FromResult<IReadOnlyList<PendingAccountDeletion>>(account?.IsDeletionPending == true ? [new(account.Id, account.ProviderUserId)] : []);
        }
    }

    /// <inheritdoc/>
    public Task DeleteChatsAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        lock (dataSynchronization)
        {
            foreach (var id in conversations.Values.Where(value => value.UserId == userId).Select(value => value.Id).ToArray())
            {
                conversations.Remove(id);
            }
        }
        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public async Task CompleteDeletionAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        if (account?.Id != userId || !account.IsDeletionPending)
        {
            return;
        }
        await DeleteChatsAsync(userId, cancellationToken);
        lock (dataSynchronization)
        {
            personalization.Remove(userId);
            preferences.Remove(userId);
            readingPreferences.Remove(userId);
            calendarPreferences.Remove(userId);
            TokenUsage.DeleteAccount(userId);
            account = null;
            nextAccountId = Guid.NewGuid();
            dataOperations.Clear();
        }
    }

    internal Guid UserId => account?.Id ?? StableUserId;

    public Task<UserAccount> UpsertAsync(ExternalUserIdentity identity, DateTimeOffset updatedAtUtc, CancellationToken cancellationToken = default)
    {
        account = new UserAccount
        {
            Id = account?.Id ?? nextAccountId,
            IsDeletionPending = account?.IsDeletionPending ?? false,
            ProviderUserId = identity.ProviderUserId,
            Email = identity.Email,
            IsEmailVerified = identity.IsEmailVerified,
            FirstName = identity.FirstName,
            LastName = identity.LastName,
            ProfileImageUrl = identity.ProfileImageUrl,
            CreatedAtUtc = account?.CreatedAtUtc ?? updatedAtUtc,
            UpdatedAtUtc = updatedAtUtc,
        };
        return Task.FromResult(account);
    }

    public Task<UserAccount?> GetByIdAsync(Guid userId, CancellationToken cancellationToken = default) => Task.FromResult(account?.Id == userId ? account : null);

    public Task<IReadOnlyList<ConversationSummary>> ListAsync(Guid userId, int limit, CancellationToken cancellationToken = default)
    {
        var values = conversations.Values
            .Where(conversation => conversation.UserId == userId)
            .OrderByDescending(conversation => conversation.UpdatedAtUtc)
            .Take(limit)
            .Select(conversation => new ConversationSummary(conversation.Id, conversation.Title, conversation.EnabledSourceKeys, conversation.UpdatedAtUtc))
            .ToArray();
        return Task.FromResult<IReadOnlyList<ConversationSummary>>(values);
    }

    public Task<Conversation?> GetAsync(Guid userId, Guid conversationId, CancellationToken cancellationToken = default)
    {
        conversations.TryGetValue(conversationId, out var conversation);
        return Task.FromResult(conversation?.UserId == userId ? conversation : null);
    }

    public Task CreateAsync(Conversation conversation, CancellationToken cancellationToken = default)
    {
        conversations.Add(conversation.Id, conversation);
        return Task.CompletedTask;
    }

    public Task<Conversation?> AppendMessageAsync(Guid userId, Guid conversationId, ConversationMessage message, DateTimeOffset updatedAtUtc, CancellationToken cancellationToken = default)
    {
        if (!conversations.TryGetValue(conversationId, out var conversation) || conversation.UserId != userId)
        {
            return Task.FromResult<Conversation?>(null);
        }

        if (conversation.Messages.All(existing => existing.Id != message.Id))
        {
            conversation = conversation with
            {
                Messages = conversation.Messages.Append(message).ToArray(),
                UpdatedAtUtc = updatedAtUtc,
            };
            conversations[conversationId] = conversation;
        }

        return Task.FromResult<Conversation?>(conversation);
    }

    public Task<bool> RenameAsync(Guid userId, Guid conversationId, string title, DateTimeOffset updatedAtUtc, CancellationToken cancellationToken = default)
    {
        if (!conversations.TryGetValue(conversationId, out var conversation) || conversation.UserId != userId)
        {
            return Task.FromResult(false);
        }

        conversations[conversationId] = conversation with { Title = title, UpdatedAtUtc = updatedAtUtc };
        return Task.FromResult(true);
    }

    public Task<bool> UpdateSourcesAsync(Guid userId, Guid conversationId, IReadOnlyList<string> sourceKeys, DateTimeOffset updatedAtUtc, CancellationToken cancellationToken = default)
    {
        if (!conversations.TryGetValue(conversationId, out var conversation) || conversation.UserId != userId)
        {
            return Task.FromResult(false);
        }

        conversations[conversationId] = conversation with { EnabledSourceKeys = sourceKeys, UpdatedAtUtc = updatedAtUtc };
        return Task.FromResult(true);
    }

    public Task<bool> DeleteAsync(Guid userId, Guid conversationId, CancellationToken cancellationToken = default)
    {
        if (!conversations.TryGetValue(conversationId, out var conversation) || conversation.UserId != userId)
        {
            return Task.FromResult(false);
        }

        return Task.FromResult(conversations.Remove(conversationId));
    }

    public Task<PersonalizationSettings?> GetPersonalizationAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        personalization.TryGetValue(userId, out var value);
        return Task.FromResult(value);
    }

    public Task UpsertPersonalizationAsync(Guid userId, PersonalizationSettings value, DateTimeOffset updatedAtUtc, CancellationToken cancellationToken = default)
    {
        personalization[userId] = value;
        return Task.CompletedTask;
    }

    public Task<ReadingPreferences?> GetReadingPreferencesAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        lock (dataSynchronization)
        {
            return Task.FromResult(readingPreferences.GetValueOrDefault(userId));
        }
    }

    /// <inheritdoc/>
    public Task UpsertReadingPreferencesAsync(Guid userId, ReadingPreferences value, DateTimeOffset updatedAtUtc, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(value);
        cancellationToken.ThrowIfCancellationRequested();
        lock (dataSynchronization)
        {
            readingPreferences[userId] = value;
        }
        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public Task<ConversationPreferences?> GetPreferencesAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        preferences.TryGetValue(userId, out var value);
        return Task.FromResult(value);
    }

    public Task UpsertPreferencesAsync(Guid userId, ConversationPreferences value, DateTimeOffset updatedAtUtc, CancellationToken cancellationToken = default)
    {
        preferences[userId] = value;
        return Task.CompletedTask;
    }

    public Task<long> GetTokenCountAsync(Guid userId, DateTimeOffset periodStartUtc, DateTimeOffset periodEndUtc, CancellationToken cancellationToken = default) => TokenUsage.GetTokenCountAsync(userId, periodStartUtc, periodEndUtc, cancellationToken);

    /// <inheritdoc/>
    public Task<bool> TryAcquireChatAsync(ChatUsageLease lease, DateTimeOffset now, CancellationToken cancellationToken = default) => TokenUsage.TryAcquireChatAsync(lease, now, cancellationToken);

    /// <inheritdoc/>
    public Task<bool> RecordTokensAsync(ChatUsageLease lease, long cumulativeTokens, CancellationToken cancellationToken = default) => TokenUsage.RecordTokensAsync(lease, cumulativeTokens, cancellationToken);

    /// <inheritdoc/>
    public Task ReleaseChatAsync(ChatUsageLease lease, CancellationToken cancellationToken = default) => TokenUsage.ReleaseChatAsync(lease, cancellationToken);
}
