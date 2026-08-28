using AskARabbiLIB.Accounts;
using AskARabbiLIB.Conversations;
using AskARabbiLIB.ConversationSettings;
using AskARabbiLIB.Usage;

namespace AskARabbi.Api.Tests;

internal sealed class InMemoryApplicationStore : IUserAccountStore, IConversationStore, IConversationSettingsStore, IUsageStore
{
    private static readonly Guid StableUserId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private readonly Dictionary<Guid, Conversation> conversations = [];
    private readonly Dictionary<Guid, PersonalizationSettings> personalization = [];
    private readonly Dictionary<Guid, ConversationPreferences> preferences = [];
    private UserAccount? account;
    private int answerCount = 7;

    internal Guid UserId => account?.Id ?? StableUserId;

    public Task<UserAccount> UpsertAsync(ExternalUserIdentity identity, DateTimeOffset updatedAtUtc, CancellationToken cancellationToken = default)
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

    public Task<int> GetAnswerCountAsync(Guid userId, DateTimeOffset periodStartUtc, DateTimeOffset periodEndUtc, CancellationToken cancellationToken = default) => Task.FromResult(answerCount);

    public Task<int> IncrementAnswerCountAsync(Guid userId, DateTimeOffset periodStartUtc, DateTimeOffset periodEndUtc, CancellationToken cancellationToken = default)
    {
        answerCount++;
        return Task.FromResult(answerCount);
    }
}
