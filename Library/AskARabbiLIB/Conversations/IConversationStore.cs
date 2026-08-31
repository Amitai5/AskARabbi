namespace AskARabbiLIB.Conversations;

/// <summary>Persists saved conversations while enforcing owner-scoped operations.</summary>
public interface IConversationStore
{
    /// <summary>Lists recent conversation summaries owned by a user.</summary>
    /// <param name="userId">Owning user ID.</param>
    /// <param name="limit">Maximum summaries to return.</param>
    /// <param name="cancellationToken">Token that can cancel the operation.</param>
    /// <returns>Recent summaries ordered by most recently updated.</returns>
    Task<IReadOnlyList<ConversationSummary>> ListAsync(Guid userId, int limit, CancellationToken cancellationToken = default);

    /// <summary>Gets one conversation only when it belongs to the specified user.</summary>
    /// <param name="userId">Owning user ID.</param>
    /// <param name="conversationId">Conversation ID.</param>
    /// <param name="cancellationToken">Token that can cancel the operation.</param>
    /// <returns>The conversation when found and owned by the user; otherwise, <see langword="null"/>.</returns>
    Task<Conversation?> GetAsync(Guid userId, Guid conversationId, CancellationToken cancellationToken = default);

    /// <summary>Creates a conversation.</summary>
    /// <param name="conversation">Conversation to persist.</param>
    /// <param name="cancellationToken">Token that can cancel the operation.</param>
    /// <returns>A task representing the operation.</returns>
    Task CreateAsync(Conversation conversation, CancellationToken cancellationToken = default);

    /// <summary>Appends a message idempotently and returns the canonical conversation context.</summary>
    /// <param name="userId">Owning user ID.</param>
    /// <param name="conversationId">Conversation ID.</param>
    /// <param name="message">Message to append.</param>
    /// <param name="updatedAtUtc">UTC update time.</param>
    /// <param name="cancellationToken">Token that can cancel the operation.</param>
    /// <returns>The updated conversation when found; otherwise, <see langword="null"/>.</returns>
    Task<Conversation?> AppendMessageAsync(Guid userId, Guid conversationId, ConversationMessage message, DateTimeOffset updatedAtUtc, CancellationToken cancellationToken = default);

    /// <summary>Appends a message using already loaded canonical context so optimized stores can avoid rereading the conversation.</summary>
    /// <param name="conversation">Already loaded user-owned canonical conversation.</param>
    /// <param name="message">Message to append.</param>
    /// <param name="updatedAtUtc">UTC update time.</param>
    /// <param name="cancellationToken">Token that can cancel the operation.</param>
    /// <returns>The updated conversation when it still exists; otherwise, <see langword="null"/>.</returns>
    Task<Conversation?> AppendMessageAsync(Conversation conversation, ConversationMessage message, DateTimeOffset updatedAtUtc, CancellationToken cancellationToken = default) => AppendMessageAsync(conversation.UserId, conversation.Id, message, updatedAtUtc, cancellationToken);

    /// <summary>Renames a user-owned conversation.</summary>
    /// <param name="userId">Owning user ID.</param>
    /// <param name="conversationId">Conversation ID.</param>
    /// <param name="title">Normalized title.</param>
    /// <param name="updatedAtUtc">UTC update time.</param>
    /// <param name="cancellationToken">Token that can cancel the operation.</param>
    /// <returns><see langword="true"/> when a matching conversation was updated.</returns>
    Task<bool> RenameAsync(Guid userId, Guid conversationId, string title, DateTimeOffset updatedAtUtc, CancellationToken cancellationToken = default);

    /// <summary>Updates source selectors for a user-owned conversation.</summary>
    /// <param name="userId">Owning user ID.</param>
    /// <param name="conversationId">Conversation ID.</param>
    /// <param name="sourceKeys">Validated source selectors.</param>
    /// <param name="updatedAtUtc">UTC update time.</param>
    /// <param name="cancellationToken">Token that can cancel the operation.</param>
    /// <returns><see langword="true"/> when a matching conversation was updated.</returns>
    Task<bool> UpdateSourcesAsync(Guid userId, Guid conversationId, IReadOnlyList<string> sourceKeys, DateTimeOffset updatedAtUtc, CancellationToken cancellationToken = default);

    /// <summary>Deletes a user-owned conversation.</summary>
    /// <param name="userId">Owning user ID.</param>
    /// <param name="conversationId">Conversation ID.</param>
    /// <param name="cancellationToken">Token that can cancel the operation.</param>
    /// <returns><see langword="true"/> when a matching conversation was deleted.</returns>
    Task<bool> DeleteAsync(Guid userId, Guid conversationId, CancellationToken cancellationToken = default);
}
