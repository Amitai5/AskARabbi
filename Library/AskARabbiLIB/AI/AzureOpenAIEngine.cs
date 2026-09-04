using System.ClientModel;
using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Serialization;
using AskARabbiLIB.AI.Tools;
using Azure.Core;
using Azure.Identity;

namespace AskARabbiLIB.AI;

/// <summary>Generates strict structured outputs through Azure OpenAI using API-key or Entra authentication.</summary>
public sealed class AzureOpenAIEngine : IAIEngine
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNameCaseInsensitive = false,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
    };

    private readonly AIEngineOptions options;
    private readonly IAIResponseTransport transport;
    private readonly Func<TimeSpan, CancellationToken, Task> delayAsync;

    /// <summary>Creates an Azure OpenAI engine without performing network work.</summary>
    /// <param name="options">Validated Azure endpoint, model, and request limits.</param>
    /// <param name="credential">Optional Entra credential; defaults to DefaultAzureCredential.</param>
    public AzureOpenAIEngine(AIEngineOptions options, TokenCredential? credential = null)
    {
        ArgumentNullException.ThrowIfNull(options);
        options.Validate();
        this.options = options;
        transport = new AzureResponsesTransport(options, credential ?? new DefaultAzureCredential());
        delayAsync = Task.Delay;
    }

    /// <summary>Creates an Azure OpenAI engine that authenticates with an API-key credential without performing network work.</summary>
    /// <param name="options">Validated Azure endpoint, model, and request limits.</param>
    /// <param name="credential">API-key credential for the configured Azure OpenAI resource.</param>
    public AzureOpenAIEngine(AIEngineOptions options, ApiKeyCredential credential)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(credential);
        options.Validate();
        this.options = options;
        transport = new AzureResponsesTransport(options, credential);
        delayAsync = Task.Delay;
    }

    internal AzureOpenAIEngine(AIEngineOptions options, IAIResponseTransport transport, Func<TimeSpan, CancellationToken, Task>? delayAsync = null)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(transport);
        options.Validate();
        this.options = options;
        this.transport = transport;
        this.delayAsync = delayAsync ?? Task.Delay;
    }

    /// <inheritdoc cref="IAIEngine.GenerateStructuredAsync{T}"/>
    public async Task<AIEngineResult<T>> GenerateStructuredAsync<T>(IReadOnlyList<AIMessage> messages, string schemaName, BinaryData jsonSchema, CancellationToken cancellationToken = default)
    {
        return await GenerateCoreAsync<T>(messages, schemaName, jsonSchema, null, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc cref="IAIEngine.GenerateStructuredAsync{T}(IReadOnlyList{AIMessage}, string, BinaryData, AIToolExecutionSession, CancellationToken)"/>
    public async Task<AIEngineResult<T>> GenerateStructuredAsync<T>(IReadOnlyList<AIMessage> messages, string schemaName, BinaryData jsonSchema, AIToolExecutionSession toolSession, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(toolSession);
        return await GenerateCoreAsync<T>(messages, schemaName, jsonSchema, toolSession, cancellationToken).ConfigureAwait(false);
    }

    private async Task<AIEngineResult<T>> GenerateCoreAsync<T>(IReadOnlyList<AIMessage> messages, string schemaName, BinaryData jsonSchema, AIToolExecutionSession? toolSession, CancellationToken cancellationToken)
    {
        ValidateRequest(messages, schemaName, jsonSchema);
        var stopwatch = Stopwatch.StartNew();
        using var timeoutSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutSource.CancelAfter(options.Timeout);
        var request = new AITransportRequest(messages.ToArray(), schemaName, jsonSchema, options.ModelName, options.MaximumOutputTokens, options.ReasoningEffort, toolSession, options.ServiceTier);
        AITransportResult? lastResult = null;

        for (var attempt = 0; attempt <= options.MaximumRetryCount; attempt++)
        {
            try
            {
                lastResult = await transport.SendAsync(request, timeoutSource.Token).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (OperationCanceledException)
            {
                stopwatch.Stop();
                return AIEngineResult<T>.Failure(AIEngineStatus.TimedOut, $"AI request exceeded the {options.Timeout.TotalSeconds:N0}-second timeout.", CreateDiagnostics(null, stopwatch.Elapsed, attempt + 1, AIEngineStatus.TimedOut, "client_timeout"));
            }

            if (lastResult.Status == AIEngineStatus.Success)
            {
                stopwatch.Stop();
                return DeserializeResult<T>(lastResult, stopwatch.Elapsed, attempt + 1);
            }
            if (!lastResult.Retryable || attempt == options.MaximumRetryCount)
            {
                stopwatch.Stop();
                return AIEngineResult<T>.Failure(lastResult.Status, lastResult.ErrorMessage ?? "The AI provider returned an unspecified failure.", CreateDiagnostics(lastResult, stopwatch.Elapsed, attempt + 1));
            }

            var delay = TimeSpan.FromSeconds(Math.Pow(2, attempt));
            try
            {
                await delayAsync(delay, timeoutSource.Token).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (OperationCanceledException)
            {
                stopwatch.Stop();
                return AIEngineResult<T>.Failure(AIEngineStatus.TimedOut, $"AI request exceeded the {options.Timeout.TotalSeconds:N0}-second timeout.", CreateDiagnostics(lastResult, stopwatch.Elapsed, attempt + 1, AIEngineStatus.TimedOut, "client_timeout"));
            }
        }

        stopwatch.Stop();
        return AIEngineResult<T>.Failure(AIEngineStatus.ProviderFailure, "The AI request ended without a provider result.", CreateDiagnostics(lastResult, stopwatch.Elapsed, options.MaximumRetryCount + 1, AIEngineStatus.ProviderFailure, "no_provider_result"));
    }

    private AIEngineResult<T> DeserializeResult<T>(AITransportResult result, TimeSpan latency, int attempts)
    {
        if (string.IsNullOrWhiteSpace(result.OutputJson))
        {
            return AIEngineResult<T>.Failure(AIEngineStatus.InvalidResponse, "The AI provider returned no structured output.", CreateDiagnostics(result, latency, attempts, AIEngineStatus.InvalidResponse, result.CompletionReason ?? "empty_structured_output"));
        }
        try
        {
            var value = JsonSerializer.Deserialize<T>(result.OutputJson, SerializerOptions);
            return value is null
                ? AIEngineResult<T>.Failure(AIEngineStatus.InvalidResponse, "The AI provider returned a null structured output.", CreateDiagnostics(result, latency, attempts, AIEngineStatus.InvalidResponse, "null_structured_output"))
                : AIEngineResult<T>.Success(value, CreateDiagnostics(result, latency, attempts));
        }
        catch (JsonException exception)
        {
            return AIEngineResult<T>.Failure(AIEngineStatus.InvalidResponse, $"The AI provider returned invalid structured JSON: {exception.Message}", CreateDiagnostics(result, latency, attempts, AIEngineStatus.InvalidResponse, "invalid_structured_json"));
        }
    }

    private AIResponseDiagnostics CreateDiagnostics(AITransportResult? result, TimeSpan latency, int attempts, AIEngineStatus? statusOverride = null, string? completionReasonOverride = null) => new(result?.ResponseId, result?.Model ?? options.ModelName, result?.Usage, latency, attempts, statusOverride ?? result?.Status ?? AIEngineStatus.ProviderFailure, completionReasonOverride ?? result?.CompletionReason);

    private static void ValidateRequest(IReadOnlyList<AIMessage> messages, string schemaName, BinaryData jsonSchema)
    {
        ArgumentNullException.ThrowIfNull(messages);
        ArgumentException.ThrowIfNullOrWhiteSpace(schemaName);
        ArgumentNullException.ThrowIfNull(jsonSchema);
        if (messages.Count == 0 || messages.Any(message => message is null || string.IsNullOrWhiteSpace(message.Content)))
        {
            throw new ArgumentException("At least one nonempty AI message is required.", nameof(messages));
        }
        if (schemaName.Length > 64 || schemaName.Any(character => !char.IsAsciiLetterOrDigit(character) && character is not '_' and not '-'))
        {
            throw new ArgumentException("Schema name must contain at most 64 ASCII letters, digits, underscores, or hyphens.", nameof(schemaName));
        }
        try
        {
            using var schemaDocument = JsonDocument.Parse(jsonSchema);
            if (schemaDocument.RootElement.ValueKind != JsonValueKind.Object)
            {
                throw new ArgumentException("JSON schema must contain an object at its root.", nameof(jsonSchema));
            }
        }
        catch (JsonException exception)
        {
            throw new ArgumentException("JSON schema is invalid JSON.", nameof(jsonSchema), exception);
        }
    }
}
