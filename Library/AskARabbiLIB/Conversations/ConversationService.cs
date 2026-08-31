namespace AskARabbiLIB.Conversations;

/// <summary>Applies conversation validation and coordinates canonical saved context.</summary>
public sealed class ConversationService
{
    private const int MaximumUserMessageLength = 8_000;
    private const int MaximumAssistantMessageLength = 32_000;
    private const int MaximumTitleLength = 80;
    private const int MaximumSourcesPerMessage = 50;
    private const int MaximumQuotationLength = 200_000;
    private const int MaximumSourceContextLength = 200_000;
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
    public async Task<IReadOnlyList<ConversationSummary>> ListAsync(Guid userId, int limit = 50, CancellationToken cancellationToken = default)
    {
        ValidateUserId(userId);
        if (limit is < 1 or > 100)
        {
            throw new ArgumentOutOfRangeException(nameof(limit), "Limit must be between 1 and 100.");
        }

        var summaries = await store.ListAsync(userId, limit, cancellationToken).ConfigureAwait(false);
        return summaries.Select(NormalizeSummary).ToArray();
    }

    /// <summary>Gets the canonical context for one user-owned conversation.</summary>
    /// <param name="userId">Owning user ID.</param>
    /// <param name="conversationId">Conversation ID.</param>
    /// <param name="cancellationToken">Token that can cancel the operation.</param>
    /// <returns>The conversation when found; otherwise, <see langword="null"/>.</returns>
    public async Task<Conversation?> GetAsync(Guid userId, Guid conversationId, CancellationToken cancellationToken = default)
    {
        ValidateIds(userId, conversationId);
        var conversation = await store.GetAsync(userId, conversationId, cancellationToken).ConfigureAwait(false);
        return NormalizeConversation(conversation);
    }

    /// <summary>Creates a new saved conversation.</summary>
    /// <param name="userId">Owning user ID.</param>
    /// <param name="title">Optional initial title.</param>
    /// <param name="sourceKeys">Optional source selection; core collections are used when omitted.</param>
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

    /// <summary>Creates a saved conversation containing its first user message.</summary>
    /// <param name="userId">Owning user ID.</param>
    /// <param name="messageId">Client-generated idempotency ID for the first message.</param>
    /// <param name="content">First user message content.</param>
    /// <param name="sourceKeys">Optional source selection; core collections are used when omitted.</param>
    /// <param name="cancellationToken">Token that can cancel the operation.</param>
    /// <returns>The created conversation containing the normalized first message.</returns>
    public async Task<Conversation> CreateWithUserMessageAsync(Guid userId, Guid messageId, string content, IReadOnlyCollection<string>? sourceKeys, CancellationToken cancellationToken = default)
    {
        ValidateUserId(userId);
        var now = timeProvider.GetUtcNow();
        var message = CreateMessage(messageId, content, ConversationMessageRole.User, MaximumUserMessageLength, now, []);
        var conversation = new Conversation
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Title = NormalizeTitle(null),
            EnabledSourceKeys = NormalizeSourceKeys(sourceKeys),
            Messages = [message],
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
        return AppendMessageAsync(userId, conversationId, messageId, content, ConversationMessageRole.User, MaximumUserMessageLength, [], cancellationToken);
    }

    /// <summary>Appends one user message to already loaded canonical context without requiring a second context read.</summary>
    /// <param name="conversation">Already loaded canonical conversation.</param>
    /// <param name="messageId">Client-generated idempotency ID.</param>
    /// <param name="content">User message content.</param>
    /// <param name="cancellationToken">Token that can cancel the operation.</param>
    /// <returns>The updated conversation when it still exists; otherwise, <see langword="null"/>.</returns>
    public Task<Conversation?> AppendUserMessageAsync(Conversation conversation, Guid messageId, string content, CancellationToken cancellationToken = default)
    {
        return AppendMessageAsync(conversation, messageId, content, ConversationMessageRole.User, MaximumUserMessageLength, [], cancellationToken);
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
        return AppendMessageAsync(userId, conversationId, messageId, content, ConversationMessageRole.Assistant, MaximumAssistantMessageLength, [], cancellationToken);
    }

    /// <summary>Appends one validated assistant message and its trusted sources idempotently.</summary>
    /// <param name="userId">Owning user ID.</param>
    /// <param name="conversationId">Conversation ID.</param>
    /// <param name="messageId">Server-generated idempotency ID.</param>
    /// <param name="content">Validated grounded answer text.</param>
    /// <param name="sources">Trusted quotations, context, and provenance materialized from validated evidence.</param>
    /// <param name="cancellationToken">Token that can cancel the operation.</param>
    /// <returns>The updated conversation when found; otherwise, <see langword="null"/>.</returns>
    public Task<Conversation?> AppendAssistantMessageAsync(Guid userId, Guid conversationId, Guid messageId, string content, IReadOnlyCollection<ConversationSourceCitation> sources, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(sources);
        return AppendMessageAsync(userId, conversationId, messageId, content, ConversationMessageRole.Assistant, MaximumAssistantMessageLength, sources, cancellationToken);
    }

    /// <summary>Appends one validated assistant message to already loaded canonical context without requiring a second context read.</summary>
    /// <param name="conversation">Already loaded canonical conversation.</param>
    /// <param name="messageId">Server-generated idempotency ID.</param>
    /// <param name="content">Validated grounded answer text.</param>
    /// <param name="sources">Trusted quotations, context, and provenance materialized from validated evidence.</param>
    /// <param name="cancellationToken">Token that can cancel the operation.</param>
    /// <returns>The updated conversation when it still exists; otherwise, <see langword="null"/>.</returns>
    public Task<Conversation?> AppendAssistantMessageAsync(Conversation conversation, Guid messageId, string content, IReadOnlyCollection<ConversationSourceCitation> sources, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(sources);
        return AppendMessageAsync(conversation, messageId, content, ConversationMessageRole.Assistant, MaximumAssistantMessageLength, sources, cancellationToken);
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
        if (allowDefault && (normalized.Length == 0 || string.Equals(normalized, Conversation.DefaultTitle, StringComparison.OrdinalIgnoreCase)))
        {
            return Conversation.DefaultTitle;
        }
        if (normalized.Length is < 1 or > MaximumTitleLength)
        {
            throw new ArgumentException($"Title must be between 1 and {MaximumTitleLength} characters.", nameof(title));
        }

        return normalized;
    }

    private static ConversationSummary NormalizeSummary(ConversationSummary summary)
    {
        var title = NormalizeTitle(summary.Title);
        return string.Equals(title, summary.Title, StringComparison.Ordinal) ? summary : summary with { Title = title };
    }

    private static Conversation? NormalizeConversation(Conversation? conversation)
    {
        if (conversation is null)
        {
            return null;
        }

        var title = NormalizeTitle(conversation.Title);
        return string.Equals(title, conversation.Title, StringComparison.Ordinal) ? conversation : conversation with { Title = title };
    }

    private static async Task<Conversation?> NormalizeConversationAsync(Task<Conversation?> conversationTask)
    {
        var conversation = await conversationTask.ConfigureAwait(false);
        return NormalizeConversation(conversation);
    }

    private Task<Conversation?> AppendMessageAsync(Guid userId, Guid conversationId, Guid messageId, string content, ConversationMessageRole role, int maximumLength, IReadOnlyCollection<ConversationSourceCitation> sources, CancellationToken cancellationToken)
    {
        ValidateIds(userId, conversationId);
        var now = timeProvider.GetUtcNow();
        var message = CreateMessage(messageId, content, role, maximumLength, now, sources);
        return NormalizeConversationAsync(store.AppendMessageAsync(userId, conversationId, message, now, cancellationToken));
    }

    private Task<Conversation?> AppendMessageAsync(Conversation conversation, Guid messageId, string content, ConversationMessageRole role, int maximumLength, IReadOnlyCollection<ConversationSourceCitation> sources, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(conversation);
        ValidateIds(conversation.UserId, conversation.Id);
        var now = timeProvider.GetUtcNow();
        var message = CreateMessage(messageId, content, role, maximumLength, now, sources);
        return NormalizeConversationAsync(store.AppendMessageAsync(conversation, message, now, cancellationToken));
    }

    private static ConversationMessage CreateMessage(Guid messageId, string content, ConversationMessageRole role, int maximumLength, DateTimeOffset createdAtUtc, IReadOnlyCollection<ConversationSourceCitation> sources)
    {
        if (messageId == Guid.Empty)
        {
            throw new ArgumentException("Message ID is required.", nameof(messageId));
        }
        var normalizedContent = content?.Trim() ?? string.Empty;
        if (normalizedContent.Length is < 1 || normalizedContent.Length > maximumLength)
        {
            throw new ArgumentException($"Message content must be between 1 and {maximumLength:N0} characters.", nameof(content));
        }

        return new ConversationMessage
        {
            Id = messageId,
            Role = role,
            Content = normalizedContent,
            Sources = NormalizeSources(sources, role),
            CreatedAtUtc = createdAtUtc,
        };
    }

    private static IReadOnlyList<ConversationSourceCitation> NormalizeSources(IReadOnlyCollection<ConversationSourceCitation> sources, ConversationMessageRole role)
    {
        if (sources.Count == 0)
        {
            return [];
        }
        if (role != ConversationMessageRole.Assistant)
        {
            throw new ArgumentException("Only assistant messages can contain grounded sources.", nameof(sources));
        }
        if (sources.Count > MaximumSourcesPerMessage)
        {
            throw new ArgumentException($"An assistant message can contain at most {MaximumSourcesPerMessage} sources.", nameof(sources));
        }

        var numbers = new HashSet<int>();
        var normalized = new List<ConversationSourceCitation>(sources.Count);
        foreach (var source in sources)
        {
            ArgumentNullException.ThrowIfNull(source);
            if (source.Number < 1 || !numbers.Add(source.Number))
            {
                throw new ArgumentException("Source citation numbers must be unique positive integers.", nameof(sources));
            }
            ValidateRequiredSourceText(source.Title, nameof(source.Title));
            ValidateRequiredSourceText(source.CanonicalReference, nameof(source.CanonicalReference));
            ValidateRequiredSourceText(source.Edition, nameof(source.Edition));
            ValidateRequiredSourceText(source.Language, nameof(source.Language));
            ValidateRequiredSourceText(source.Collection, nameof(source.Collection));
            ValidateRequiredSourceText(source.License, nameof(source.License));
            ValidateHttpUrl(source.SourceUrl, nameof(source.SourceUrl));
            ValidateHttpUrl(source.AttributionUrl, nameof(source.AttributionUrl));
            if (string.IsNullOrWhiteSpace(source.Context) || source.Context.Length > MaximumSourceContextLength)
            {
                throw new ArgumentException($"Source context must be between 1 and {MaximumSourceContextLength:N0} characters.", nameof(sources));
            }
            if (source.Quotations is null || source.Quotations.Any(quotation => string.IsNullOrWhiteSpace(quotation) || quotation.Length > MaximumQuotationLength))
            {
                throw new ArgumentException($"Source quotations must be between 1 and {MaximumQuotationLength:N0} characters.", nameof(sources));
            }

            normalized.Add(source with
            {
                Title = source.Title.Trim(),
                HebrewTitle = source.HebrewTitle.Trim(),
                CanonicalReference = source.CanonicalReference.Trim(),
                Edition = source.Edition.Trim(),
                Language = source.Language.Trim(),
                Collection = source.Collection.Trim(),
                License = source.License.Trim(),
                Quotations = source.Quotations.ToArray(),
                Context = source.Context.Trim(),
            });
        }
        return normalized;
    }

    private static void ValidateRequiredSourceText(string value, string name)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException($"{name} is required.", nameof(value));
        }
    }

    private static void ValidateHttpUrl(string value, string name)
    {
        if (!Uri.TryCreate(value, UriKind.Absolute, out var uri) || uri.Scheme is not ("http" or "https"))
        {
            throw new ArgumentException($"{name} must be an absolute HTTP URL.", nameof(value));
        }
    }

    private static IReadOnlyList<string> NormalizeSourceKeys(IReadOnlyCollection<string>? sourceKeys)
    {
        var normalized = sourceKeys is null
            ? ConversationSourceCatalog.Core.ToArray()
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
