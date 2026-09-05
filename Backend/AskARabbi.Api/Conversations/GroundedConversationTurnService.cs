using System.Diagnostics;
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

    /// <summary>Creates a conversation with its first user message and processes its first grounded response.</summary>
    /// <param name="userId">Authenticated account ID.</param>
    /// <param name="userMessageId">Client-generated user-message ID.</param>
    /// <param name="content">Question text.</param>
    /// <param name="sourceKeys">Approved source selectors for the conversation.</param>
    /// <param name="cancellationToken">Token that can cancel the operation.</param>
    /// <returns>A stored, answered, limited, or fail-closed first-turn result.</returns>
    public async Task<GroundedConversationTurnResult> CreateAsync(Guid userId, Guid userMessageId, string content, IReadOnlyCollection<string>? sourceKeys, CancellationToken cancellationToken = default)
    {
        var conversation = await conversations.CreateWithUserMessageAsync(userId, userMessageId, content, sourceKeys, cancellationToken).ConfigureAwait(false);
        return await ProcessStoredMessageAsync(userId, conversation, userMessageId, content, cancellationToken).ConfigureAwait(false);
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
        var existing = await conversations.GetAsync(userId, conversationId, cancellationToken).ConfigureAwait(false);
        if (existing is null)
        {
            return new GroundedConversationTurnResult("not_found", null, null);
        }

        var conversation = await conversations.AppendUserMessageAsync(existing, userMessageId, content, cancellationToken).ConfigureAwait(false);
        if (conversation is null)
        {
            return new GroundedConversationTurnResult("not_found", null, null);
        }

        return await ProcessStoredMessageAsync(userId, conversation, userMessageId, content, cancellationToken).ConfigureAwait(false);
    }

    private async Task<GroundedConversationTurnResult> ProcessStoredMessageAsync(Guid userId, Conversation conversation, Guid userMessageId, string content, CancellationToken cancellationToken)
    {
        var processingStopwatch = Stopwatch.StartNew();
        var storedQuestion = conversation.Messages.SingleOrDefault(message => message.Id == userMessageId);
        if (storedQuestion is null || storedQuestion.Role != ConversationMessageRole.User || !string.Equals(storedQuestion.Content, content.Trim(), StringComparison.Ordinal))
        {
            throw new InvalidOperationException("The message ID was already used for different conversation content.");
        }

        var assistantMessageId = CreateAssistantMessageId(userMessageId);
        if (conversation.Messages.Any(message => message.Id == assistantMessageId && message.Role == ConversationMessageRole.Assistant))
        {
            return new GroundedConversationTurnResult("answered", conversation, null, null, processingStopwatch.Elapsed);
        }
        var shouldGenerateConversationTitle = string.Equals(conversation.Title, Conversation.DefaultTitle, StringComparison.Ordinal) && conversation.Messages.All(message => message.Role != ConversationMessageRole.Assistant);
        var currentUsageTask = usage.GetCurrentAsync(userId, cancellationToken);
        var personalizationTask = settings.GetPersonalizationAsync(userId, cancellationToken);
        await Task.WhenAll(currentUsageTask, personalizationTask).ConfigureAwait(false);
        var currentUsage = await currentUsageTask.ConfigureAwait(false);
        if (currentUsage.AnswersRemaining <= 0)
        {
            return new GroundedConversationTurnResult("usage_limit_reached", conversation, "You have reached the answer limit for this billing period. Your question was saved, but the model was not called.", null, processingStopwatch.Elapsed);
        }

        GroundedAnswerResult answerResult;
        try
        {
            var personalization = await personalizationTask.ConfigureAwait(false);
            var question = CreateQuestion(storedQuestion.Content, conversation.EnabledSourceKeys, personalization, shouldGenerateConversationTitle);
            var recentTurns = CreateRecentTurns(conversation.Messages, userMessageId);
            answerResult = await groundedAnswers.AnswerAsync(question, recentTurns, cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception exception) when (exception is HttpRequestException or AuthenticationFailedException or RequestFailedException or InvalidDataException)
        {
            logger.LogError(exception, "Grounded retrieval failed for conversation {ConversationId} and user {UserId}.", conversation.Id, userId);
            return new GroundedConversationTurnResult("retrieval_unavailable", conversation, "The approved source library is temporarily unavailable, so AskRabbi did not generate an unsupported answer. Please try again shortly.", null, processingStopwatch.Elapsed);
        }

        if (!answerResult.IsSuccess || answerResult.Answer is null)
        {
            LogTurnMetrics(conversation.Id, answerResult, processingStopwatch.Elapsed, false);
            if (answerResult.Status == GroundedAnswerStatus.ValidationFailed)
            {
                logger.LogWarning("Grounded validation rejected the generated answer for conversation {ConversationId}: {ValidationError}", conversation.Id, answerResult.ErrorMessage);
            }
            if (answerResult.Status is GroundedAnswerStatus.AIUnavailable or GroundedAnswerStatus.AuthenticationFailed)
            {
                logger.LogWarning("Grounded model stage failed for conversation {ConversationId}: provider status {ProviderStatus}, completion reason {CompletionReason}, response {ResponseId}.", conversation.Id, answerResult.Trace.ProviderStatus, answerResult.Trace.CompletionReason, answerResult.Trace.ResponseId);
            }
            return new GroundedConversationTurnResult(ToStatus(answerResult.Status), conversation, CreateClientFailureMessage(answerResult), answerResult.Trace, processingStopwatch.Elapsed);
        }
        var rendered = renderer.Render(answerResult.Answer);
        var sources = ConversationSourceMaterializer.Materialize(answerResult.Answer, answerResult.Evidence ?? throw new InvalidOperationException("A successful grounded answer must retain its trusted evidence packet."));
        var suggestedTitle = answerResult.Answer.SuggestedConversationTitle;
        var updated = shouldGenerateConversationTitle && !string.IsNullOrWhiteSpace(suggestedTitle)
            ? await conversations.AppendAssistantMessageWithTitleAsync(conversation, assistantMessageId, rendered, sources, suggestedTitle, cancellationToken).ConfigureAwait(false)
            : await conversations.AppendAssistantMessageAsync(conversation, assistantMessageId, rendered, sources, cancellationToken).ConfigureAwait(false);
        if (updated is null)
        {
            throw new InvalidOperationException("Conversation disappeared before its validated answer could be saved.");
        }
        await usage.RecordAnswerAsync(userId, cancellationToken).ConfigureAwait(false);
        processingStopwatch.Stop();
        LogTurnMetrics(conversation.Id, answerResult, processingStopwatch.Elapsed, true);
        return new GroundedConversationTurnResult("answered", updated, null, answerResult.Trace, processingStopwatch.Elapsed);
    }

    private void LogTurnMetrics(Guid conversationId, GroundedAnswerResult result, TimeSpan processingLatency, bool wasPersisted)
    {
        logger.LogInformation(
            "Grounded turn completed for conversation {ConversationId}: status {Status}, persisted {WasPersisted}, total {TotalMilliseconds} ms, retrieval {RetrievalMilliseconds} ms, model {ModelMilliseconds} ms, candidates {CandidateCount}, evidence {EvidenceCount}, evidence characters {EvidenceCharacterCount}, validation {ValidationStatus}, repair {RepairAttempted}, provider status {ProviderStatus}, completion reason {CompletionReason}, response {ResponseId}, provider attempts {ProviderAttempts}, input tokens {InputTokens}, output tokens {OutputTokens}, total tokens {TotalTokens}.",
            conversationId,
            result.Status,
            wasPersisted,
            processingLatency.TotalMilliseconds,
            result.Trace.RetrievalLatency.TotalMilliseconds,
            result.Trace.ModelLatency.TotalMilliseconds,
            result.Trace.CandidateCount,
            result.Trace.EvidenceCount,
            result.Trace.EvidenceCharacterCount,
            result.Trace.ValidationStatus,
            result.Trace.RepairAttempted,
            result.Trace.ProviderStatus,
            result.Trace.CompletionReason,
            result.Trace.ResponseId,
            result.Trace.ProviderAttempts,
            result.Trace.Usage?.InputTokens,
            result.Trace.Usage?.OutputTokens,
            result.Trace.Usage?.TotalTokens);
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

    private static GroundedQuestion CreateQuestion(string content, IReadOnlyList<string> sourceKeys, PersonalizationSettings? personalization, bool shouldGenerateConversationTitle)
    {
        return new GroundedQuestion
        {
            Question = content,
            ShouldGenerateConversationTitle = shouldGenerateConversationTitle,
            Languages = [],
            SourceKeys = sourceKeys,
            ConversationLanguage = personalization?.ConversationLanguage,
            QuotationLanguage = personalization?.QuotationLanguage,
            UserProfile = personalization is null ? null : new UserProfile
            {
                Name = personalization.FullName,
                DateOfBirth = personalization.BirthDate,
                TimeOfBirth = personalization.BirthTime,
                BirthTimeZone = personalization.BirthTimeZone,
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

    private static string? CreateClientFailureMessage(GroundedAnswerResult result) => result.Status switch
    {
        GroundedAnswerStatus.InsufficientEvidence => "I couldn't find enough relevant text in the sources selected for this conversation. Try naming the passage or enabling an additional source, and I'll take another look.",
        GroundedAnswerStatus.ValidationFailed => "AskARabbi could not fully support every statement with the cited sources, so it did not show the answer. Please try again.",
        GroundedAnswerStatus.AIUnavailable => "AskARabbi could not complete the grounded answer right now. Please try again.",
        GroundedAnswerStatus.AuthenticationFailed => "AskARabbi could not connect to its AI provider right now. Please try again shortly.",
        _ => result.ErrorMessage,
    };
}
