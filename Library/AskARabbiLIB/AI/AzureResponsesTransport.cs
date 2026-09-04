using System.ClientModel;
using System.Text.Json;
using Azure.AI.OpenAI;
using Azure.Core;
using Azure.Identity;
using OpenAI.Responses;

namespace AskARabbiLIB.AI;

internal sealed class AzureResponsesTransport : IAIResponseTransport
{
    private readonly ResponsesClient client;
    private readonly bool usesApiKey;

    internal AzureResponsesTransport(AIEngineOptions options, TokenCredential credential)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(credential);
        var azureClient = new AzureOpenAIClient(options.ProjectEndpoint, credential, new AzureOpenAIClientOptions { NetworkTimeout = options.Timeout });
        client = azureClient.GetResponsesClient();
        usesApiKey = false;
    }

    internal AzureResponsesTransport(AIEngineOptions options, ApiKeyCredential credential)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(credential);
        var azureClient = new AzureOpenAIClient(options.ProjectEndpoint, credential, new AzureOpenAIClientOptions { NetworkTimeout = options.Timeout });
        client = azureClient.GetResponsesClient();
        usesApiKey = true;
    }

    /// <inheritdoc cref="IAIResponseTransport.SendAsync"/>
    public async Task<AITransportResult> SendAsync(AITransportRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var responseOptions = CreateOptions(request);
        try
        {
            AIUsage? aggregateUsage = null;
            while (true)
            {
                ClientResult<ResponseResult> response = await client.CreateResponseAsync(responseOptions, cancellationToken).ConfigureAwait(false);
                var value = response.Value;
                var responseUsage = value.Usage is null ? null : new AIUsage(value.Usage.InputTokenCount, value.Usage.OutputTokenCount, value.Usage.TotalTokenCount);
                aggregateUsage = CombineUsage(aggregateUsage, responseUsage);
                var responseId = value.Id;
                var responseModel = string.IsNullOrWhiteSpace(value.Model) ? request.Model : value.Model;
                var completionReason = GetCompletionReason(value);
                if (string.Equals(completionReason, "content_filter", StringComparison.OrdinalIgnoreCase))
                {
                    completionReason = GetContentFilterCompletionReason(response.GetRawResponse().Content, completionReason);
                }
                var functionCalls = value.OutputItems.OfType<FunctionCallResponseItem>().ToArray();
                if (functionCalls.Length > 0)
                {
                    if (request.ToolSession is null)
                    {
                        return new AITransportResult(AIEngineStatus.InvalidResponse, null, "Azure OpenAI requested a tool when no tool session was configured.", responseId, responseModel, aggregateUsage, false, "unexpected_tool_call");
                    }

                    AppendResponseOutputItems(responseOptions, value.OutputItems);
                    foreach (var functionCall in functionCalls)
                    {
                        var output = await request.ToolSession.ExecuteAsync(functionCall.FunctionName, functionCall.FunctionArguments, cancellationToken).ConfigureAwait(false);
                        responseOptions.InputItems.Add(new FunctionCallOutputResponseItem(functionCall.CallId, output.ToString()));
                    }
                    if (request.ToolSession.ExecutionCount >= request.ToolSession.MaximumExecutionCount)
                    {
                        responseOptions.ToolChoice = ResponseToolChoice.CreateNoneChoice();
                        responseOptions.Tools.Clear();
                    }
                    continue;
                }

                if (value.Status == ResponseStatus.Completed)
                {
                    return new AITransportResult(AIEngineStatus.Success, value.GetOutputText(), null, responseId, responseModel, aggregateUsage, false, completionReason);
                }

                if (value.Status == ResponseStatus.Failed && value.Error?.Code == ResponseErrorCode.RateLimitExceeded)
                {
                    return new AITransportResult(AIEngineStatus.RateLimited, null, value.Error.Message, responseId, responseModel, aggregateUsage, true, completionReason);
                }

                if (value.Status == ResponseStatus.Failed && value.Error?.Code == ResponseErrorCode.ServerError)
                {
                    return new AITransportResult(AIEngineStatus.ProviderFailure, null, value.Error.Message, responseId, responseModel, aggregateUsage, true, completionReason);
                }

                var error = value.Error?.Message ?? $"Azure OpenAI returned response status '{value.Status}'.";
                return new AITransportResult(AIEngineStatus.InvalidResponse, null, error, responseId, responseModel, aggregateUsage, false, completionReason);
            }
        }
        catch (ClientResultException exception) when (exception.Status == 429)
        {
            return new AITransportResult(AIEngineStatus.RateLimited, null, exception.Message, null, request.Model, null, true, "http_429");
        }
        catch (ClientResultException exception) when (exception.Status == 401)
        {
            return CreateAuthorizationFailure(401, request.Model, exception.Message);
        }
        catch (ClientResultException exception) when (exception.Status == 403)
        {
            return CreateAuthorizationFailure(403, request.Model, exception.Message);
        }
        catch (ClientResultException exception) when (exception.Status >= 500)
        {
            return new AITransportResult(AIEngineStatus.ProviderFailure, null, exception.Message, null, request.Model, null, true, "http_5xx");
        }
        catch (ClientResultException exception)
        {
            return new AITransportResult(AIEngineStatus.ProviderFailure, null, exception.Message, null, request.Model, null, false, $"http_{exception.Status}");
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (AuthenticationFailedException exception)
        {
            var message = $"Azure could not obtain an Entra access token. Refresh the selected local Azure sign-in and try again. Authentication detail: {exception.Message}";
            return new AITransportResult(AIEngineStatus.Unauthorized, null, message, null, request.Model, null, false, "authentication_failed");
        }
        catch (Azure.RequestFailedException exception) when (exception.Status == 401)
        {
            return CreateAuthorizationFailure(401, request.Model, exception.Message);
        }
        catch (Azure.RequestFailedException exception) when (exception.Status == 403)
        {
            return CreateAuthorizationFailure(403, request.Model, exception.Message);
        }
        catch (Azure.RequestFailedException exception) when (exception.Status == 429)
        {
            return new AITransportResult(AIEngineStatus.RateLimited, null, exception.Message, null, request.Model, null, true, "http_429");
        }
        catch (Azure.RequestFailedException exception) when (exception.Status >= 500)
        {
            return new AITransportResult(AIEngineStatus.ProviderFailure, null, exception.Message, null, request.Model, null, true, "http_5xx");
        }
        catch (HttpRequestException exception)
        {
            return new AITransportResult(AIEngineStatus.ProviderFailure, null, exception.Message, null, request.Model, null, true, "network_error");
        }
        catch (Exception exception)
        {
            return new AITransportResult(AIEngineStatus.ProviderFailure, null, exception.Message, null, request.Model, null, false, "provider_exception");
        }
    }

    internal static CreateResponseOptions CreateOptions(AITransportRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        var options = new CreateResponseOptions
        {
            Model = request.Model,
            StoredOutputEnabled = false,
            MaxOutputTokenCount = request.MaximumOutputTokens,
            TruncationMode = ResponseTruncationMode.Disabled,
            ServiceTier = MapServiceTier(request.ServiceTier),
            ReasoningOptions = new ResponseReasoningOptions
            {
                ReasoningEffortLevel = request.ReasoningEffort switch
                {
                    AIReasoningEffort.Low => ResponseReasoningEffortLevel.Low,
                    AIReasoningEffort.Medium => ResponseReasoningEffortLevel.Medium,
                    AIReasoningEffort.High => ResponseReasoningEffortLevel.High,
                    _ => throw new ArgumentOutOfRangeException(nameof(request)),
                },
            },
            TextOptions = new ResponseTextOptions
            {
                TextFormat = ResponseTextFormat.CreateJsonSchemaFormat(request.SchemaName, request.JsonSchema, "Grounded answer response", true),
            },
        };

        foreach (var message in request.Messages)
        {
            options.InputItems.Add(message.Role switch
            {
                AIMessageRole.System => MessageResponseItem.CreateSystemMessageItem(message.Content),
                AIMessageRole.User => MessageResponseItem.CreateUserMessageItem(message.Content),
                AIMessageRole.Assistant => MessageResponseItem.CreateAssistantMessageItem(message.Content),
                _ => throw new ArgumentOutOfRangeException(nameof(request)),
            });
        }

        if (request.ToolSession is not null)
        {
            foreach (var definition in request.ToolSession.Definitions)
            {
                options.Tools.Add(ResponseTool.CreateFunctionTool(definition.Name, definition.ParametersJsonSchema, false, definition.Description));
            }
            options.MaxToolCallCount = request.ToolSession.MaximumExecutionCount;
            options.ParallelToolCallsEnabled = false;
        }

        return options;
    }

    private static ResponseServiceTier? MapServiceTier(AIServiceTier serviceTier) => serviceTier switch
    {
        AIServiceTier.Auto => default(ResponseServiceTier?),
        AIServiceTier.Standard => ResponseServiceTier.Default,
        AIServiceTier.Priority => (ResponseServiceTier)"priority",
        _ => throw new ArgumentOutOfRangeException(nameof(serviceTier)),
    };

    internal static void AppendResponseOutputItems(CreateResponseOptions responseOptions, IEnumerable<ResponseItem> outputItems)
    {
        ArgumentNullException.ThrowIfNull(responseOptions);
        ArgumentNullException.ThrowIfNull(outputItems);
        foreach (var outputItem in outputItems)
        {
            responseOptions.InputItems.Add(outputItem);
        }
    }

    private static AIUsage? CombineUsage(AIUsage? first, AIUsage? second)
    {
        if (first is null)
        {
            return second;
        }
        if (second is null)
        {
            return first;
        }
        return new AIUsage(first.InputTokens + second.InputTokens, first.OutputTokens + second.OutputTokens, first.TotalTokens + second.TotalTokens);
    }

    private static string GetCompletionReason(ResponseResult response) => GetCompletionReason(response.Status?.ToString(), response.IncompleteStatusDetails?.Reason.ToString(), response.Error?.Code.ToString());

    internal static string GetCompletionReason(string? status, string? incompleteReason, string? errorCode)
    {
        if (string.Equals(status, ResponseStatus.Completed.ToString(), StringComparison.Ordinal))
        {
            return "completed";
        }
        if (string.Equals(status, ResponseStatus.Incomplete.ToString(), StringComparison.Ordinal))
        {
            return incompleteReason ?? "incomplete";
        }
        if (string.Equals(status, ResponseStatus.Failed.ToString(), StringComparison.Ordinal))
        {
            return errorCode ?? "failed";
        }
        return status ?? "unknown";
    }

    internal static string GetContentFilterCompletionReason(BinaryData responseContent, string fallback = "content_filter")
    {
        if (responseContent is null || string.IsNullOrWhiteSpace(fallback))
        {
            return "content_filter";
        }

        try
        {
            using var document = JsonDocument.Parse(responseContent);
            if (!document.RootElement.TryGetProperty("content_filters", out var filters) || filters.ValueKind != JsonValueKind.Array)
            {
                return fallback;
            }

            foreach (var filter in filters.EnumerateArray())
            {
                if (filter.ValueKind != JsonValueKind.Object || !filter.TryGetProperty("blocked", out var blocked) || blocked.ValueKind != JsonValueKind.True)
                {
                    continue;
                }

                var source = GetKnownFilterSource(filter);
                var categories = GetBlockedFilterCategories(filter);
                return categories.Count == 0
                    ? $"content_filter.{source}"
                    : $"content_filter.{source}.{string.Join('.', categories)}";
            }
        }
        catch (JsonException)
        {
            return fallback;
        }

        return fallback;
    }

    private static string GetKnownFilterSource(JsonElement filter)
    {
        if (filter.TryGetProperty("source_type", out var source) && source.ValueKind == JsonValueKind.String)
        {
            return source.GetString() switch
            {
                "prompt" => "prompt",
                "completion" => "completion",
                _ => "unknown",
            };
        }

        return "unknown";
    }

    private static IReadOnlyList<string> GetBlockedFilterCategories(JsonElement filter)
    {
        if (!filter.TryGetProperty("content_filter_results", out var results) || results.ValueKind != JsonValueKind.Object)
        {
            return [];
        }

        var categories = new SortedSet<string>(StringComparer.Ordinal);
        foreach (var result in results.EnumerateObject())
        {
            var category = GetKnownFilterCategory(result.Name);
            if (category is null || result.Value.ValueKind != JsonValueKind.Object || !IsFiltered(result.Value))
            {
                continue;
            }

            var severity = GetKnownSeverity(result.Value);
            categories.Add(severity is null ? category : $"{category}_{severity}");
        }

        return categories.ToArray();
    }

    private static string? GetKnownFilterCategory(string value)
    {
        var normalized = value.Trim().ToLowerInvariant().Replace('-', '_').Replace(' ', '_');
        return normalized switch
        {
            "hate" => "hate",
            "sexual" => "sexual",
            "violence" => "violence",
            "self_harm" => "self_harm",
            "jailbreak" => "jailbreak",
            "indirect_attack" => "indirect_attack",
            "protected_material_text" => "protected_material_text",
            "protected_material_code" => "protected_material_code",
            "phone" or "phone_number" or "phone_number_protection" or "telephone_number" => "phone_number",
            "email" or "email_address" or "email_protection" => "email_address",
            "ip" or "ip_address" or "ip_address_protection" => "ip_address",
            "pii" or "personal_data" or "personally_identifiable_information" => "personal_data",
            _ => null,
        };
    }

    private static bool IsFiltered(JsonElement result) => IsTrue(result, "filtered") || IsTrue(result, "detected");

    private static bool IsTrue(JsonElement result, string propertyName) => result.TryGetProperty(propertyName, out var value) && value.ValueKind == JsonValueKind.True;

    private static string? GetKnownSeverity(JsonElement result)
    {
        if (!result.TryGetProperty("severity", out var severity) || severity.ValueKind != JsonValueKind.String)
        {
            return null;
        }

        return severity.GetString() switch
        {
            "safe" => "safe",
            "low" => "low",
            "medium" => "medium",
            "high" => "high",
            _ => null,
        };
    }

    private AITransportResult CreateAuthorizationFailure(int status, string model, string providerDetail)
    {
        var guidance = (usesApiKey, status) switch
        {
            (true, 401) => "Azure rejected AI:APIKey. Verify that the key belongs to AI:ProjectEndpoint and copy the current resource key without surrounding quotes or whitespace.",
            (true, _) => "Azure denied the API-key request. Verify that local/key authentication is enabled on the resource and that AI:ModelName names a deployment on AI:ProjectEndpoint.",
            (false, 401) => "Azure rejected the current Entra identity. Refresh the local Azure sign-in, restart AI chat, and verify that the endpoint belongs to the same tenant.",
            _ => "Azure authenticated the identity but denied model inference. Verify that it has Cognitive Services OpenAI User on an Azure OpenAI resource, or Cognitive Services User on a Foundry resource.",
        };

        return new AITransportResult(AIEngineStatus.Unauthorized, null, $"{guidance} Provider detail: {providerDetail}", null, model, null, false, $"http_{status}");
    }
}
