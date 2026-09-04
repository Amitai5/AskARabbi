using AskARabbiLIB.AI;
using AskARabbiLIB.Grounding;
using AskARabbiLIB.Retrieval;

namespace AskARabbi.Api.Configuration;

/// <summary>Configures production grounded-answer generation and managed retrieval.</summary>
public sealed record GroundedChatOptions
{
    /// <summary>Gets the configuration section name.</summary>
    public const string SectionName = "AI";

    /// <summary>Gets the Azure OpenAI resource endpoint.</summary>
    public string ProjectEndpoint { get; init; } = string.Empty;

    /// <summary>Gets the Azure OpenAI deployment name.</summary>
    public string ModelName { get; init; } = string.Empty;

    /// <summary>Gets the managed vector-store identifier.</summary>
    public string VectorStoreId { get; init; } = string.Empty;

    /// <summary>Gets the immutable lowercase corpus fingerprint.</summary>
    public string CorpusFingerprint { get; init; } = string.Empty;

    /// <summary>Gets an optional tenant override for local Entra authentication.</summary>
    public string? TenantId { get; init; }

    /// <summary>Gets the Azure model request timeout in seconds.</summary>
    public int TimeoutSeconds { get; init; } = 120;

    /// <summary>Gets the maximum combined reasoning and structured-output token count.</summary>
    public int MaximumOutputTokens { get; init; } = 8_000;

    /// <summary>Gets the smaller output budget used by the independent claim-support audit.</summary>
    public int ValidationMaximumOutputTokens { get; init; } = 1_600;

    /// <summary>Gets the reasoning effort used for answer generation.</summary>
    public AIReasoningEffort ReasoningEffort { get; init; } = AIReasoningEffort.Medium;

    /// <summary>Gets the processing tier used for conversational answer and validation calls.</summary>
    public AIServiceTier ServiceTier { get; init; } = AIServiceTier.Priority;

    /// <summary>Gets the maximum retries after an initial answer or audit request.</summary>
    public int MaximumRetryCount { get; init; } = 1;

    /// <summary>Gets the number of initial source candidates requested from managed retrieval.</summary>
    public int MaximumCandidates { get; init; } = 20;

    /// <summary>Gets the maximum source segments supplied to answer generation.</summary>
    public int MaximumEvidenceSegments { get; init; } = 10;

    /// <summary>Gets the total character budget for source evidence.</summary>
    public int MaximumEvidenceCharacters { get; init; } = 16_000;

    /// <summary>Gets the character budget for one source segment.</summary>
    public int MaximumCharactersPerSegment { get; init; } = 2_400;

    /// <summary>Gets the maximum evidence segments selected from one document edition.</summary>
    public int MaximumSegmentsPerDocument { get; init; } = 3;

    /// <summary>Gets the neighboring segment radius used when enrichment is enabled.</summary>
    public int ContextRadius { get; init; } = 2;

    /// <summary>Gets the number of hits that trigger additional retrieval calls; production defaults to zero to keep retrieval single-pass.</summary>
    public int MaximumEnrichmentHits { get; init; }

    /// <summary>Gets the recent conversational turns included in answer generation.</summary>
    public int RecentConversationTurns { get; init; } = 2;

    /// <summary>Gets the duration of the safe corpus-search cache in seconds.</summary>
    public int RetrievalCacheSeconds { get; init; } = 600;

    /// <summary>Gets the maximum distinct corpus searches retained by one API process.</summary>
    public int RetrievalCacheMaximumEntries { get; init; } = 256;

    /// <summary>Gets the direct vector-search score threshold.</summary>
    public double RetrievalScoreThreshold { get; init; }

    /// <summary>Gets whether all required provider and corpus settings are present.</summary>
    public bool IsConfigured => !string.IsNullOrWhiteSpace(ProjectEndpoint)
        && !string.IsNullOrWhiteSpace(ModelName)
        && !string.IsNullOrWhiteSpace(VectorStoreId)
        && !string.IsNullOrWhiteSpace(CorpusFingerprint);

    /// <summary>Validates complete configuration while allowing every provider setting to be omitted for process-only health checks.</summary>
    public void Validate()
    {
        var hasProviderConfiguration = !string.IsNullOrWhiteSpace(ProjectEndpoint)
            || !string.IsNullOrWhiteSpace(ModelName)
            || !string.IsNullOrWhiteSpace(VectorStoreId)
            || !string.IsNullOrWhiteSpace(CorpusFingerprint)
            || !string.IsNullOrWhiteSpace(TenantId);
        if (!hasProviderConfiguration)
        {
            ValidateLimits();
            return;
        }
        if (!Uri.TryCreate(ProjectEndpoint, UriKind.Absolute, out var endpoint) || !string.Equals(endpoint.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException($"{SectionName}:{nameof(ProjectEndpoint)} must be an absolute HTTPS URI.");
        }
        if (string.IsNullOrWhiteSpace(ModelName))
        {
            throw new InvalidOperationException($"{SectionName}:{nameof(ModelName)} is required.");
        }
        if (string.IsNullOrWhiteSpace(VectorStoreId))
        {
            throw new InvalidOperationException($"{SectionName}:{nameof(VectorStoreId)} is required.");
        }
        if (CorpusFingerprint is not { Length: 64 } || CorpusFingerprint.Any(character => character is not (>= '0' and <= '9') and not (>= 'a' and <= 'f')))
        {
            throw new InvalidOperationException($"{SectionName}:{nameof(CorpusFingerprint)} must be a lowercase SHA-256 value.");
        }
        if (!string.IsNullOrWhiteSpace(TenantId) && !Guid.TryParse(TenantId, out _))
        {
            throw new InvalidOperationException($"{SectionName}:{nameof(TenantId)} must be a GUID when configured.");
        }

        ValidateLimits();
    }

    /// <summary>Creates validated retrieval and evidence budgets for the grounded-answer service.</summary>
    /// <returns>Grounded-answer options matching this API configuration.</returns>
    public GroundedAnswerOptions CreateGroundedAnswerOptions() => new()
    {
        MaximumCandidates = MaximumCandidates,
        MaximumEvidenceSegments = MaximumEvidenceSegments,
        MaximumEvidenceCharacters = MaximumEvidenceCharacters,
        MaximumCharactersPerSegment = MaximumCharactersPerSegment,
        MaximumSegmentsPerDocument = MaximumSegmentsPerDocument,
        ContextRadius = ContextRadius,
        MaximumEnrichmentHits = MaximumEnrichmentHits,
        RecentConversationTurns = RecentConversationTurns,
    };

    /// <summary>Creates validated process-local retrieval cache settings.</summary>
    /// <returns>Source-retrieval cache options matching this API configuration.</returns>
    public SourceRetrieverCacheOptions CreateRetrieverCacheOptions() => new()
    {
        Duration = TimeSpan.FromSeconds(RetrievalCacheSeconds),
        MaximumEntries = RetrievalCacheMaximumEntries,
    };

    private void ValidateLimits()
    {
        if (TimeoutSeconds is < 1 or > 600)
        {
            throw new InvalidOperationException($"{SectionName}:{nameof(TimeoutSeconds)} must be between 1 and 600.");
        }
        if (MaximumOutputTokens is < 1 or > 100_000)
        {
            throw new InvalidOperationException($"{SectionName}:{nameof(MaximumOutputTokens)} must be between 1 and 100,000.");
        }
        if (ValidationMaximumOutputTokens is < 1 or > 100_000)
        {
            throw new InvalidOperationException($"{SectionName}:{nameof(ValidationMaximumOutputTokens)} must be between 1 and 100,000.");
        }
        if (!Enum.IsDefined(ReasoningEffort))
        {
            throw new InvalidOperationException($"{SectionName}:{nameof(ReasoningEffort)} is not supported.");
        }
        if (!Enum.IsDefined(ServiceTier))
        {
            throw new InvalidOperationException($"{SectionName}:{nameof(ServiceTier)} is not supported.");
        }
        if (MaximumRetryCount is < 0 or > 5)
        {
            throw new InvalidOperationException($"{SectionName}:{nameof(MaximumRetryCount)} must be between zero and five.");
        }
        if (RetrievalScoreThreshold is < 0 or > 1)
        {
            throw new InvalidOperationException($"{SectionName}:{nameof(RetrievalScoreThreshold)} must be between zero and one.");
        }

        try
        {
            CreateGroundedAnswerOptions().Validate();
            CreateRetrieverCacheOptions().Validate();
        }
        catch (ArgumentOutOfRangeException exception)
        {
            throw new InvalidOperationException($"{SectionName} retrieval limits are invalid: {exception.Message}", exception);
        }
    }
}
