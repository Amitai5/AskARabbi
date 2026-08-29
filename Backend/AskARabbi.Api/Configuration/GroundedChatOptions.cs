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

    /// <summary>Gets the maximum structured-output token count.</summary>
    public int MaximumOutputTokens { get; init; } = 2_000;

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
        if (RetrievalScoreThreshold is < 0 or > 1)
        {
            throw new InvalidOperationException($"{SectionName}:{nameof(RetrievalScoreThreshold)} must be between zero and one.");
        }
    }
}
