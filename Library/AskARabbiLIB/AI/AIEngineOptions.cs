namespace AskARabbiLIB.AI;

/// <summary>Controls Azure OpenAI response generation.</summary>
public sealed record AIEngineOptions
{
    /// <summary>Gets the HTTPS endpoint for the Azure OpenAI or Foundry project.</summary>
    public required Uri ProjectEndpoint { get; init; }

    /// <summary>Gets the model deployment name supplied on every request.</summary>
    public required string ModelName { get; init; }

    /// <summary>Gets the end-to-end request timeout.</summary>
    public TimeSpan Timeout { get; init; } = TimeSpan.FromSeconds(120);

    /// <summary>Gets the maximum number of output tokens requested from the provider.</summary>
    public int MaximumOutputTokens { get; init; } = 2_000;

    /// <summary>Gets the provider-neutral reasoning effort.</summary>
    public AIReasoningEffort ReasoningEffort { get; init; } = AIReasoningEffort.Medium;

    /// <summary>Gets the provider processing tier requested for each generation.</summary>
    public AIServiceTier ServiceTier { get; init; } = AIServiceTier.Auto;

    /// <summary>Gets the maximum number of retries after the initial provider request.</summary>
    public int MaximumRetryCount { get; init; } = 2;

    /// <summary>Validates endpoint, model, timeout, output, and retry settings.</summary>
    public void Validate()
    {
        if (ProjectEndpoint is null || !ProjectEndpoint.IsAbsoluteUri || !string.Equals(ProjectEndpoint.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException("AI project endpoint must be an absolute HTTPS URI.", nameof(ProjectEndpoint));
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(ModelName);
        if (Timeout <= TimeSpan.Zero || Timeout > TimeSpan.FromMinutes(10))
        {
            throw new ArgumentOutOfRangeException(nameof(Timeout), "AI timeout must be greater than zero and no more than ten minutes.");
        }

        if (MaximumOutputTokens is < 1 or > 100_000)
        {
            throw new ArgumentOutOfRangeException(nameof(MaximumOutputTokens), "Maximum output tokens must be between 1 and 100,000.");
        }

        if (!Enum.IsDefined(ServiceTier))
        {
            throw new ArgumentOutOfRangeException(nameof(ServiceTier), "AI service tier is not supported.");
        }

        if (MaximumRetryCount is < 0 or > 5)
        {
            throw new ArgumentOutOfRangeException(nameof(MaximumRetryCount), "Maximum retry count must be between 0 and 5.");
        }
    }
}
