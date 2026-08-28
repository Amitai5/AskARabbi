using AskARabbiLIB.Accounts;
using AskARabbiLIB.Conversations;
using AskARabbiLIB.ConversationSettings;
using AskARabbiLIB.Usage;

namespace AskARabbiLIB.Persistence.Mongo;

/// <summary>Fails persistence operations explicitly when MongoDB has not been configured.</summary>
public sealed class UnavailableApplicationStore : IUserAccountStore, IConversationStore, IConversationSettingsStore, IUsageStore
{
    /// <inheritdoc/>
    public Task<UserAccount> UpsertAsync(ExternalUserIdentity identity, DateTimeOffset updatedAtUtc, CancellationToken cancellationToken = default) => Task.FromException<UserAccount>(CreateException());

    /// <inheritdoc/>
    public Task<UserAccount?> GetByIdAsync(Guid userId, CancellationToken cancellationToken = default) => Task.FromException<UserAccount?>(CreateException());

    /// <inheritdoc/>
    public Task<IReadOnlyList<ConversationSummary>> ListAsync(Guid userId, int limit, CancellationToken cancellationToken = default) => Task.FromException<IReadOnlyList<ConversationSummary>>(CreateException());

    /// <inheritdoc/>
    public Task<Conversation?> GetAsync(Guid userId, Guid conversationId, CancellationToken cancellationToken = default) => Task.FromException<Conversation?>(CreateException());

    /// <inheritdoc/>
    public Task CreateAsync(Conversation conversation, CancellationToken cancellationToken = default) => Task.FromException(CreateException());

    /// <inheritdoc/>
    public Task<Conversation?> AppendMessageAsync(Guid userId, Guid conversationId, ConversationMessage message, DateTimeOffset updatedAtUtc, CancellationToken cancellationToken = default) => Task.FromException<Conversation?>(CreateException());

    /// <inheritdoc/>
    public Task<bool> RenameAsync(Guid userId, Guid conversationId, string title, DateTimeOffset updatedAtUtc, CancellationToken cancellationToken = default) => Task.FromException<bool>(CreateException());

    /// <inheritdoc/>
    public Task<bool> UpdateSourcesAsync(Guid userId, Guid conversationId, IReadOnlyList<string> sourceKeys, DateTimeOffset updatedAtUtc, CancellationToken cancellationToken = default) => Task.FromException<bool>(CreateException());

    /// <inheritdoc/>
    public Task<bool> DeleteAsync(Guid userId, Guid conversationId, CancellationToken cancellationToken = default) => Task.FromException<bool>(CreateException());

    /// <inheritdoc/>
    public Task<PersonalizationSettings?> GetPersonalizationAsync(Guid userId, CancellationToken cancellationToken = default) => Task.FromException<PersonalizationSettings?>(CreateException());

    /// <inheritdoc/>
    public Task UpsertPersonalizationAsync(Guid userId, PersonalizationSettings personalization, DateTimeOffset updatedAtUtc, CancellationToken cancellationToken = default) => Task.FromException(CreateException());

    /// <inheritdoc/>
    public Task<ConversationPreferences?> GetPreferencesAsync(Guid userId, CancellationToken cancellationToken = default) => Task.FromException<ConversationPreferences?>(CreateException());

    /// <inheritdoc/>
    public Task UpsertPreferencesAsync(Guid userId, ConversationPreferences preferences, DateTimeOffset updatedAtUtc, CancellationToken cancellationToken = default) => Task.FromException(CreateException());

    /// <inheritdoc/>
    public Task<int> GetAnswerCountAsync(Guid userId, DateTimeOffset periodStartUtc, DateTimeOffset periodEndUtc, CancellationToken cancellationToken = default) => Task.FromException<int>(CreateException());

    /// <inheritdoc/>
    public Task<int> IncrementAnswerCountAsync(Guid userId, DateTimeOffset periodStartUtc, DateTimeOffset periodEndUtc, CancellationToken cancellationToken = default) => Task.FromException<int>(CreateException());

    private static PersistenceUnavailableException CreateException() => new();
}
