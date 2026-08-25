using System.ClientModel;
using AskARabbiLIB.AI;
using Microsoft.Extensions.Configuration;

namespace AskARabbiPrototype;

internal static class AIEngineFactory
{
    internal static AzureOpenAIEngine Create(IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        var endpointValue = configuration["AI:ProjectEndpoint"];
        var modelName = configuration["AI:ModelName"];
        var apiKey = configuration["AI:APIKey"];
        var problems = new List<string>();

        Uri? endpoint = null;
        if (!Uri.TryCreate(endpointValue, UriKind.Absolute, out endpoint) || !string.Equals(endpoint.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase))
        {
            problems.Add("AI:ProjectEndpoint is missing or is not an absolute HTTPS URL");
        }

        if (string.IsNullOrWhiteSpace(modelName))
        {
            problems.Add("AI:ModelName is empty");
        }

        if (string.IsNullOrWhiteSpace(apiKey))
        {
            problems.Add("AI:APIKey is empty");
        }

        if (problems.Count > 0)
        {
            throw new InvalidOperationException($"AI chat configuration is invalid: {string.Join("; ", problems)}. Set values in the ignored root appsettings.json or use AI__ProjectEndpoint, AI__ModelName, and AI__APIKey environment variables.");
        }

        if (endpoint is null || modelName is null || apiKey is null)
        {
            throw new InvalidOperationException("AI chat configuration validation completed without usable values.");
        }

        var options = new AIEngineOptions
        {
            ProjectEndpoint = endpoint,
            ModelName = modelName.Trim(),
            MaximumOutputTokens = 2_000,
        };

        return new AzureOpenAIEngine(options, new ApiKeyCredential(apiKey.Trim()));
    }
}
