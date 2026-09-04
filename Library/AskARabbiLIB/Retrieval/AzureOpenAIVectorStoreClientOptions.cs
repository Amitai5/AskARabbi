using AskARabbiLIB.AI;

namespace AskARabbiLIB.Retrieval;

/// <summary>Configures authenticated requests to Azure OpenAI vector-store APIs.</summary>
public sealed record AzureOpenAIVectorStoreClientOptions
{
    /// <summary>Gets the Azure OpenAI resource or Foundry project endpoint.</summary>
    public required Uri ProjectEndpoint { get; init; }

    /// <summary>Gets the Azure OpenAI deployment used only for Responses file-search retrieval.</summary>
    public string? ModelName { get; init; }

    /// <summary>Gets the processing tier requested for Responses file-search retrieval.</summary>
    public AIServiceTier ServiceTier { get; init; } = AIServiceTier.Auto;

    /// <summary>Gets the end-to-end timeout for one vector-store request.</summary>
    public TimeSpan Timeout { get; init; } = TimeSpan.FromMinutes(2);

    /// <summary>Validates the endpoint and request timeout.</summary>
    public void Validate()
    {
        if (ProjectEndpoint is null || !ProjectEndpoint.IsAbsoluteUri || !string.Equals(ProjectEndpoint.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException("Vector-store endpoint must be an absolute HTTPS URI.", nameof(ProjectEndpoint));
        }
        if (Timeout <= TimeSpan.Zero || Timeout > TimeSpan.FromMinutes(10))
        {
            throw new ArgumentOutOfRangeException(nameof(Timeout), "Vector-store timeout must be greater than zero and no more than ten minutes.");
        }
        if (ModelName is not null && string.IsNullOrWhiteSpace(ModelName))
        {
            throw new ArgumentException("Vector-store retrieval model cannot be empty when supplied.", nameof(ModelName));
        }
        if (!Enum.IsDefined(ServiceTier))
        {
            throw new ArgumentOutOfRangeException(nameof(ServiceTier), "AI service tier is not supported.");
        }
    }
}
