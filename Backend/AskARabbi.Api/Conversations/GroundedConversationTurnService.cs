using System.Security.Cryptography;
using System.Text;
using AskARabbiLIB.Conversations;
using AskARabbiLIB.ConversationSettings;
using AskARabbiLIB.Grounding;
using AskARabbiLIB.Profiles;
using AskARabbiLIB.Usage;
using Azure;
using Azure.Identity;

namespace AskARabbi.Api.Conversations;

/// <summary>Stores a user turn, creates one fail-closed grounded answer, and persists only validated output.</summary>
public sealed class GroundedConversationTurnService
{
    private readonly ConversationService conversations;
    private readonly ConversationSettingsService settings;
    private readonly MonthlyUsageService usage;
    private readonly IGroundedAnswerService groundedAnswers;
    private readonly GroundedAnswerTextRenderer renderer;
    private readonly ILogger<GroundedConversationTurnService> logger;

    /// <summary>Initializes the production conversation-turn orchestrator.</summary>
    /// <param name="conversations">Canonical conversation service.</param>
    /// <param name="settings">Account personalization service.</param>
    /// <param name="usage">Calendar-month answer usage service.</param>
    /// <param name="groundedAnswers">Fail-closed grounded-answer service.</param>
    /// <param name="renderer">Validated answer renderer.</param>
    /// <param name="logger">Structured boundary logger.</param>
    public GroundedConversationTurnService(ConversationService conversations, ConversationSettingsService settings, MonthlyUsageService usage, IGroundedAnswerService groundedAnswers, GroundedAnswerTextRenderer renderer, ILogger<GroundedConversationTurnService> logger)
    {
        this.conversations = conversations ?? throw new ArgumentNullException(nameof(conversations));
        this.settings = settings ?? throw new ArgumentNullException(nameof(settings));
        this.usage = usage ?? throw new ArgumentNullException(nameof(usage));
        this.groundedAnswers = groundedAnswers ?? throw new ArgumentNullException(nameof(groundedAnswers));
        this.renderer = renderer ?? throw new ArgumentNullException(nameof(renderer));
        this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>Processes one idempotent user message and returns canonical persisted context.</summary>
    /// <param name="userId">Authenticated account ID.</param>
    /// <param name="conversationId">Owned conversation ID.</param>
    /// <param name="userMessageId">Client-generated user-message ID.</param>
    /// <param name="content">Question text.</param>
    /// <param name="cancellationToken">Token that can cancel the operation.</param>
    /// <returns>A stored, answered, limited, or fail-closed turn result.</returns>
    public async Task<GroundedConversationTurnResult> ProcessAsync(Guid userId, Guid conversationId, Guid userMessageId, string content, CancellationToken cancellationToken = default)
    {
        var conversation = await conversations.AppendUserMessageAsync(userId, conversationId, userMessageId, content, cancellationToken).ConfigureAwait(false);
        if (conversation is null)
        {
            return new GroundedConversationTurnResult("not_found", null, null);
        }
        var storedQuestion = conversation.Messages.SingleOrDefault(message => message.Id == userMessageId);
        if (storedQuestion is null || storedQuestion.Role != ConversationMessageRole.User || !string.Equals(storedQuestion.Content, content.Trim(), StringComparison.Ordinal))
        {
            throw new InvalidOperationException("The message ID was already used for different conversation content.");
        }

        var assistantMessageId = CreateAssistantMessageId(userMessageId);
        if (conversation.Messages.Any(message => message.Id == assistantMessageId && message.Role == ConversationMessageRole.Assistant))
        {
            return new GroundedConversationTurnResult("answered", conversation, null);
        }
        var currentUsage = await usage.GetCurrentAsync(userId, cancellationToken).ConfigureAwait(false);
        if (currentUsage.AnswersRemaining <= 0)
        {
            return new GroundedConversationTurnResult("usage_limit_reached", conversation, "You have reached the answer limit for this billing period. Your question was saved, but the model was not called.");
        }

        GroundedAnswerResult answerResult;
        try
        {
            var personalization = await settings.GetPersonalizationAsync(userId, cancellationToken).ConfigureAwait(false);
            var question = CreateQuestion(storedQuestion.Content, conversation.EnabledSourceKeys, personalization);
            var recentTurns = CreateRecentTurns(conversation.Messages, userMessageId);
            answerResult = await groundedAnswers.AnswerAsync(question, recentTurns, cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception exception) when (exception is HttpRequestException or AuthenticationFailedException or RequestFailedException or InvalidDataException)
        {
            logger.LogError(exception, "Grounded retrieval failed for conversation {ConversationId} and user {UserId}.", conversationId, userId);
            return new GroundedConversationTurnResult("retrieval_unavailable", conversation, "The approved source library is temporarily unavailable, so AskRabbi did not generate an unsupported answer. Please try again shortly.");
        }

        if (!answerResult.IsSuccess || answerResult.Answer is null)
        {
            return new GroundedConversationTurnResult(ToStatus(answerResult.Status), conversation, answerResult.ErrorMessage);
        }
        var rendered = renderer.Render(answerResult.Answer);
        var updated = await conversations.AppendAssistantMessageAsync(userId, conversationId, assistantMessageId, rendered, cancellationToken).ConfigureAwait(false) ?? throw new InvalidOperationException("Conversation disappeared before its validated answer could be saved.");
        await usage.RecordAnswerAsync(userId, cancellationToken).ConfigureAwait(false);
        return new GroundedConversationTurnResult("answered", updated, null);
    }

    internal static Guid CreateAssistantMessageId(Guid userMessageId)
    {
        if (userMessageId == Guid.Empty)
        {
            throw new ArgumentException("User message ID is required.", nameof(userMessageId));
        }
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes($"AskARabbi:assistant:{userMessageId:D}"));
        return Guid.ParseExact(Convert.ToHexString(hash.AsSpan(0, 16)), "N");
    }

    private static GroundedQuestion CreateQuestion(string content, IReadOnlyList<string> sourceKeys, PersonalizationSettings? personalization)
    {
        return new GroundedQuestion
        {
            Question = content,
            Languages = [],
            SourceKeys = sourceKeys,
            ConversationLanguage = personalization?.ConversationLanguage,
            QuotationLanguage = personalization?.QuotationLanguage,
            UserProfile = personalization is null ? null : new UserProfile
            {
                Name = personalization.FullName,
                DateOfBirth = personalization.BirthDate,
                Bio = personalization.AdditionalContext,
                ReligiousBackground = personalization.ReligiousMovement,
                JewishHeritage = personalization.JewishHeritage,
            },
        };
    }

    private static IReadOnlyList<GroundedConversationTurn> CreateRecentTurns(IReadOnlyList<ConversationMessage> messages, Guid currentMessageId)
    {
        var turns = new List<GroundedConversationTurn>();
        ConversationMessage? pendingUser = null;
        foreach (var message in messages)
        {
            if (message.Id == currentMessageId)
            {
                break;
            }
            if (message.Role == ConversationMessageRole.User)
            {
                pendingUser = message;
            }
            else if (message.Role == ConversationMessageRole.Assistant && pendingUser is not null)
            {
                turns.Add(new GroundedConversationTurn(pendingUser.Content, message.Content));
                pendingUser = null;
            }
        }
        return turns;
    }

    private static string ToStatus(GroundedAnswerStatus status) => status switch
    {
        GroundedAnswerStatus.InsufficientEvidence => "insufficient_evidence",
        GroundedAnswerStatus.AuthenticationFailed => "ai_authentication_failed",
        GroundedAnswerStatus.ValidationFailed => "validation_failed",
        GroundedAnswerStatus.AIUnavailable => "ai_unavailable",
        GroundedAnswerStatus.Success => throw new InvalidOperationException("A successful grounded answer must contain materialized answer content."),
        _ => throw new ArgumentOutOfRangeException(nameof(status), status, "Unknown grounded-answer status."),
    };
}
