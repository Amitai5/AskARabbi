using System.ClientModel;
using System.Text.Json.Serialization;
using AskARabbiLIB.AI;
using AskARabbiLIB.AI.Tools;
using AskARabbiLIB.Calendar;
using Azure.Core;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenAI.Responses;

namespace AskARabbiLIB.Tests;

[TestClass]
public sealed class AzureOpenAIEngineTests
{
    [TestMethod]
    [TestCategory("Unit")]
    public void Constructor_ApiKeyCredential_DoesNotPerformNetworkWork()
    {
        // Arrange
        var credential = new ApiKeyCredential("test-api-key");

        // Act
        var engine = new AzureOpenAIEngine(CreateOptions(), credential);

        // Assert
        Assert.IsNotNull(engine);
    }

    [TestMethod]
    [TestCategory("Unit")]
    public void Constructor_EntraCredentialAndDefaultCredential_DoNotPerformNetworkWork()
    {
        // Act
        var explicitCredential = new AzureOpenAIEngine(CreateOptions(), new NoOpTokenCredential());
        var defaultCredential = new AzureOpenAIEngine(CreateOptions(), (TokenCredential?)null);

        // Assert
        Assert.IsNotNull(explicitCredential);
        Assert.IsNotNull(defaultCredential);
    }

    [TestMethod]
    [DataRow("http://example.test", "model")]
    [DataRow("https://example.test", "")]
    [TestCategory("Unit")]
    public void Validate_InvalidRequiredSettings_ThrowsArgumentException(string endpoint, string model)
    {
        // Arrange
        var options = CreateOptions() with { ProjectEndpoint = new Uri(endpoint), ModelName = model };

        // Act + Assert
        Assert.ThrowsExactly<ArgumentException>(options.Validate);
    }

    [TestMethod]
    [DataRow("timeout", 0)]
    [DataRow("timeout", 601)]
    [DataRow("maximumOutputTokens", 0)]
    [DataRow("maximumOutputTokens", 100_001)]
    [DataRow("maximumRetryCount", -1)]
    [DataRow("maximumRetryCount", 6)]
    [TestCategory("Unit")]
    public void Validate_OutOfRangeSetting_ThrowsArgumentOutOfRangeException(string setting, int value)
    {
        // Arrange
        var options = setting switch
        {
            "timeout" => CreateOptions() with { Timeout = TimeSpan.FromSeconds(value) },
            "maximumOutputTokens" => CreateOptions() with { MaximumOutputTokens = value },
            "maximumRetryCount" => CreateOptions() with { MaximumRetryCount = value },
            _ => throw new AssertFailedException($"Unknown setting '{setting}'."),
        };

        // Act and assert
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(options.Validate);
    }

    [TestMethod]
    [TestCategory("Unit")]
    public void Validate_NullOrRelativeEndpoint_ThrowsArgumentException()
    {
        // Arrange
        var nullEndpoint = CreateOptions() with { ProjectEndpoint = null! };
        var relativeEndpoint = CreateOptions() with { ProjectEndpoint = new Uri("relative", UriKind.Relative) };

        // Act and assert
        Assert.ThrowsExactly<ArgumentException>(nullEndpoint.Validate);
        Assert.ThrowsExactly<ArgumentException>(relativeEndpoint.Validate);
    }

    [TestMethod]
    [TestCategory("Unit")]
    public async Task GenerateStructuredAsync_Success_ReportsUsageAndPropagatesModel()
    {
        // Arrange
        var transport = new QueueTransport(new AITransportResult(AIEngineStatus.Success, "{\"answer\":\"grounded\"}", null, "resp-1", "returned-model", new AIUsage(10, 4, 14), false, "completed"));
        var engine = new AzureOpenAIEngine(CreateOptions(), transport, static (_, _) => Task.CompletedTask);

        // Act
        var result = await engine.GenerateStructuredAsync<TestResponse>([new AIMessage(AIMessageRole.User, "Question")], "test_schema", BinaryData.FromString("{\"type\":\"object\"}"));

        // Assert
        Assert.IsTrue(result.IsSuccess);
        Assert.IsNotNull(result.Value);
        Assert.AreEqual("grounded", result.Value.Answer);
        Assert.AreEqual("resp-1", result.Diagnostics.ResponseId);
        Assert.AreEqual("returned-model", result.Diagnostics.Model);
        Assert.AreEqual(14, result.Diagnostics.Usage?.TotalTokens);
        Assert.AreEqual(AIEngineStatus.Success, result.Diagnostics.ProviderStatus);
        Assert.AreEqual("completed", result.Diagnostics.CompletionReason);
        Assert.IsNotNull(transport.LastRequest);
        Assert.AreEqual("test-deployment", transport.LastRequest.Model);
    }

    [TestMethod]
    [TestCategory("Unit")]
    public async Task GenerateStructuredAsync_ToolSession_PropagatesSessionToTransport()
    {
        // Arrange
        var transport = new QueueTransport(new AITransportResult(AIEngineStatus.Success, "{\"answer\":\"grounded\"}", null, "resp-tool", "returned-model", null, false));
        var engine = new AzureOpenAIEngine(CreateOptions(), transport, static (_, _) => Task.CompletedTask);
        var session = CreateToolSession();

        // Act
        var result = await engine.GenerateStructuredAsync<TestResponse>([new AIMessage(AIMessageRole.User, "Question")], "test_schema", BinaryData.FromString("{\"type\":\"object\"}"), session);

        // Assert
        Assert.IsTrue(result.IsSuccess);
        Assert.IsNotNull(transport.LastRequest);
        Assert.AreSame(session, transport.LastRequest.ToolSession);
    }

    [TestMethod]
    [TestCategory("Unit")]
    public async Task GenerateStructuredAsync_RateLimited_RetriesAndReturnsSuccess()
    {
        // Arrange
        var transport = new QueueTransport(
            new AITransportResult(AIEngineStatus.RateLimited, null, "slow down", null, "test-deployment", null, true),
            new AITransportResult(AIEngineStatus.Success, "{\"answer\":\"after retry\"}", null, "resp-2", "test-deployment", new AIUsage(5, 2, 7), false));
        var delays = 0;
        var engine = new AzureOpenAIEngine(CreateOptions(), transport, (_, _) =>
        {
            delays++;
            return Task.CompletedTask;
        });

        // Act
        var result = await engine.GenerateStructuredAsync<TestResponse>([new AIMessage(AIMessageRole.User, "Question")], "test_schema", BinaryData.FromString("{\"type\":\"object\"}"));

        // Assert
        Assert.IsTrue(result.IsSuccess);
        Assert.AreEqual(2, transport.CallCount);
        Assert.AreEqual(1, delays);
        Assert.AreEqual(2, result.Diagnostics.Attempts);
    }

    [TestMethod]
    [TestCategory("Unit")]
    public async Task GenerateStructuredAsync_ProviderCancellationWithoutCallerCancellation_ReturnsTimeout()
    {
        // Arrange
        var transport = new CancelingTransport();
        var engine = new AzureOpenAIEngine(CreateOptions(), transport, static (_, _) => Task.CompletedTask);

        // Act
        var result = await engine.GenerateStructuredAsync<TestResponse>([new AIMessage(AIMessageRole.User, "Question")], "test_schema", BinaryData.FromString("{\"type\":\"object\"}"));

        // Assert
        Assert.AreEqual(AIEngineStatus.TimedOut, result.Status);
        Assert.IsFalse(result.IsSuccess);
    }

    [TestMethod]
    [TestCategory("Unit")]
    public async Task GenerateStructuredAsync_CallerCancellation_PropagatesOperationCanceledException()
    {
        // Arrange
        using var cancellationSource = new CancellationTokenSource();
        cancellationSource.Cancel();
        var engine = new AzureOpenAIEngine(CreateOptions(), new CancellationAwareTransport(), static (_, _) => Task.CompletedTask);

        // Act + Assert
        await Assert.ThrowsExactlyAsync<OperationCanceledException>(() => engine.GenerateStructuredAsync<TestResponse>([new AIMessage(AIMessageRole.User, "Question")], "test_schema", BinaryData.FromString("{\"type\":\"object\"}"), cancellationSource.Token));
    }

    [TestMethod]
    [TestCategory("Unit")]
    public async Task GenerateStructuredAsync_InvalidStructuredJson_ReturnsInvalidResponse()
    {
        // Arrange
        var transport = new QueueTransport(new AITransportResult(AIEngineStatus.Success, "{invalid", null, "resp-invalid", "test-deployment", null, false));
        var engine = new AzureOpenAIEngine(CreateOptions(), transport, static (_, _) => Task.CompletedTask);

        // Act
        var result = await engine.GenerateStructuredAsync<TestResponse>([new AIMessage(AIMessageRole.User, "Question")], "test_schema", BinaryData.FromString("{\"type\":\"object\"}"));

        // Assert
        Assert.AreEqual(AIEngineStatus.InvalidResponse, result.Status);
        Assert.IsNull(result.Value);
        StringAssert.Contains(result.ErrorMessage, "invalid structured JSON");
        Assert.AreEqual(AIEngineStatus.InvalidResponse, result.Diagnostics.ProviderStatus);
        Assert.AreEqual("invalid_structured_json", result.Diagnostics.CompletionReason);
    }

    [TestMethod]
    [TestCategory("Unit")]
    public async Task GenerateStructuredAsync_NonRetryableProviderFailure_ReturnsTypedFailure()
    {
        // Arrange
        var transport = new QueueTransport(new AITransportResult(AIEngineStatus.ProviderFailure, null, "provider unavailable", "provider-response", "test-deployment", new AIUsage(12, 3, 15), false, "ServerError"));
        var engine = new AzureOpenAIEngine(CreateOptions(), transport, static (_, _) => Task.CompletedTask);

        // Act
        var result = await engine.GenerateStructuredAsync<TestResponse>([new AIMessage(AIMessageRole.User, "Question")], "test_schema", BinaryData.FromString("{\"type\":\"object\"}"));

        // Assert
        Assert.AreEqual(AIEngineStatus.ProviderFailure, result.Status);
        Assert.AreEqual(1, transport.CallCount);
        Assert.AreEqual(1, result.Diagnostics.Attempts);
        Assert.AreEqual(AIEngineStatus.ProviderFailure, result.Diagnostics.ProviderStatus);
        Assert.AreEqual("ServerError", result.Diagnostics.CompletionReason);
        Assert.AreEqual("provider-response", result.Diagnostics.ResponseId);
        Assert.AreEqual(15, result.Diagnostics.Usage?.TotalTokens);
    }

    [TestMethod]
    [TestCategory("Unit")]
    public async Task GenerateStructuredAsync_RetryableFailureExhaustsRetries_UsesFallbackMessageAndModel()
    {
        // Arrange
        var retryable = new AITransportResult(AIEngineStatus.RateLimited, null, null, null, null!, null, true);
        var transport = new QueueTransport(retryable, retryable, retryable);
        var engine = new AzureOpenAIEngine(CreateOptions(), transport, static (_, _) => Task.CompletedTask);

        // Act
        var result = await engine.GenerateStructuredAsync<TestResponse>([new AIMessage(AIMessageRole.User, "Question")], "test_schema", BinaryData.FromString("{\"type\":\"object\"}"));

        // Assert
        Assert.AreEqual(AIEngineStatus.RateLimited, result.Status);
        Assert.AreEqual(3, result.Diagnostics.Attempts);
        Assert.AreEqual("test-deployment", result.Diagnostics.Model);
        StringAssert.Contains(result.ErrorMessage, "unspecified failure");
    }

    [TestMethod]
    [TestCategory("Unit")]
    public async Task GenerateStructuredAsync_DelayCancellationWithoutCallerCancellation_ReturnsTimeout()
    {
        // Arrange
        var transport = new QueueTransport(new AITransportResult(AIEngineStatus.RateLimited, null, "retry", null, "test-deployment", null, true));
        var engine = new AzureOpenAIEngine(CreateOptions(), transport, static (_, _) => throw new OperationCanceledException());

        // Act
        var result = await engine.GenerateStructuredAsync<TestResponse>([new AIMessage(AIMessageRole.User, "Question")], "test_schema", BinaryData.FromString("{\"type\":\"object\"}"));

        // Assert
        Assert.AreEqual(AIEngineStatus.TimedOut, result.Status);
        Assert.AreEqual(1, result.Diagnostics.Attempts);
    }

    [TestMethod]
    [TestCategory("Unit")]
    public async Task GenerateStructuredAsync_CallerCancellationDuringDelay_PropagatesOperationCanceledException()
    {
        // Arrange
        using var cancellationSource = new CancellationTokenSource();
        var transport = new QueueTransport(new AITransportResult(AIEngineStatus.RateLimited, null, "retry", null, "test-deployment", null, true));
        var engine = new AzureOpenAIEngine(CreateOptions(), transport, (_, _) =>
        {
            cancellationSource.Cancel();
            throw new OperationCanceledException(cancellationSource.Token);
        });

        // Act and assert
        await Assert.ThrowsExactlyAsync<OperationCanceledException>(() => engine.GenerateStructuredAsync<TestResponse>([new AIMessage(AIMessageRole.User, "Question")], "test_schema", BinaryData.FromString("{\"type\":\"object\"}"), cancellationSource.Token));
    }

    [TestMethod]
    [DataRow("")]
    [DataRow("bad schema")]
    [DataRow("*")]
    [TestCategory("Unit")]
    public async Task GenerateStructuredAsync_InvalidSchemaName_ThrowsArgumentException(string schemaName)
    {
        // Arrange
        var engine = new AzureOpenAIEngine(CreateOptions(), new QueueTransport(), static (_, _) => Task.CompletedTask);

        // Act and assert
        await Assert.ThrowsExactlyAsync<ArgumentException>(() => engine.GenerateStructuredAsync<TestResponse>([new AIMessage(AIMessageRole.User, "Question")], schemaName, BinaryData.FromString("{\"type\":\"object\"}")));
    }

    [TestMethod]
    [TestCategory("Unit")]
    public async Task GenerateStructuredAsync_InvalidMessageCollections_ThrowArgumentException()
    {
        // Arrange
        var engine = new AzureOpenAIEngine(CreateOptions(), new QueueTransport(), static (_, _) => Task.CompletedTask);
        var schema = BinaryData.FromString("{\"type\":\"object\"}");

        // Act and assert
        await Assert.ThrowsExactlyAsync<ArgumentNullException>(() => engine.GenerateStructuredAsync<TestResponse>(null!, "schema", schema));
        await Assert.ThrowsExactlyAsync<ArgumentException>(() => engine.GenerateStructuredAsync<TestResponse>([], "schema", schema));
        await Assert.ThrowsExactlyAsync<ArgumentException>(() => engine.GenerateStructuredAsync<TestResponse>([null!], "schema", schema));
        await Assert.ThrowsExactlyAsync<ArgumentException>(() => engine.GenerateStructuredAsync<TestResponse>([new AIMessage(AIMessageRole.User, " ")], "schema", schema));
    }

    [TestMethod]
    [TestCategory("Unit")]
    public async Task GenerateStructuredAsync_InvalidJsonSchemas_ThrowArgumentException()
    {
        // Arrange
        var engine = new AzureOpenAIEngine(CreateOptions(), new QueueTransport(), static (_, _) => Task.CompletedTask);
        var messages = new[] { new AIMessage(AIMessageRole.User, "Question") };

        // Act and assert
        await Assert.ThrowsExactlyAsync<ArgumentNullException>(() => engine.GenerateStructuredAsync<TestResponse>(messages, "schema", null!));
        await Assert.ThrowsExactlyAsync<ArgumentException>(() => engine.GenerateStructuredAsync<TestResponse>(messages, "schema", BinaryData.FromString("not-json")));
        await Assert.ThrowsExactlyAsync<ArgumentException>(() => engine.GenerateStructuredAsync<TestResponse>(messages, "schema", BinaryData.FromString("[]")));
    }

    [TestMethod]
    [TestCategory("Unit")]
    public async Task GenerateStructuredAsync_EmptyAndNullStructuredOutputs_ReturnInvalidResponse()
    {
        // Arrange
        var transport = new QueueTransport(
            new AITransportResult(AIEngineStatus.Success, " ", null, null, "test-deployment", null, false),
            new AITransportResult(AIEngineStatus.Success, "null", null, null, "test-deployment", null, false));
        var engine = new AzureOpenAIEngine(CreateOptions(), transport, static (_, _) => Task.CompletedTask);

        // Act
        var empty = await engine.GenerateStructuredAsync<TestResponse>([new AIMessage(AIMessageRole.User, "Question")], "schema", BinaryData.FromString("{\"type\":\"object\"}"));
        var nullValue = await engine.GenerateStructuredAsync<TestResponse>([new AIMessage(AIMessageRole.User, "Question")], "schema", BinaryData.FromString("{\"type\":\"object\"}"));

        // Assert
        Assert.AreEqual(AIEngineStatus.InvalidResponse, empty.Status);
        StringAssert.Contains(empty.ErrorMessage, "no structured output");
        Assert.AreEqual(AIEngineStatus.InvalidResponse, nullValue.Status);
        StringAssert.Contains(nullValue.ErrorMessage, "null structured output");
    }

    [TestMethod]
    [TestCategory("Unit")]
    public async Task GenerateStructuredAsync_OverlongSchemaName_ThrowsArgumentException()
    {
        // Arrange
        var engine = new AzureOpenAIEngine(CreateOptions(), new QueueTransport(), static (_, _) => Task.CompletedTask);

        // Act + Assert
        await Assert.ThrowsExactlyAsync<ArgumentException>(() => engine.GenerateStructuredAsync<TestResponse>([new AIMessage(AIMessageRole.User, "Question")], new string('a', 65), BinaryData.FromString("{\"type\":\"object\"}")));
    }

    [TestMethod]
    [TestCategory("Unit")]
    public void CreateOptions_ConfiguresExplicitModelStoreFalseAndStructuredOutput()
    {
        // Arrange
        var request = new AITransportRequest([new AIMessage(AIMessageRole.System, "Contract")], "grounded", BinaryData.FromString("{\"type\":\"object\"}"), "deployment-name", 2000, AIReasoningEffort.Medium);

        // Act
        var options = AzureResponsesTransport.CreateOptions(request);

        // Assert
        Assert.AreEqual("deployment-name", options.Model);
        Assert.AreEqual(false, options.StoredOutputEnabled);
        Assert.AreEqual(2000, options.MaxOutputTokenCount);
        Assert.AreEqual(ResponseReasoningEffortLevel.Medium, options.ReasoningOptions?.ReasoningEffortLevel);
        Assert.IsNotNull(options.TextOptions?.TextFormat);
        Assert.HasCount(1, options.InputItems);
    }

    [TestMethod]
    [TestCategory("Unit")]
    public void CreateOptions_ToolSession_AddsBoundedNonParallelFunctionTools()
    {
        // Arrange
        var session = CreateToolSession();
        var request = new AITransportRequest([new AIMessage(AIMessageRole.System, "Contract")], "grounded", BinaryData.FromString("{\"type\":\"object\"}"), "deployment-name", 2000, AIReasoningEffort.Medium, session);

        // Act
        var options = AzureResponsesTransport.CreateOptions(request);

        // Assert
        Assert.HasCount(3, options.Tools);
        Assert.AreEqual(session.MaximumExecutionCount, options.MaxToolCallCount);
        Assert.AreEqual(false, options.ParallelToolCallsEnabled);
    }

    [TestMethod]
    [TestCategory("Unit")]
    public void AppendResponseOutputItems_ToolContinuation_PreservesEveryProviderItemInOrder()
    {
        // Arrange
        var options = new CreateResponseOptions();
        var reasoningState = MessageResponseItem.CreateAssistantMessageItem("Provider reasoning state");
        var functionCall = new FunctionCallResponseItem("call-1", "get_today_as_hebrew_and_gregorian", BinaryData.FromString("{}"));

        // Act
        AzureResponsesTransport.AppendResponseOutputItems(options, [reasoningState, functionCall]);

        // Assert
        Assert.HasCount(2, options.InputItems);
        Assert.AreSame(reasoningState, options.InputItems[0]);
        Assert.AreSame(functionCall, options.InputItems[1]);
    }

    [TestMethod]
    [TestCategory("Unit")]
    public void GetCompletionReason_AllProviderStates_ReturnsSafeDiagnosticCategory()
    {
        // Arrange
        var completed = ResponseStatus.Completed.ToString();
        var incomplete = ResponseStatus.Incomplete.ToString();
        var failed = ResponseStatus.Failed.ToString();

        // Act and assert
        Assert.AreEqual("completed", AzureResponsesTransport.GetCompletionReason(completed, null, null));
        Assert.AreEqual("MaxOutputTokens", AzureResponsesTransport.GetCompletionReason(incomplete, "MaxOutputTokens", null));
        Assert.AreEqual("incomplete", AzureResponsesTransport.GetCompletionReason(incomplete, null, null));
        Assert.AreEqual("ServerError", AzureResponsesTransport.GetCompletionReason(failed, null, "ServerError"));
        Assert.AreEqual("failed", AzureResponsesTransport.GetCompletionReason(failed, null, null));
        Assert.AreEqual("queued", AzureResponsesTransport.GetCompletionReason("queued", null, null));
        Assert.AreEqual("unknown", AzureResponsesTransport.GetCompletionReason(null, null, null));
    }

    [TestMethod]
    [TestCategory("Unit")]
    public void GetContentFilterCompletionReason_BlockedCompletion_ReturnsOnlySafeCategoryMetadata()
    {
        // Arrange
        var response = BinaryData.FromString("""
            {
              "content_filters": [
                {
                  "blocked": false,
                  "source_type": "prompt",
                  "content_filter_results": {
                    "violence": { "filtered": false, "severity": "safe" }
                  }
                },
                {
                  "blocked": true,
                  "source_type": "completion",
                  "content_filter_results": {
                    "violence": { "filtered": true, "severity": "high" },
                    "protected_material_text": { "detected": true, "filtered": true },
                    "Phone Number Protection": { "detected": true, "filtered": true },
                    "email_protection": { "detected": true, "filtered": true },
                    "ip-address": { "detected": true, "filtered": true },
                    "untrusted_custom_category": { "filtered": true, "details": "must not appear" }
                  }
                }
              ]
            }
            """);

        // Act
        var reason = AzureResponsesTransport.GetContentFilterCompletionReason(response);

        // Assert
        Assert.AreEqual("content_filter.completion.email_address.ip_address.phone_number.protected_material_text.violence_high", reason);
        Assert.IsFalse(reason.Contains("untrusted", StringComparison.Ordinal));
    }

    [TestMethod]
    [TestCategory("Unit")]
    public void GetContentFilterCompletionReason_MalformedOrMissingMetadata_ReturnsFallback()
    {
        // Act
        var malformed = AzureResponsesTransport.GetContentFilterCompletionReason(BinaryData.FromString("not-json"), "content_filter");
        var missing = AzureResponsesTransport.GetContentFilterCompletionReason(BinaryData.FromString("{}"), "content_filter");

        // Assert
        Assert.AreEqual("content_filter", malformed);
        Assert.AreEqual("content_filter", missing);
    }

    [TestMethod]
    [DataRow(AIReasoningEffort.Low)]
    [DataRow(AIReasoningEffort.Medium)]
    [DataRow(AIReasoningEffort.High)]
    [TestCategory("Unit")]
    public void CreateOptions_AllReasoningEffortsAndMessageRoles_MapCorrectly(AIReasoningEffort effort)
    {
        // Arrange
        var expected = effort switch
        {
            AIReasoningEffort.Low => ResponseReasoningEffortLevel.Low,
            AIReasoningEffort.Medium => ResponseReasoningEffortLevel.Medium,
            AIReasoningEffort.High => ResponseReasoningEffortLevel.High,
            _ => throw new AssertFailedException($"Unknown reasoning effort '{effort}'."),
        };
        var messages = new[]
        {
            new AIMessage(AIMessageRole.System, "System"),
            new AIMessage(AIMessageRole.User, "User"),
            new AIMessage(AIMessageRole.Assistant, "Assistant"),
        };
        var request = new AITransportRequest(messages, "grounded", BinaryData.FromString("{\"type\":\"object\"}"), "deployment", 100, effort);

        // Act
        var options = AzureResponsesTransport.CreateOptions(request);

        // Assert
        Assert.AreEqual(expected, options.ReasoningOptions?.ReasoningEffortLevel);
        Assert.HasCount(3, options.InputItems);
    }

    [TestMethod]
    [TestCategory("Unit")]
    public void CreateOptions_UnknownReasoningEffortOrMessageRole_ThrowsArgumentOutOfRangeException()
    {
        // Arrange
        var schema = BinaryData.FromString("{\"type\":\"object\"}");
        var invalidEffort = new AITransportRequest([new AIMessage(AIMessageRole.User, "Question")], "schema", schema, "deployment", 100, (AIReasoningEffort)999);
        var invalidRole = new AITransportRequest([new AIMessage((AIMessageRole)999, "Question")], "schema", schema, "deployment", 100, AIReasoningEffort.Low);

        // Act and assert
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => AzureResponsesTransport.CreateOptions(invalidEffort));
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => AzureResponsesTransport.CreateOptions(invalidRole));
    }

    [TestMethod]
    [TestCategory("Unit")]
    public void AIEngineResult_InvalidFactoryArguments_ThrowPreciseExceptions()
    {
        // Arrange
        var diagnostics = new AIResponseDiagnostics(null, "model", null, TimeSpan.Zero, 1);

        // Act and assert
        Assert.ThrowsExactly<ArgumentNullException>(() => AIEngineResult<TestResponse>.Success(null!, diagnostics));
        Assert.ThrowsExactly<ArgumentNullException>(() => AIEngineResult<TestResponse>.Success(new TestResponse { Answer = "answer" }, null!));
        Assert.ThrowsExactly<ArgumentException>(() => AIEngineResult<TestResponse>.Failure(AIEngineStatus.Success, "failure", diagnostics));
        Assert.ThrowsExactly<ArgumentException>(() => AIEngineResult<TestResponse>.Failure(AIEngineStatus.ProviderFailure, " ", diagnostics));
        Assert.ThrowsExactly<ArgumentNullException>(() => AIEngineResult<TestResponse>.Failure(AIEngineStatus.ProviderFailure, "failure", null!));
    }

    private static AIEngineOptions CreateOptions() => new()
    {
        ProjectEndpoint = new Uri("https://example.openai.azure.com"),
        ModelName = "test-deployment",
        Timeout = TimeSpan.FromSeconds(120),
        MaximumOutputTokens = 2000,
        MaximumRetryCount = 2,
    };

    private static AIToolExecutionSession CreateToolSession()
    {
        var registry = new AIToolRegistry([new CalendarAITools(new HebrewCalendarService())]);
        return new AIToolExecutionSession(registry, new AIToolExecutionContext(null, new DateTimeOffset(2026, 8, 31, 18, 0, 0, TimeSpan.Zero)), 0);
    }

    private sealed record TestResponse
    {
        [JsonPropertyName("answer")]
        public required string Answer { get; init; }
    }

    private sealed class QueueTransport : IAIResponseTransport
    {
        private readonly Queue<AITransportResult> results;

        internal QueueTransport(params AITransportResult[] results)
        {
            this.results = new Queue<AITransportResult>(results);
        }

        internal AITransportRequest? LastRequest { get; private set; }

        internal int CallCount { get; private set; }

        public Task<AITransportResult> SendAsync(AITransportRequest request, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            LastRequest = request;
            CallCount++;
            return Task.FromResult(results.Dequeue());
        }
    }

    private sealed class CancelingTransport : IAIResponseTransport
    {
        public Task<AITransportResult> SendAsync(AITransportRequest request, CancellationToken cancellationToken) => throw new OperationCanceledException();
    }

    private sealed class CancellationAwareTransport : IAIResponseTransport
    {
        public Task<AITransportResult> SendAsync(AITransportRequest request, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            throw new AssertFailedException("Expected cancellation before transport work.");
        }
    }

    private sealed class NoOpTokenCredential : TokenCredential
    {
        public override AccessToken GetToken(TokenRequestContext requestContext, CancellationToken cancellationToken) => new("token", DateTimeOffset.MaxValue);

        public override ValueTask<AccessToken> GetTokenAsync(TokenRequestContext requestContext, CancellationToken cancellationToken) => ValueTask.FromResult(new AccessToken("token", DateTimeOffset.MaxValue));
    }
}
