using AskARabbiLIB.Accounts;
using AskARabbiLIB.Conversations;
using AskARabbiLIB.ConversationSettings;
using AskARabbiLIB.Usage;

namespace AskARabbi.Api.Development;

/// <summary>Stores local development data in process memory without replacing production persistence.</summary>
public sealed class LocalDevelopmentApplicationStore : IUserAccountStore, IConversationStore, IConversationSettingsStore, IUsageStore
{
    private static readonly Guid StableUserId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private readonly object synchronization = new();
    private readonly Dictionary<Guid, Conversation> conversations = [];
    private readonly Dictionary<Guid, PersonalizationSettings> personalization = [];
    private readonly Dictionary<Guid, ConversationPreferences> preferences = [];
    private readonly Dictionary<(Guid UserId, DateTimeOffset PeriodStartUtc), int> answerCounts = [];
    private UserAccount? account;

    /// <inheritdoc/>
    public Task<UserAccount> UpsertAsync(ExternalUserIdentity identity, DateTimeOffset updatedAtUtc, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(identity);
        cancellationToken.ThrowIfCancellationRequested();

        lock (synchronization)
        {
            account = new UserAccount
            {
                Id = StableUserId,
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
    }

    /// <inheritdoc/>
    public Task<UserAccount?> GetByIdAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        lock (synchronization)
        {
            return Task.FromResult(account?.Id == userId ? account : null);
        }
    }

    /// <inheritdoc/>
    public Task<IReadOnlyList<ConversationSummary>> ListAsync(Guid userId, int limit, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        lock (synchronization)
        {
            var values = conversations.Values
                .Where(conversation => conversation.UserId == userId)
                .OrderByDescending(conversation => conversation.UpdatedAtUtc)
                .Take(limit)
                .Select(conversation => new ConversationSummary(conversation.Id, conversation.Title, conversation.EnabledSourceKeys, conversation.UpdatedAtUtc))
                .ToArray();
            return Task.FromResult<IReadOnlyList<ConversationSummary>>(values);
        }
    }

    /// <inheritdoc/>
    public Task<Conversation?> GetAsync(Guid userId, Guid conversationId, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        lock (synchronization)
        {
            conversations.TryGetValue(conversationId, out var conversation);
            return Task.FromResult(conversation?.UserId == userId ? conversation : null);
        }
    }

    /// <inheritdoc/>
    public Task CreateAsync(Conversation conversation, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(conversation);
        cancellationToken.ThrowIfCancellationRequested();
        lock (synchronization)
        {
            conversations.Add(conversation.Id, conversation);
        }
        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public Task<Conversation?> AppendMessageAsync(Guid userId, Guid conversationId, ConversationMessage message, DateTimeOffset updatedAtUtc, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(message);
        cancellationToken.ThrowIfCancellationRequested();
        lock (synchronization)
        {
            if (!conversations.TryGetValue(conversationId, out var conversation) || conversation.UserId != userId)
            {
                return Task.FromResult<Conversation?>(null);
            }

            if (conversation.Messages.All(existing => existing.Id != message.Id))
            {
                conversation = conversation with { Messages = [.. conversation.Messages, message], UpdatedAtUtc = updatedAtUtc };
                conversations[conversationId] = conversation;
            }

            return Task.FromResult<Conversation?>(conversation);
        }
    }

    /// <inheritdoc/>
    public Task<bool> RenameAsync(Guid userId, Guid conversationId, string title, DateTimeOffset updatedAtUtc, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        lock (synchronization)
        {
            if (!conversations.TryGetValue(conversationId, out var conversation) || conversation.UserId != userId)
            {
                return Task.FromResult(false);
            }

            conversations[conversationId] = conversation with { Title = title, UpdatedAtUtc = updatedAtUtc };
            return Task.FromResult(true);
        }
    }

    /// <inheritdoc/>
    public Task<bool> UpdateSourcesAsync(Guid userId, Guid conversationId, IReadOnlyList<string> sourceKeys, DateTimeOffset updatedAtUtc, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        lock (synchronization)
        {
            if (!conversations.TryGetValue(conversationId, out var conversation) || conversation.UserId != userId)
            {
                return Task.FromResult(false);
            }

            conversations[conversationId] = conversation with { EnabledSourceKeys = sourceKeys.ToArray(), UpdatedAtUtc = updatedAtUtc };
            return Task.FromResult(true);
        }
    }

    /// <inheritdoc/>
    public Task<bool> DeleteAsync(Guid userId, Guid conversationId, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        lock (synchronization)
        {
            if (!conversations.TryGetValue(conversationId, out var conversation) || conversation.UserId != userId)
            {
                return Task.FromResult(false);
            }

            return Task.FromResult(conversations.Remove(conversationId));
        }
    }

    /// <inheritdoc/>
    public Task<PersonalizationSettings?> GetPersonalizationAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        lock (synchronization)
        {
            personalization.TryGetValue(userId, out var value);
            return Task.FromResult(value);
        }
    }

    /// <inheritdoc/>
    public Task UpsertPersonalizationAsync(Guid userId, PersonalizationSettings value, DateTimeOffset updatedAtUtc, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(value);
        cancellationToken.ThrowIfCancellationRequested();
        lock (synchronization)
        {
            personalization[userId] = value;
        }
        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public Task<ConversationPreferences?> GetPreferencesAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        lock (synchronization)
        {
            preferences.TryGetValue(userId, out var value);
            return Task.FromResult(value);
        }
    }

    /// <inheritdoc/>
    public Task UpsertPreferencesAsync(Guid userId, ConversationPreferences value, DateTimeOffset updatedAtUtc, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(value);
        cancellationToken.ThrowIfCancellationRequested();
        lock (synchronization)
        {
            preferences[userId] = value;
        }
        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public Task<int> GetAnswerCountAsync(Guid userId, DateTimeOffset periodStartUtc, DateTimeOffset periodEndUtc, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        lock (synchronization)
        {
            answerCounts.TryGetValue((userId, periodStartUtc), out var value);
            return Task.FromResult(value);
        }
    }

    /// <inheritdoc/>
    public Task<int> IncrementAnswerCountAsync(Guid userId, DateTimeOffset periodStartUtc, DateTimeOffset periodEndUtc, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        lock (synchronization)
        {
            var key = (userId, periodStartUtc);
            answerCounts.TryGetValue(key, out var value);
            value++;
            answerCounts[key] = value;
            return Task.FromResult(value);
        }
    }
}
