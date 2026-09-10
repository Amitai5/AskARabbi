using System.ClientModel;
using System.ClientModel.Primitives;
using System.Net;
using System.Text;
using AskARabbiLIB.AI;
using AskARabbiLIB.Usage;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenAI;
using OpenAI.Responses;

namespace AskARabbiLIB.Tests;

[TestClass]
public sealed class AzureResponsesUsageTests
{
    [TestMethod]
    [DataRow("completed", AIEngineStatus.Success)]
    [DataRow("incomplete", AIEngineStatus.InvalidResponse)]
    [TestCategory("Regression")]
    public async Task SendAsync_ProviderReturnsUsage_AccountsBeforeInterpretingResponse(string status, AIEngineStatus expectedStatus)
    {
        var observer = new UsageObserver();
        var handler = new ResponseHandler(status);
        using var httpClient = new HttpClient(handler);
        var transport = CreateTransport(httpClient, observer);

        var result = await transport.SendAsync(CreateRequest(), CancellationToken.None);

        Assert.AreEqual(expectedStatus, result.Status, result.ErrorMessage);
        Assert.AreEqual(1, observer.Checks);
        Assert.AreEqual("resp_test", observer.ResponseId);
        Assert.AreEqual(new AIUsage(100, 200, 300), observer.Usage);
        Assert.AreEqual(1, handler.Requests);
    }

    [TestMethod]
    [TestCategory("Regression")]
    public async Task SendAsync_ExhaustedAllowance_RejectsBeforeNetworkWithoutConvertingToProviderFailure()
    {
        var observer = new UsageObserver { RejectBeforeRequest = true };
        var handler = new ResponseHandler("completed");
        using var httpClient = new HttpClient(handler);

        var exception = await Assert.ThrowsExactlyAsync<ChatUsageException>(() => CreateTransport(httpClient, observer).SendAsync(CreateRequest(), CancellationToken.None));

        Assert.AreEqual("usage_limit_reached", exception.Code);
        Assert.AreEqual(0, handler.Requests);
    }

    [TestMethod]
    [TestCategory("Regression")]
    public async Task SendAsync_AccountingFailure_StopsWithoutReturningUnchargedAnswer()
    {
        var observer = new UsageObserver { RejectAccounting = true };
        var handler = new ResponseHandler("completed");
        using var httpClient = new HttpClient(handler);

        var exception = await Assert.ThrowsExactlyAsync<ChatUsageException>(() => CreateTransport(httpClient, observer).SendAsync(CreateRequest(), CancellationToken.None));

        Assert.AreEqual("usage_unavailable", exception.Code);
        Assert.AreEqual(1, handler.Requests);
    }

    private static AzureResponsesTransport CreateTransport(HttpClient httpClient, IAIUsageObserver observer) => new(new ResponsesClient(new ApiKeyCredential("fake-test-key"), new OpenAIClientOptions
    {
        Endpoint = new Uri("https://unit-test.invalid/v1"),
        Transport = new HttpClientPipelineTransport(httpClient),
        RetryPolicy = new ClientRetryPolicy(0),
    }), observer);

    private static AITransportRequest CreateRequest() => new([new(AIMessageRole.User, "Explain Shabbat")], "test", BinaryData.FromString("""{"type":"object","properties":{},"additionalProperties":false}"""), "test-model", 1000, AIReasoningEffort.Medium);

    private sealed class UsageObserver : IAIUsageObserver
    {
        internal int Checks { get; private set; }
        internal string? ResponseId { get; private set; }
        internal AIUsage? Usage { get; private set; }
        internal bool RejectBeforeRequest { get; init; }
        internal bool RejectAccounting { get; init; }

        public Task BeforeRequestAsync(CancellationToken cancellationToken = default)
        {
            Checks++;
            if (RejectBeforeRequest)
            {
                throw new ChatUsageException("usage_limit_reached", "Monthly limit reached.");
            }
            return Task.CompletedTask;
        }

        public Task RecordAsync(string? responseId, AIUsage usage)
        {
            if (RejectAccounting)
            {
                throw new ChatUsageException("usage_unavailable", "Accounting unavailable.");
            }
            ResponseId = responseId;
            Usage = usage;
            return Task.CompletedTask;
        }
    }

    private sealed class ResponseHandler(string status) : HttpMessageHandler
    {
        internal int Requests { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Requests++;
            var json = $$$"""
                {"id":"resp_test","object":"response","created_at":1785542400,"status":"{{{status}}}","model":"test-model",
                 "output":[{"id":"msg_test","type":"message","role":"assistant","status":"completed","content":[{"type":"output_text","text":"{}","annotations":[]}]}],
                 "usage":{"input_tokens":100,"input_tokens_details":{"cached_tokens":20},"output_tokens":200,"output_tokens_details":{"reasoning_tokens":150},"total_tokens":300}}
                """;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(json, Encoding.UTF8, "application/json") });
        }
    }
}
