namespace AskARabbiLIB.Conversations;

/// <summary>Applies conversation validation and coordinates canonical saved context.</summary>
public sealed class ConversationService
{
    private const int MaximumUserMessageLength = 8_000;
    private const int MaximumAssistantMessageLength = 32_000;
    private const int MaximumTitleLength = 80;
    private readonly IConversationStore store;
    private readonly TimeProvider timeProvider;

    /// <summary>Initializes a conversation service.</summary>
    /// <param name="store">Conversation persistence boundary.</param>
    /// <param name="timeProvider">Optional source of UTC time.</param>
    public ConversationService(IConversationStore store, TimeProvider? timeProvider = null)
    {
        this.store = store ?? throw new ArgumentNullException(nameof(store));
        this.timeProvider = timeProvider ?? TimeProvider.System;
    }

    /// <summary>Lists recent conversations for one user.</summary>
    /// <param name="userId">Owning user ID.</param>
    /// <param name="limit">Maximum summaries to return.</param>
    /// <param name="cancellationToken">Token that can cancel the operation.</param>
    /// <returns>Recent conversation summaries.</returns>
    public Task<IReadOnlyList<ConversationSummary>> ListAsync(Guid userId, int limit = 50, CancellationToken cancellationToken = default)
    {
        ValidateUserId(userId);
        if (limit is < 1 or > 100)
        {
            throw new ArgumentOutOfRangeException(nameof(limit), "Limit must be between 1 and 100.");
        }

        return store.ListAsync(userId, limit, cancellationToken);
    }

    /// <summary>Gets the canonical context for one user-owned conversation.</summary>
    /// <param name="userId">Owning user ID.</param>
    /// <param name="conversationId">Conversation ID.</param>
    /// <param name="cancellationToken">Token that can cancel the operation.</param>
    /// <returns>The conversation when found; otherwise, <see langword="null"/>.</returns>
    public Task<Conversation?> GetAsync(Guid userId, Guid conversationId, CancellationToken cancellationToken = default)
    {
        ValidateIds(userId, conversationId);
        return store.GetAsync(userId, conversationId, cancellationToken);
    }

    /// <summary>Creates a new saved conversation.</summary>
    /// <param name="userId">Owning user ID.</param>
    /// <param name="title">Optional initial title.</param>
    /// <param name="sourceKeys">Optional source selection; all approved sources are used when omitted.</param>
    /// <param name="cancellationToken">Token that can cancel the operation.</param>
    /// <returns>The created conversation.</returns>
    public async Task<Conversation> CreateAsync(Guid userId, string? title, IReadOnlyCollection<string>? sourceKeys, CancellationToken cancellationToken = default)
    {
        ValidateUserId(userId);
        var now = timeProvider.GetUtcNow();
        var conversation = new Conversation
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Title = NormalizeTitle(title),
            EnabledSourceKeys = NormalizeSourceKeys(sourceKeys),
            Messages = [],
            CreatedAtUtc = now,
            UpdatedAtUtc = now,
        };

        await store.CreateAsync(conversation, cancellationToken).ConfigureAwait(false);
        return conversation;
    }

    /// <summary>Appends one user message idempotently and returns the canonical context.</summary>
    /// <param name="userId">Owning user ID.</param>
    /// <param name="conversationId">Conversation ID.</param>
    /// <param name="messageId">Client-generated idempotency ID.</param>
    /// <param name="content">User message content.</param>
    /// <param name="cancellationToken">Token that can cancel the operation.</param>
    /// <returns>The updated conversation when found; otherwise, <see langword="null"/>.</returns>
    public Task<Conversation?> AppendUserMessageAsync(Guid userId, Guid conversationId, Guid messageId, string content, CancellationToken cancellationToken = default)
    {
        return AppendMessageAsync(userId, conversationId, messageId, content, ConversationMessageRole.User, MaximumUserMessageLength, cancellationToken);
    }

    /// <summary>Appends one validated assistant message idempotently and returns the canonical context.</summary>
    /// <param name="userId">Owning user ID.</param>
    /// <param name="conversationId">Conversation ID.</param>
    /// <param name="messageId">Server-generated idempotency ID.</param>
    /// <param name="content">Validated grounded answer text.</param>
    /// <param name="cancellationToken">Token that can cancel the operation.</param>
    /// <returns>The updated conversation when found; otherwise, <see langword="null"/>.</returns>
    public Task<Conversation?> AppendAssistantMessageAsync(Guid userId, Guid conversationId, Guid messageId, string content, CancellationToken cancellationToken = default)
    {
        return AppendMessageAsync(userId, conversationId, messageId, content, ConversationMessageRole.Assistant, MaximumAssistantMessageLength, cancellationToken);
    }

    /// <summary>Renames one user-owned conversation.</summary>
    /// <param name="userId">Owning user ID.</param>
    /// <param name="conversationId">Conversation ID.</param>
    /// <param name="title">New title.</param>
    /// <param name="cancellationToken">Token that can cancel the operation.</param>
    /// <returns><see langword="true"/> when the conversation was updated.</returns>
    public Task<bool> RenameAsync(Guid userId, Guid conversationId, string title, CancellationToken cancellationToken = default)
    {
        ValidateIds(userId, conversationId);
        return store.RenameAsync(userId, conversationId, NormalizeTitle(title, false), timeProvider.GetUtcNow(), cancellationToken);
    }

    /// <summary>Updates source selectors for one user-owned conversation.</summary>
    /// <param name="userId">Owning user ID.</param>
    /// <param name="conversationId">Conversation ID.</param>
    /// <param name="sourceKeys">New source selection.</param>
    /// <param name="cancellationToken">Token that can cancel the operation.</param>
    /// <returns><see langword="true"/> when the conversation was updated.</returns>
    public Task<bool> UpdateSourcesAsync(Guid userId, Guid conversationId, IReadOnlyCollection<string> sourceKeys, CancellationToken cancellationToken = default)
    {
        ValidateIds(userId, conversationId);
        return store.UpdateSourcesAsync(userId, conversationId, NormalizeSourceKeys(sourceKeys), timeProvider.GetUtcNow(), cancellationToken);
    }

    /// <summary>Deletes one user-owned conversation.</summary>
    /// <param name="userId">Owning user ID.</param>
    /// <param name="conversationId">Conversation ID.</param>
    /// <param name="cancellationToken">Token that can cancel the operation.</param>
    /// <returns><see langword="true"/> when the conversation was deleted.</returns>
    public Task<bool> DeleteAsync(Guid userId, Guid conversationId, CancellationToken cancellationToken = default)
    {
        ValidateIds(userId, conversationId);
        return store.DeleteAsync(userId, conversationId, cancellationToken);
    }

    private static string NormalizeTitle(string? title, bool allowDefault = true)
    {
        var normalized = title?.Trim() ?? string.Empty;
        if (normalized.Length == 0 && allowDefault)
        {
            return "New conversation";
        }
        if (normalized.Length is < 1 or > MaximumTitleLength)
        {
            throw new ArgumentException($"Title must be between 1 and {MaximumTitleLength} characters.", nameof(title));
        }

        return normalized;
    }

    private Task<Conversation?> AppendMessageAsync(Guid userId, Guid conversationId, Guid messageId, string content, ConversationMessageRole role, int maximumLength, CancellationToken cancellationToken)
    {
        ValidateIds(userId, conversationId);
        if (messageId == Guid.Empty)
        {
            throw new ArgumentException("Message ID is required.", nameof(messageId));
        }
        var normalizedContent = content?.Trim() ?? string.Empty;
        if (normalizedContent.Length is < 1 || normalizedContent.Length > maximumLength)
        {
            throw new ArgumentException($"Message content must be between 1 and {maximumLength:N0} characters.", nameof(content));
        }
        var now = timeProvider.GetUtcNow();
        var message = new ConversationMessage
        {
            Id = messageId,
            Role = role,
            Content = normalizedContent,
            CreatedAtUtc = now,
        };
        return store.AppendMessageAsync(userId, conversationId, message, now, cancellationToken);
    }

    private static IReadOnlyList<string> NormalizeSourceKeys(IReadOnlyCollection<string>? sourceKeys)
    {
        var normalized = sourceKeys is null
            ? ConversationSourceCatalog.All.ToArray()
            : sourceKeys.Where(value => !string.IsNullOrWhiteSpace(value)).Select(value => value.Trim()).Distinct(StringComparer.Ordinal).ToArray();

        if (normalized.Length == 0)
        {
            throw new ArgumentException("At least one source must be enabled.", nameof(sourceKeys));
        }

        var unsupported = normalized.FirstOrDefault(sourceKey => !ConversationSourceCatalog.Contains(sourceKey));
        if (unsupported is not null)
        {
            throw new ArgumentException($"Unsupported source selector: {unsupported}.", nameof(sourceKeys));
        }

        return normalized;
    }

    private static void ValidateIds(Guid userId, Guid conversationId)
    {
        ValidateUserId(userId);
        if (conversationId == Guid.Empty)
        {
            throw new ArgumentException("Conversation ID is required.", nameof(conversationId));
        }
    }

    private static void ValidateUserId(Guid userId)
    {
        if (userId == Guid.Empty)
        {
            throw new ArgumentException("User ID is required.", nameof(userId));
        }
    }
}
