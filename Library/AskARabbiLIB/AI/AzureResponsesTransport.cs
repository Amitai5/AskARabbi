using System.ClientModel;
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
                var functionCalls = value.OutputItems.OfType<FunctionCallResponseItem>().ToArray();
                if (functionCalls.Length > 0)
                {
                    if (request.ToolSession is null)
                    {
                        return new AITransportResult(AIEngineStatus.InvalidResponse, null, "Azure OpenAI requested a tool when no tool session was configured.", responseId, responseModel, aggregateUsage, false);
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
                    return new AITransportResult(AIEngineStatus.Success, value.GetOutputText(), null, responseId, responseModel, aggregateUsage, false);
                }

                if (value.Status == ResponseStatus.Failed && value.Error?.Code == ResponseErrorCode.RateLimitExceeded)
                {
                    return new AITransportResult(AIEngineStatus.RateLimited, null, value.Error.Message, responseId, responseModel, aggregateUsage, true);
                }

                if (value.Status == ResponseStatus.Failed && value.Error?.Code == ResponseErrorCode.ServerError)
                {
                    return new AITransportResult(AIEngineStatus.ProviderFailure, null, value.Error.Message, responseId, responseModel, aggregateUsage, true);
                }

                var error = value.Error?.Message ?? $"Azure OpenAI returned response status '{value.Status}'.";
                return new AITransportResult(AIEngineStatus.InvalidResponse, null, error, responseId, responseModel, aggregateUsage, false);
            }
        }
        catch (ClientResultException exception) when (exception.Status == 429)
        {
            return new AITransportResult(AIEngineStatus.RateLimited, null, exception.Message, null, request.Model, null, true);
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
            return new AITransportResult(AIEngineStatus.ProviderFailure, null, exception.Message, null, request.Model, null, true);
        }
        catch (ClientResultException exception)
        {
            return new AITransportResult(AIEngineStatus.ProviderFailure, null, exception.Message, null, request.Model, null, false);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (AuthenticationFailedException exception)
        {
            var message = $"Azure could not obtain an Entra access token. Refresh the selected local Azure sign-in and try again. Authentication detail: {exception.Message}";
            return new AITransportResult(AIEngineStatus.Unauthorized, null, message, null, request.Model, null, false);
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
            return new AITransportResult(AIEngineStatus.RateLimited, null, exception.Message, null, request.Model, null, true);
        }
        catch (Azure.RequestFailedException exception) when (exception.Status >= 500)
        {
            return new AITransportResult(AIEngineStatus.ProviderFailure, null, exception.Message, null, request.Model, null, true);
        }
        catch (HttpRequestException exception)
        {
            return new AITransportResult(AIEngineStatus.ProviderFailure, null, exception.Message, null, request.Model, null, true);
        }
        catch (Exception exception)
        {
            return new AITransportResult(AIEngineStatus.ProviderFailure, null, exception.Message, null, request.Model, null, false);
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

    private AITransportResult CreateAuthorizationFailure(int status, string model, string providerDetail)
    {
        var guidance = (usesApiKey, status) switch
        {
            (true, 401) => "Azure rejected AI:APIKey. Verify that the key belongs to AI:ProjectEndpoint and copy the current resource key without surrounding quotes or whitespace.",
            (true, _) => "Azure denied the API-key request. Verify that local/key authentication is enabled on the resource and that AI:ModelName names a deployment on AI:ProjectEndpoint.",
            (false, 401) => "Azure rejected the current Entra identity. Refresh the local Azure sign-in, restart AI chat, and verify that the endpoint belongs to the same tenant.",
            _ => "Azure authenticated the identity but denied model inference. Verify that it has Cognitive Services OpenAI User on an Azure OpenAI resource, or Cognitive Services User on a Foundry resource.",
        };

        return new AITransportResult(AIEngineStatus.Unauthorized, null, $"{guidance} Provider detail: {providerDetail}", null, model, null, false);
    }
}
