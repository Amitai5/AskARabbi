using System.Net;
using System.Net.Http.Headers;
using System.Text;
using AskARabbiLIB.Models;
using AskARabbiLIB.Retrieval;
using Azure.Core;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace AskARabbiLIB.Tests;

[TestClass]
public sealed class AzureOpenAIVectorStoreClientTests
{
    [TestMethod]
    [DataRow("relative-endpoint")]
    [DataRow("http-endpoint")]
    [DataRow("zero-timeout")]
    [DataRow("long-timeout")]
    [DataRow("empty-model")]
    [TestCategory("Unit")]
    public void Validate_InvalidClientOptions_Throws(string scenario)
    {
        var options = scenario switch
        {
            "relative-endpoint" => new AzureOpenAIVectorStoreClientOptions { ProjectEndpoint = new Uri("relative", UriKind.Relative) },
            "http-endpoint" => new AzureOpenAIVectorStoreClientOptions { ProjectEndpoint = new Uri("http://openai.example.test/") },
            "zero-timeout" => new AzureOpenAIVectorStoreClientOptions { ProjectEndpoint = new Uri("https://openai.example.test/"), Timeout = TimeSpan.Zero },
            "long-timeout" => new AzureOpenAIVectorStoreClientOptions { ProjectEndpoint = new Uri("https://openai.example.test/"), Timeout = TimeSpan.FromMinutes(11) },
            "empty-model" => new AzureOpenAIVectorStoreClientOptions { ProjectEndpoint = new Uri("https://openai.example.test/"), ModelName = " " },
            _ => throw new AssertFailedException($"Unknown scenario '{scenario}'."),
        };

        if (scenario is "relative-endpoint" or "http-endpoint" or "empty-model")
        {
            Assert.ThrowsExactly<ArgumentException>(options.Validate);
        }
        else
        {
            Assert.ThrowsExactly<ArgumentOutOfRangeException>(options.Validate);
        }
    }

    [TestMethod]
    [TestCategory("Unit")]
    public async Task GetAndSearchAsync_ValidResponses_ParsesContractAndSendsBearerToken()
    {
        var handler = new QueueHandler(
            _ => Json(HttpStatusCode.OK, StoreJson("vs_test", new string('a', 64))),
            _ => Json(HttpStatusCode.OK, """{"status":"incomplete","output":[{"type":"reasoning"},{"type":"file_search_call","status":"completed","results":[{"file_id":"file_1","filename":"source.md","score":0.75,"attributes":{"language":"English"},"text":"result text"}]},{"type":"message"}]}"""));
        using var httpClient = new HttpClient(handler);
        var client = CreateClient(httpClient);

        var store = await client.GetAsync("vs_test");
        var page = await client.SearchAsync("vs_test", new AzureOpenAIVectorStoreSearchRequest { Queries = ["question"], Languages = ["English"], MaximumResults = 5, ScoreThreshold = 0.2 });

        Assert.AreEqual("vs_test", store.Id);
        Assert.AreEqual(1, store.CompletedFileCount);
        Assert.HasCount(1, page.Results);
        Assert.AreEqual("result text", page.Results[0].Content[0]);
        Assert.IsFalse(page.HasMore);
        Assert.IsTrue(handler.Requests.All(request => request.Authorization == "Bearer test-token"));
        Assert.AreEqual("/openai/v1/responses", handler.Requests[1].Uri.AbsolutePath);
        Assert.AreEqual("?api-version=v1", handler.Requests[1].Uri.Query);
        StringAssert.Contains(handler.Requests[1].Body, "\"model\":\"search-model\"");
        StringAssert.Contains(handler.Requests[1].Body, "\"type\":\"file_search\"");
        StringAssert.Contains(handler.Requests[1].Body, "\"store\":false");
        StringAssert.Contains(handler.Requests[1].Body, "vs_test");
        StringAssert.Contains(handler.Requests[1].Body, "score_threshold");
        StringAssert.Contains(handler.Requests[1].Body, "rankingHints");
        StringAssert.Contains(handler.Requests[1].Body, "English");
        Assert.IsFalse(handler.Requests[1].Body.Contains("\"filters\"", StringComparison.Ordinal));
    }

    [TestMethod]
    [TestCategory("Regression")]
    public async Task SearchAsync_MissingIncludedResultsOnce_RetriesAndParsesSecondResponse()
    {
        var handler = new QueueHandler(
            _ => Json(HttpStatusCode.OK, """{"id":"resp_missing","status":"incomplete","output":[{"type":"file_search_call","status":"completed"}]}"""),
            _ => Json(HttpStatusCode.OK, """{"id":"resp_complete","status":"completed","output":[{"type":"file_search_call","status":"completed","results":[{"file_id":"file_1","filename":"source.md","score":0.75,"attributes":{},"text":"result text"}]}]}"""));
        using var httpClient = new HttpClient(handler);
        var delays = new List<TimeSpan>();
        var client = CreateClient(httpClient, (delay, cancellationToken) =>
        {
            cancellationToken.ThrowIfCancellationRequested();
            delays.Add(delay);
            return Task.CompletedTask;
        });

        var page = await client.SearchAsync("vs_test", new AzureOpenAIVectorStoreSearchRequest { Queries = ["question"] });

        Assert.HasCount(1, page.Results);
        Assert.AreEqual("result text", page.Results[0].Content[0]);
        Assert.HasCount(2, handler.Requests);
        CollectionAssert.AreEqual(new[] { TimeSpan.FromMilliseconds(250) }, delays);
    }

    [TestMethod]
    [TestCategory("Unit")]
    public async Task SearchAsync_MissingIncludedResultsAfterMaximumAttempts_ThrowsDiagnosticInvalidData()
    {
        const string response = """{"id":"resp_missing","status":"incomplete","output":[{"type":"file_search_call","status":"completed"}]}""";
        var handler = new QueueHandler(
            _ => Json(HttpStatusCode.OK, response),
            _ => Json(HttpStatusCode.OK, response),
            _ => Json(HttpStatusCode.OK, response));
        using var httpClient = new HttpClient(handler);
        var delays = new List<TimeSpan>();
        var client = CreateClient(httpClient, (delay, _) =>
        {
            delays.Add(delay);
            return Task.CompletedTask;
        });

        var exception = await Assert.ThrowsAsync<InvalidDataException>(() => client.SearchAsync("vs_test", new AzureOpenAIVectorStoreSearchRequest { Queries = ["question"] }));

        StringAssert.Contains(exception.Message, "resp_missing");
        StringAssert.Contains(exception.Message, "incomplete");
        Assert.HasCount(3, handler.Requests);
        CollectionAssert.AreEqual(new[] { TimeSpan.FromMilliseconds(250), TimeSpan.FromMilliseconds(500) }, delays);
    }

    [TestMethod]
    [TestCategory("Unit")]
    public async Task SearchAsync_MissingIncludedResultsWithoutResponseMetadata_UsesSafeDiagnosticDefaults()
    {
        const string response = """{"output":[{"type":"file_search_call"}]}""";
        var handler = new QueueHandler(
            _ => Json(HttpStatusCode.OK, response),
            _ => Json(HttpStatusCode.OK, response),
            _ => Json(HttpStatusCode.OK, response));
        using var httpClient = new HttpClient(handler);
        var client = CreateClient(httpClient, (_, _) => Task.CompletedTask);

        var exception = await Assert.ThrowsAsync<InvalidDataException>(() => client.SearchAsync("vs_test", new AzureOpenAIVectorStoreSearchRequest { Queries = ["question"] }));

        StringAssert.Contains(exception.Message, "Response ID: 'unknown'");
        StringAssert.Contains(exception.Message, "response status: 'unknown'");
        StringAssert.Contains(exception.Message, "call status: 'unknown'");
        Assert.HasCount(3, handler.Requests);
    }

    [TestMethod]
    [TestCategory("Unit")]
    public async Task GetAsync_ProviderError_ThrowsWithStatusAndBoundedDetail()
    {
        var handler = new QueueHandler(_ => Json(HttpStatusCode.Forbidden, "{\"error\":\"denied\"}"));
        using var httpClient = new HttpClient(handler);
        var client = CreateClient(httpClient);

        var exception = await Assert.ThrowsExactlyAsync<HttpRequestException>(() => client.GetAsync("vs_test"));

        Assert.AreEqual(HttpStatusCode.Forbidden, exception.StatusCode);
        StringAssert.Contains(exception.Message, "GET /openai/v1/vector_stores/vs_test");
        StringAssert.Contains(exception.Message, "denied");
    }

    [TestMethod]
    [TestCategory("Unit")]
    public async Task GetAsync_InvalidJson_ThrowsInvalidData()
    {
        var handler = new QueueHandler(_ => Json(HttpStatusCode.OK, "not-json"));
        using var httpClient = new HttpClient(handler);
        var client = CreateClient(httpClient);

        await Assert.ThrowsExactlyAsync<InvalidDataException>(() => client.GetAsync("vs_test"));
    }

    [TestMethod]
    [DataRow("empty-query")]
    [DataRow("maximum")]
    [DataRow("threshold")]
    [DataRow("bad-source")]
    [TestCategory("Unit")]
    public async Task SearchAsync_InvalidRequest_ThrowsBeforeNetwork(string scenario)
    {
        var handler = new QueueHandler();
        using var httpClient = new HttpClient(handler);
        var client = CreateClient(httpClient);
        var request = scenario switch
        {
            "empty-query" => new AzureOpenAIVectorStoreSearchRequest { Queries = [] },
            "maximum" => new AzureOpenAIVectorStoreSearchRequest { Queries = ["question"], MaximumResults = 51 },
            "threshold" => new AzureOpenAIVectorStoreSearchRequest { Queries = ["question"], ScoreThreshold = -0.1 },
            "bad-source" => new AzureOpenAIVectorStoreSearchRequest { Queries = ["question"], SourceKeys = ["bad"] },
            _ => throw new AssertFailedException($"Unknown scenario '{scenario}'."),
        };

        if (scenario is "maximum" or "threshold")
        {
            await Assert.ThrowsExactlyAsync<ArgumentOutOfRangeException>(() => client.SearchAsync("vs_test", request));
        }
        else
        {
            await Assert.ThrowsExactlyAsync<ArgumentException>(() => client.SearchAsync("vs_test", request));
        }

        Assert.HasCount(0, handler.Requests);
    }

    [TestMethod]
    [TestCategory("Unit")]
    public async Task SearchAsync_MissingModel_ThrowsBeforeNetwork()
    {
        var handler = new QueueHandler();
        using var httpClient = new HttpClient(handler);
        var client = new AzureOpenAIVectorStoreClient(new AzureOpenAIVectorStoreClientOptions { ProjectEndpoint = new Uri("https://openai.example.test/") }, new FakeTokenCredential(), httpClient);

        await Assert.ThrowsExactlyAsync<InvalidOperationException>(() => client.SearchAsync("vs_test", new AzureOpenAIVectorStoreSearchRequest { Queries = ["question"] }));

        Assert.HasCount(0, handler.Requests);
    }

    [TestMethod]
    [DataRow(HttpStatusCode.InternalServerError)]
    [DataRow(HttpStatusCode.Unauthorized)]
    [TestCategory("Unit")]
    public async Task UploadFileAsync_TransientFailure_RetriesWithFreshRequest(HttpStatusCode statusCode)
    {
        var handler = new QueueHandler(
            _ => Json(statusCode, "{\"error\":\"temporary\"}"),
            _ => Json(HttpStatusCode.OK, "{\"id\":\"file_retry\"}"));
        using var httpClient = new HttpClient(handler);
        var delays = new List<TimeSpan>();
        var client = new AzureOpenAIVectorStoreClient(new AzureOpenAIVectorStoreClientOptions
        {
            ProjectEndpoint = new Uri("https://openai.example.test/"),
            ModelName = "search-model",
        }, new FakeTokenCredential(), httpClient, (delay, cancellationToken) =>
        {
            cancellationToken.ThrowIfCancellationRequested();
            delays.Add(delay);
            return Task.CompletedTask;
        });
        var document = new AzureOpenAIVectorStoreCorpusDocument("source.md", Encoding.UTF8.GetBytes("source"), new Dictionary<string, string>(), 1, 1);

        var fileId = await client.UploadFileAsync(document, CancellationToken.None);

        Assert.AreEqual("file_retry", fileId);
        Assert.HasCount(2, handler.Requests);
        CollectionAssert.AreEqual(new[] { TimeSpan.FromSeconds(1) }, delays);
    }

    [TestMethod]
    [TestCategory("Unit")]
    public async Task AttachFileAsync_UnauthorizedOnce_RetriesWithFreshRequest()
    {
        var handler = new QueueHandler(
            _ => Json(HttpStatusCode.Unauthorized, "{\"error\":\"expired token\"}"),
            _ => Json(HttpStatusCode.OK, "{\"id\":\"file_attached\"}"));
        using var httpClient = new HttpClient(handler);
        var delays = new List<TimeSpan>();
        var client = new AzureOpenAIVectorStoreClient(new AzureOpenAIVectorStoreClientOptions
        {
            ProjectEndpoint = new Uri("https://openai.example.test/"),
            ModelName = "search-model",
        }, new FakeTokenCredential(), httpClient, (delay, cancellationToken) =>
        {
            cancellationToken.ThrowIfCancellationRequested();
            delays.Add(delay);
            return Task.CompletedTask;
        });
        var file = new AzureOpenAIVectorStoreUploadedFile("file_1", new Dictionary<string, string> { ["language"] = "English" });

        await client.AttachFileAsync("vs_test", file, CancellationToken.None);

        Assert.HasCount(2, handler.Requests);
        Assert.IsTrue(handler.Requests.All(request => request.Body.Contains("file_1", StringComparison.Ordinal)));
        CollectionAssert.AreEqual(new[] { TimeSpan.FromSeconds(1) }, delays);
    }

    [TestMethod]
    [TestCategory("Unit")]
    public async Task ListStoreFilesAsync_PaginatedResponse_ReturnsEveryEntry()
    {
        var handler = new QueueHandler(
            _ => Json(HttpStatusCode.OK, "{\"data\":[{\"id\":\"file_1\",\"status\":\"completed\"}],\"has_more\":true,\"last_id\":\"file_1\"}"),
            _ => Json(HttpStatusCode.OK, "{\"data\":[{\"id\":\"file_2\",\"status\":\"failed\"}],\"has_more\":false,\"last_id\":\"file_2\"}"));
        using var httpClient = new HttpClient(handler);

        var files = await CreateClient(httpClient).ListStoreFilesAsync("vs_test", CancellationToken.None);

        Assert.HasCount(2, files);
        Assert.AreEqual("file_1", files[0].FileId);
        Assert.AreEqual("failed", files[1].Status);
        StringAssert.Contains(handler.Requests[1].Uri.Query, "after=file_1");
    }

    [TestMethod]
    [TestCategory("Unit")]
    public async Task ListUploadedFileNamesAsync_OverlappingPages_DeduplicatesIdenticalEntry()
    {
        var handler = new QueueHandler(
            _ => Json(HttpStatusCode.OK, "{\"data\":[{\"id\":\"file_1\",\"filename\":\"source.md\"}],\"has_more\":true,\"last_id\":\"file_1\"}"),
            _ => Json(HttpStatusCode.OK, "{\"data\":[{\"id\":\"file_1\",\"filename\":\"source.md\"},{\"id\":\"file_2\",\"filename\":\"second.md\"}],\"has_more\":false,\"last_id\":\"file_2\"}"));
        using var httpClient = new HttpClient(handler);

        var files = await CreateClient(httpClient).ListUploadedFileNamesAsync(CancellationToken.None);

        Assert.HasCount(2, files);
        Assert.AreEqual("source.md", files["file_1"]);
        Assert.AreEqual("second.md", files["file_2"]);
    }

    [TestMethod]
    [TestCategory("Unit")]
    public async Task ListUploadedFileNamesAsync_ConflictingDuplicateId_FailsClosed()
    {
        var handler = new QueueHandler(_ => Json(HttpStatusCode.OK, "{\"data\":[{\"id\":\"file_1\",\"filename\":\"source.md\"},{\"id\":\"file_1\",\"filename\":\"other.md\"}],\"has_more\":false,\"last_id\":\"file_1\"}"));
        using var httpClient = new HttpClient(handler);

        var exception = await Assert.ThrowsExactlyAsync<InvalidDataException>(() => CreateClient(httpClient).ListUploadedFileNamesAsync(CancellationToken.None));

        StringAssert.Contains(exception.Message, "conflicting filenames");
    }

    [TestMethod]
    [TestCategory("Unit")]
    public async Task ResumeAsync_FailedAndMissingFiles_ReplacesOnlyMissingContentAndVerifiesStore()
    {
        var firstDocument = TestManifestFactory.CreateDocument(segmentCount: 1, firstReference: "Genesis 1:1", lastReference: "Genesis 1:1");
        var secondDocument = TestManifestFactory.CreateDocument(segmentCount: 1, firstReference: "Genesis 1:1", lastReference: "Genesis 1:1", rawSha256: new string('b', 64));
        var manifest = TestManifestFactory.CreateManifest(firstDocument, secondDocument);
        var fingerprint = SourceIndexBuilder.ComputeCorpusFingerprint(manifest);
        var handler = new QueueHandler(
            _ => Json(HttpStatusCode.OK, StoreJson("vs_existing", fingerprint, "completed", 1, 1, documentCount: 2, fileCount: 2, segmentCount: 2)),
            _ => Json(HttpStatusCode.OK, "{\"data\":[{\"id\":\"file_good\",\"status\":\"completed\"},{\"id\":\"file_bad\",\"status\":\"failed\"}],\"has_more\":false,\"last_id\":\"file_bad\"}"),
            _ => Json(HttpStatusCode.OK, "{\"data\":[{\"id\":\"file_good\",\"filename\":\"sefaria-aaaaaaaaaaaaaaaa.md\"},{\"id\":\"file_bad\",\"filename\":\"sefaria-bbbbbbbbbbbbbbbb.md\"}],\"has_more\":false,\"last_id\":\"file_bad\"}"),
            _ => Json(HttpStatusCode.OK, "{\"id\":\"file_bad\",\"deleted\":true}"),
            _ => Json(HttpStatusCode.OK, "{\"id\":\"file_replacement\"}"),
            _ => Json(HttpStatusCode.OK, "{\"id\":\"file_replacement\"}"),
            _ => Json(HttpStatusCode.OK, StoreJson("vs_existing", fingerprint, "completed", 2, 0, 9_876, 2, 2, 2)));
        using var httpClient = new HttpClient(handler);
        var publisher = new AzureOpenAIVectorStoreCorpusPublisher(CreateClient(httpClient), new AzureOpenAIVectorStoreCorpusFormatter(), (_, cancellationToken) => Task.CompletedTask.WaitAsync(cancellationToken));
        var progressEvents = new List<AzureOpenAIVectorStorePublicationProgress>();

        var publication = await publisher.ResumeAsync(manifest, new FakeDocumentProvider(CreateMarkdown("Published source text.")), "vs_existing", uploadConcurrency: 1, progress: new RecordingProgress<AzureOpenAIVectorStorePublicationProgress>(progressEvents.Add));

        Assert.AreEqual("vs_existing", publication.VectorStoreId);
        Assert.AreEqual(2, publication.FileCount);
        Assert.AreEqual(2L, publication.SearchRecordCount);
        Assert.AreEqual(9_876L, publication.UsageBytes);
        Assert.HasCount(7, handler.Requests);
        Assert.AreEqual(HttpMethod.Delete, handler.Requests[3].Method);
        StringAssert.Contains(handler.Requests[3].Uri.AbsolutePath, "/vector_stores/vs_existing/files/file_bad");
        StringAssert.Contains(handler.Requests[4].Body, "sefaria-bbbbbbbbbbbbbbbb.md");
        Assert.AreEqual("Completed", progressEvents[^1].Stage);
    }

    [TestMethod]
    [TestCategory("Unit")]
    public async Task ResumeAsync_SourceStoreNotCompleted_FailsBeforeInventory()
    {
        var document = TestManifestFactory.CreateDocument(segmentCount: 1, firstReference: "Genesis 1:1", lastReference: "Genesis 1:1");
        var manifest = TestManifestFactory.CreateManifest(document);
        var fingerprint = SourceIndexBuilder.ComputeCorpusFingerprint(manifest);
        var handler = new QueueHandler(_ => Json(HttpStatusCode.OK, StoreJson("vs_existing", fingerprint, "in_progress")));
        using var httpClient = new HttpClient(handler);
        var publisher = new AzureOpenAIVectorStoreCorpusPublisher(CreateClient(httpClient));

        var exception = await Assert.ThrowsExactlyAsync<InvalidDataException>(() => publisher.ResumeAsync(manifest, new FakeDocumentProvider(CreateMarkdown("Published source text.")), "vs_existing"));

        StringAssert.Contains(exception.Message, "cannot be resumed");
        Assert.HasCount(1, handler.Requests);
    }

    [TestMethod]
    [TestCategory("Unit")]
    public async Task CreateCleanReplacementAsync_CompletedFilesAndFailedDuplicate_ReusesFilesWithoutUploading()
    {
        var firstDocument = TestManifestFactory.CreateDocument(segmentCount: 1, firstReference: "Genesis 1:1", lastReference: "Genesis 1:1");
        var secondDocument = TestManifestFactory.CreateDocument(segmentCount: 1, firstReference: "Genesis 1:1", lastReference: "Genesis 1:1", rawSha256: new string('b', 64));
        var manifest = TestManifestFactory.CreateManifest(firstDocument, secondDocument);
        var fingerprint = SourceIndexBuilder.ComputeCorpusFingerprint(manifest);
        var handler = new QueueHandler(
            _ => Json(HttpStatusCode.OK, StoreJson("vs_source", fingerprint, "completed", 2, 1, documentCount: 2, fileCount: 2, segmentCount: 2)),
            _ => Json(HttpStatusCode.OK, "{\"data\":[{\"id\":\"file_a\",\"status\":\"completed\"},{\"id\":\"file_b\",\"status\":\"completed\"},{\"id\":\"file_failed_deleted\",\"status\":\"failed\"}],\"has_more\":false,\"last_id\":\"file_failed_deleted\"}"),
            _ => Json(HttpStatusCode.OK, "{\"data\":[{\"id\":\"file_a\",\"filename\":\"sefaria-aaaaaaaaaaaaaaaa.md\"},{\"id\":\"file_b\",\"filename\":\"sefaria-bbbbbbbbbbbbbbbb.md\"}],\"has_more\":false,\"last_id\":\"file_b\"}"),
            request =>
            {
                StringAssert.Contains(request.Body, "Clean store");
                StringAssert.Contains(request.Body, fingerprint);
                return Json(HttpStatusCode.OK, StoreJson("vs_clean", fingerprint, "in_progress", 0, 0, documentCount: 2, fileCount: 2, segmentCount: 2));
            },
            request =>
            {
                Assert.IsTrue(request.Body.Contains("file_a", StringComparison.Ordinal) || request.Body.Contains("file_b", StringComparison.Ordinal));
                StringAssert.Contains(request.Body, "chunking_strategy");
                return Json(HttpStatusCode.OK, "{\"id\":\"attached\"}");
            },
            request =>
            {
                Assert.IsTrue(request.Body.Contains("file_a", StringComparison.Ordinal) || request.Body.Contains("file_b", StringComparison.Ordinal));
                StringAssert.Contains(request.Body, "attributes");
                return Json(HttpStatusCode.OK, "{\"id\":\"attached\"}");
            },
            _ => Json(HttpStatusCode.OK, StoreJson("vs_clean", fingerprint, "completed", 2, 0, 7_654, 2, 2, 2)));
        using var httpClient = new HttpClient(handler);
        var publisher = new AzureOpenAIVectorStoreCorpusPublisher(CreateClient(httpClient), new AzureOpenAIVectorStoreCorpusFormatter(), (_, cancellationToken) => Task.CompletedTask.WaitAsync(cancellationToken));
        var progressEvents = new List<AzureOpenAIVectorStorePublicationProgress>();

        var publication = await publisher.CreateCleanReplacementAsync(manifest, new FakeDocumentProvider(CreateMarkdown("Published source text.")), "vs_source", "Clean store", attachConcurrency: 1, progress: new RecordingProgress<AzureOpenAIVectorStorePublicationProgress>(progressEvents.Add));

        Assert.AreEqual("vs_clean", publication.VectorStoreId);
        Assert.AreEqual(2, publication.FileCount);
        Assert.AreEqual(2L, publication.SearchRecordCount);
        Assert.AreEqual(7_654L, publication.UsageBytes);
        Assert.HasCount(7, handler.Requests);
        Assert.IsFalse(handler.Requests.Any(request => request.ContentType.Contains("multipart/form-data", StringComparison.OrdinalIgnoreCase)));
        Assert.AreEqual(HttpMethod.Post, handler.Requests[3].Method);
        Assert.IsTrue(progressEvents.Any(progress => progress.Stage == "Creating clean vector store"));
        Assert.AreEqual("Completed", progressEvents[^1].Stage);
    }

    [TestMethod]
    [TestCategory("Unit")]
    public async Task CreateCleanReplacementAsync_MissingCompletedFile_FailsBeforeCreatingStore()
    {
        var firstDocument = TestManifestFactory.CreateDocument(segmentCount: 1, firstReference: "Genesis 1:1", lastReference: "Genesis 1:1");
        var secondDocument = TestManifestFactory.CreateDocument(segmentCount: 1, firstReference: "Genesis 1:1", lastReference: "Genesis 1:1", rawSha256: new string('b', 64));
        var manifest = TestManifestFactory.CreateManifest(firstDocument, secondDocument);
        var fingerprint = SourceIndexBuilder.ComputeCorpusFingerprint(manifest);
        var handler = new QueueHandler(
            _ => Json(HttpStatusCode.OK, StoreJson("vs_source", fingerprint, "completed", 1, 1, documentCount: 2, fileCount: 2, segmentCount: 2)),
            _ => Json(HttpStatusCode.OK, "{\"data\":[{\"id\":\"file_a\",\"status\":\"completed\"},{\"id\":\"file_failed\",\"status\":\"failed\"}],\"has_more\":false,\"last_id\":\"file_failed\"}"),
            _ => Json(HttpStatusCode.OK, "{\"data\":[{\"id\":\"file_a\",\"filename\":\"sefaria-aaaaaaaaaaaaaaaa.md\"}],\"has_more\":false,\"last_id\":\"file_a\"}"));
        using var httpClient = new HttpClient(handler);
        var publisher = new AzureOpenAIVectorStoreCorpusPublisher(CreateClient(httpClient));

        var exception = await Assert.ThrowsExactlyAsync<InvalidDataException>(() => publisher.CreateCleanReplacementAsync(manifest, new FakeDocumentProvider(CreateMarkdown("Published source text.")), "vs_source", "Clean store", attachConcurrency: 1));

        StringAssert.Contains(exception.Message, "Missing");
        Assert.HasCount(3, handler.Requests);
        Assert.IsTrue(handler.Requests.All(request => request.Method == HttpMethod.Get));
    }

    [TestMethod]
    [DataRow("maximum")]
    [DataRow("concurrency-low")]
    [DataRow("concurrency-high")]
    [DataRow("schema")]
    [DataRow("count")]
    [DataRow("empty")]
    [DataRow("source-id")]
    [DataRow("name")]
    [TestCategory("Unit")]
    public async Task CreateCleanReplacementAsync_InvalidArguments_ThrowsBeforeNetwork(string scenario)
    {
        var document = TestManifestFactory.CreateDocument(segmentCount: 1, firstReference: "Genesis 1:1", lastReference: "Genesis 1:1");
        var manifest = scenario switch
        {
            "schema" => TestManifestFactory.CreateManifest(document) with { SchemaVersion = "old" },
            "count" => TestManifestFactory.CreateManifest(document) with { DocumentCount = 2 },
            "empty" => TestManifestFactory.CreateManifest(),
            _ => TestManifestFactory.CreateManifest(document),
        };
        var maximumDocuments = scenario == "maximum" ? 0 : (int?)null;
        var concurrency = scenario switch
        {
            "concurrency-low" => 0,
            "concurrency-high" => 17,
            _ => 1,
        };
        var sourceId = scenario == "source-id" ? " " : "vs_source";
        var name = scenario == "name" ? " " : "Clean store";
        var handler = new QueueHandler();
        using var httpClient = new HttpClient(handler);
        var publisher = new AzureOpenAIVectorStoreCorpusPublisher(CreateClient(httpClient));

        Task action = publisher.CreateCleanReplacementAsync(manifest, new FakeDocumentProvider(CreateMarkdown("Published source text.")), sourceId, name, maximumDocuments, concurrency);

        if (scenario is "maximum" or "concurrency-low" or "concurrency-high")
        {
            await Assert.ThrowsExactlyAsync<ArgumentOutOfRangeException>(() => action);
        }
        else
        {
            await Assert.ThrowsExactlyAsync<ArgumentException>(() => action);
        }
        Assert.HasCount(0, handler.Requests);
    }

    [TestMethod]
    [TestCategory("Unit")]
    public async Task CreateCleanReplacementAsync_SourceStoreNotCompleted_FailsBeforeInventory()
    {
        var document = TestManifestFactory.CreateDocument(segmentCount: 1, firstReference: "Genesis 1:1", lastReference: "Genesis 1:1");
        var manifest = TestManifestFactory.CreateManifest(document);
        var fingerprint = SourceIndexBuilder.ComputeCorpusFingerprint(manifest);
        var handler = new QueueHandler(_ => Json(HttpStatusCode.OK, StoreJson("vs_source", fingerprint, "in_progress")));
        using var httpClient = new HttpClient(handler);
        var publisher = new AzureOpenAIVectorStoreCorpusPublisher(CreateClient(httpClient));

        var exception = await Assert.ThrowsExactlyAsync<InvalidDataException>(() => publisher.CreateCleanReplacementAsync(manifest, new FakeDocumentProvider(CreateMarkdown("Published source text.")), "vs_source", "Clean store"));

        StringAssert.Contains(exception.Message, "cannot be replaced");
        Assert.HasCount(1, handler.Requests);
    }

    [TestMethod]
    [DataRow("status")]
    [DataRow("missing-catalog")]
    [DataRow("unexpected")]
    [DataRow("duplicate")]
    [TestCategory("Unit")]
    public async Task CreateCleanReplacementAsync_InvalidCompletedFileInventory_FailsBeforeCreatingStore(string scenario)
    {
        var document = TestManifestFactory.CreateDocument(segmentCount: 1, firstReference: "Genesis 1:1", lastReference: "Genesis 1:1");
        var manifest = TestManifestFactory.CreateManifest(document);
        var fingerprint = SourceIndexBuilder.ComputeCorpusFingerprint(manifest);
        var storeFilesJson = scenario switch
        {
            "status" => "{\"data\":[{\"id\":\"file_a\",\"status\":\"in_progress\"}],\"has_more\":false,\"last_id\":\"file_a\"}",
            "duplicate" => "{\"data\":[{\"id\":\"file_a\",\"status\":\"completed\"},{\"id\":\"file_b\",\"status\":\"completed\"}],\"has_more\":false,\"last_id\":\"file_b\"}",
            _ => "{\"data\":[{\"id\":\"file_a\",\"status\":\"completed\"}],\"has_more\":false,\"last_id\":\"file_a\"}",
        };
        var uploadedFilesJson = scenario switch
        {
            "missing-catalog" => "{\"data\":[],\"has_more\":false,\"last_id\":null}",
            "unexpected" => "{\"data\":[{\"id\":\"file_a\",\"filename\":\"unexpected.md\"}],\"has_more\":false,\"last_id\":\"file_a\"}",
            "duplicate" => "{\"data\":[{\"id\":\"file_a\",\"filename\":\"sefaria-aaaaaaaaaaaaaaaa.md\"},{\"id\":\"file_b\",\"filename\":\"sefaria-aaaaaaaaaaaaaaaa.md\"}],\"has_more\":false,\"last_id\":\"file_b\"}",
            _ => "{\"data\":[{\"id\":\"file_a\",\"filename\":\"sefaria-aaaaaaaaaaaaaaaa.md\"}],\"has_more\":false,\"last_id\":\"file_a\"}",
        };
        var handler = new QueueHandler(
            _ => Json(HttpStatusCode.OK, StoreJson("vs_source", fingerprint)),
            _ => Json(HttpStatusCode.OK, storeFilesJson),
            _ => Json(HttpStatusCode.OK, uploadedFilesJson));
        using var httpClient = new HttpClient(handler);
        var publisher = new AzureOpenAIVectorStoreCorpusPublisher(CreateClient(httpClient));

        await Assert.ThrowsExactlyAsync<InvalidDataException>(() => publisher.CreateCleanReplacementAsync(manifest, new FakeDocumentProvider(CreateMarkdown("Published source text.")), "vs_source", "Clean store"));

        Assert.HasCount(3, handler.Requests);
        Assert.IsTrue(handler.Requests.All(request => request.Method == HttpMethod.Get));
    }

    [TestMethod]
    [TestCategory("Unit")]
    public async Task PublishAsync_OneVerifiedDocument_UploadsAttachesPollsAndReturnsUsage()
    {
        var document = TestManifestFactory.CreateDocument(segmentCount: 1, firstReference: "Genesis 1:1", lastReference: "Genesis 1:1");
        var manifest = TestManifestFactory.CreateManifest(document);
        var fingerprint = SourceIndexBuilder.ComputeCorpusFingerprint(manifest);
        var storeJson = StoreJson("vs_new", fingerprint, "in_progress", 0, 0, 0);
        var completedStoreJson = StoreJson("vs_new", fingerprint, "completed", 1, 0, 9_876);
        var handler = new QueueHandler(
            request =>
            {
                StringAssert.Contains(request.Body, "\"name\":\"Test store\"");
                StringAssert.Contains(request.Body, "\"metadata\"");
                Assert.IsFalse(request.Body.Contains("description", StringComparison.Ordinal));
                return Json(HttpStatusCode.OK, storeJson);
            },
            request =>
            {
                StringAssert.Contains(request.ContentType, "multipart/form-data");
                StringAssert.Contains(request.Body, "assistants");
                return Json(HttpStatusCode.OK, "{\"id\":\"file_new\",\"object\":\"file\",\"bytes\":12,\"created_at\":1,\"filename\":\"source.md\",\"purpose\":\"assistants\",\"status\":\"processed\"}");
            },
            request =>
            {
                StringAssert.Contains(request.Body, "file_new");
                StringAssert.Contains(request.Body, "chunk_overlap_tokens");
                StringAssert.Contains(request.Body, "\"attributes\"");
                Assert.AreEqual("/openai/v1/vector_stores/vs_new/files", request.Uri.AbsolutePath);
                return Json(HttpStatusCode.OK, "{\"id\":\"file_new\",\"object\":\"vector_store.file\",\"status\":\"in_progress\"}");
            },
            _ => Json(HttpStatusCode.OK, storeJson),
            _ => Json(HttpStatusCode.OK, completedStoreJson));
        using var httpClient = new HttpClient(handler);
        var client = CreateClient(httpClient);
        var publisher = new AzureOpenAIVectorStoreCorpusPublisher(client, new AzureOpenAIVectorStoreCorpusFormatter(), (_, cancellationToken) => Task.CompletedTask.WaitAsync(cancellationToken));

        var publication = await publisher.PublishAsync(manifest, new FakeDocumentProvider(CreateMarkdown("Published source text.")), "Test store", uploadConcurrency: 1);

        Assert.AreEqual("vs_new", publication.VectorStoreId);
        Assert.AreEqual(fingerprint, publication.CorpusFingerprint);
        Assert.AreEqual(1, publication.DocumentCount);
        Assert.AreEqual(1L, publication.SegmentCount);
        Assert.AreEqual(1L, publication.SearchRecordCount);
        Assert.AreEqual(1, publication.FileCount);
        Assert.AreEqual(9_876L, publication.UsageBytes);
        Assert.HasCount(5, handler.Requests);
    }

    [TestMethod]
    [TestCategory("Unit")]
    public async Task PublishAsync_FailedStore_FailsClosed()
    {
        var document = TestManifestFactory.CreateDocument(segmentCount: 1, firstReference: "Genesis 1:1", lastReference: "Genesis 1:1");
        var manifest = TestManifestFactory.CreateManifest(document);
        var fingerprint = SourceIndexBuilder.ComputeCorpusFingerprint(manifest);
        var handler = new QueueHandler(
            _ => Json(HttpStatusCode.OK, StoreJson("vs_new", fingerprint, "in_progress", 0, 0, 0)),
            _ => Json(HttpStatusCode.OK, "{\"id\":\"file_new\"}"),
            _ => Json(HttpStatusCode.OK, "{\"id\":\"file_new\"}"),
            _ => Json(HttpStatusCode.OK, StoreJson("vs_new", fingerprint, "completed", 0, 1, 0)));
        using var httpClient = new HttpClient(handler);
        var publisher = new AzureOpenAIVectorStoreCorpusPublisher(CreateClient(httpClient));

        var exception = await Assert.ThrowsExactlyAsync<InvalidDataException>(() => publisher.PublishAsync(manifest, new FakeDocumentProvider(CreateMarkdown("Published source text.")), "Test store", uploadConcurrency: 1));

        StringAssert.Contains(exception.Message, "failed");
        Assert.HasCount(4, handler.Requests);
    }

    [TestMethod]
    [DataRow("maximum")]
    [DataRow("concurrency-low")]
    [DataRow("concurrency-high")]
    [DataRow("schema")]
    [DataRow("count")]
    [DataRow("empty")]
    [TestCategory("Unit")]
    public async Task PublishAsync_InvalidArguments_ThrowsBeforeNetwork(string scenario)
    {
        var document = TestManifestFactory.CreateDocument(segmentCount: 1, firstReference: "Genesis 1:1", lastReference: "Genesis 1:1");
        var manifest = scenario switch
        {
            "schema" => TestManifestFactory.CreateManifest(document) with { SchemaVersion = "old" },
            "count" => TestManifestFactory.CreateManifest(document) with { DocumentCount = 2 },
            "empty" => TestManifestFactory.CreateManifest(),
            _ => TestManifestFactory.CreateManifest(document),
        };
        var maximumDocuments = scenario == "maximum" ? 0 : (int?)null;
        var concurrency = scenario switch
        {
            "concurrency-low" => 0,
            "concurrency-high" => 17,
            _ => 1,
        };
        var handler = new QueueHandler();
        using var httpClient = new HttpClient(handler);
        var publisher = new AzureOpenAIVectorStoreCorpusPublisher(CreateClient(httpClient));

        Task action = publisher.PublishAsync(manifest, new FakeDocumentProvider(CreateMarkdown("Published source text.")), "Test store", maximumDocuments, concurrency);

        if (scenario is "maximum" or "concurrency-low" or "concurrency-high")
        {
            await Assert.ThrowsExactlyAsync<ArgumentOutOfRangeException>(() => action);
        }
        else
        {
            await Assert.ThrowsExactlyAsync<ArgumentException>(() => action);
        }
        Assert.HasCount(0, handler.Requests);
    }

    [TestMethod]
    [DataRow("missing-data")]
    [DataRow("missing-file-id")]
    [DataRow("missing-score")]
    [DataRow("attributes-not-object")]
    [TestCategory("Unit")]
    public async Task SearchAsync_InvalidProviderContract_ThrowsInvalidData(string scenario)
    {
        var json = scenario switch
        {
            "missing-data" => "{}",
            "missing-file-id" => "{\"output\":[{\"type\":\"file_search_call\",\"results\":[{\"filename\":\"source.md\",\"score\":0.5,\"attributes\":{},\"text\":\"result\"}]}]}",
            "missing-score" => "{\"output\":[{\"type\":\"file_search_call\",\"results\":[{\"file_id\":\"file_1\",\"filename\":\"source.md\",\"attributes\":{},\"text\":\"result\"}]}]}",
            "attributes-not-object" => "{\"output\":[{\"type\":\"file_search_call\",\"results\":[{\"file_id\":\"file_1\",\"filename\":\"source.md\",\"score\":0.5,\"attributes\":[],\"text\":\"result\"}]}]}",
            _ => throw new AssertFailedException($"Unknown scenario '{scenario}'."),
        };
        var handler = new QueueHandler(_ => Json(HttpStatusCode.OK, json));
        using var httpClient = new HttpClient(handler);
        var client = CreateClient(httpClient);

        await Assert.ThrowsExactlyAsync<InvalidDataException>(() => client.SearchAsync("vs_test", new AzureOpenAIVectorStoreSearchRequest { Queries = ["question"] }));
    }

    [TestMethod]
    [TestCategory("Unit")]
    public async Task SearchAsync_NumericAttribute_ConvertsToString()
    {
        var handler = new QueueHandler(_ => Json(HttpStatusCode.OK, """{"output":[{"type":"file_search_call","status":"completed","results":[{"file_id":"file_1","filename":"source.md","score":0.5,"attributes":{"numeric":42},"text":"result text"}]}]}"""));
        using var httpClient = new HttpClient(handler);
        var client = CreateClient(httpClient);

        var page = await client.SearchAsync("vs_test", new AzureOpenAIVectorStoreSearchRequest { Queries = ["question"] });

        Assert.IsFalse(page.HasMore);
        Assert.HasCount(1, page.Results);
        CollectionAssert.AreEqual(new[] { "result text" }, page.Results[0].Content.ToArray());
        Assert.AreEqual("42", page.Results[0].Attributes["numeric"]);
    }

    [TestMethod]
    [TestCategory("Unit")]
    public async Task SearchAsync_ProviderReportsFinalPage_ReturnsHasMoreFalse()
    {
        var handler = new QueueHandler(_ => Json(HttpStatusCode.OK, """{"output":[{"type":"file_search_call","status":"completed","results":[]}]}"""));
        using var httpClient = new HttpClient(handler);

        var page = await CreateClient(httpClient).SearchAsync("vs_test", new AzureOpenAIVectorStoreSearchRequest { Queries = ["question"] });

        Assert.IsFalse(page.HasMore);
        Assert.HasCount(0, page.Results);
    }

    [TestMethod]
    [DataRow("missing-id")]
    [DataRow("metadata-not-object")]
    [TestCategory("Unit")]
    public async Task GetAsync_InvalidStoreContract_ThrowsInvalidData(string scenario)
    {
        var json = scenario switch
        {
            "missing-id" => "{\"name\":\"Store\",\"status\":\"completed\"}",
            "metadata-not-object" => "{\"id\":\"vs_test\",\"name\":\"Store\",\"status\":\"completed\",\"metadata\":[]}",
            _ => throw new AssertFailedException($"Unknown scenario '{scenario}'."),
        };
        var handler = new QueueHandler(_ => Json(HttpStatusCode.OK, json));
        using var httpClient = new HttpClient(handler);

        await Assert.ThrowsExactlyAsync<InvalidDataException>(() => CreateClient(httpClient).GetAsync("vs_test"));
    }

    [TestMethod]
    [TestCategory("Unit")]
    public async Task GetAsync_OptionalStoreFieldsMissing_ReturnsSafeDefaults()
    {
        var handler = new QueueHandler(_ => Json(HttpStatusCode.OK, "{\"id\":\"vs_test\",\"name\":\"Store\",\"status\":\"completed\",\"metadata\":null}"));
        using var httpClient = new HttpClient(handler);

        var store = await CreateClient(httpClient).GetAsync("vs_test");

        Assert.AreEqual(0L, store.UsageBytes);
        Assert.AreEqual(0, store.CompletedFileCount);
        Assert.AreEqual(0, store.FailedFileCount);
        Assert.HasCount(0, store.Metadata);
    }

    [TestMethod]
    [DataRow("empty")]
    [DataRow("too-many")]
    [DataRow("too-many-attributes")]
    [DataRow("empty-key")]
    [DataRow("long-value")]
    [TestCategory("Unit")]
    public async Task AttachFileAsync_InvalidFile_ThrowsBeforeNetwork(string scenario)
    {
        var handler = new QueueHandler();
        using var httpClient = new HttpClient(handler);
        var files = scenario switch
        {
            "empty" => new AzureOpenAIVectorStoreUploadedFile(" ", new Dictionary<string, string>()),
            "too-many" => new AzureOpenAIVectorStoreUploadedFile("file_1", Enumerable.Range(0, 17).ToDictionary(index => $"key{index}", _ => "value")),
            "too-many-attributes" => new AzureOpenAIVectorStoreUploadedFile("file_1", Enumerable.Range(0, 17).ToDictionary(index => $"key{index}", _ => "value")),
            "empty-key" => new AzureOpenAIVectorStoreUploadedFile("file_1", new Dictionary<string, string> { [" "] = "value" }),
            "long-value" => new AzureOpenAIVectorStoreUploadedFile("file_1", new Dictionary<string, string> { ["key"] = new string('x', 513) }),
            _ => throw new AssertFailedException($"Unknown scenario '{scenario}'."),
        };

        await Assert.ThrowsExactlyAsync<ArgumentException>(() => CreateClient(httpClient).AttachFileAsync("vs_test", files, CancellationToken.None));
        Assert.HasCount(0, handler.Requests);
    }

    [TestMethod]
    [DataRow("status")]
    [DataRow("schema")]
    [DataRow("missing-schema")]
    [DataRow("fingerprint")]
    [DataRow("missing-fingerprint")]
    [DataRow("counts")]
    [DataRow("document-count")]
    [DataRow("missing-document-count")]
    [DataRow("file-count")]
    [DataRow("missing-file-count")]
    [TestCategory("Unit")]
    public async Task VerifyAsync_MismatchedStore_RejectsPublication(string scenario)
    {
        var fingerprint = new string('a', 64);
        var json = scenario switch
        {
            "status" => StoreJson("vs_test", fingerprint, "in_progress"),
            "schema" => StoreJson("vs_test", fingerprint).Replace("\"schemaVersion\":\"2\"", "\"schemaVersion\":\"0\"", StringComparison.Ordinal),
            "missing-schema" => StoreJson("vs_test", fingerprint).Replace("\"schemaVersion\":\"2\",", string.Empty, StringComparison.Ordinal),
            "fingerprint" => StoreJson("vs_test", new string('b', 64)),
            "missing-fingerprint" => StoreJson("vs_test", fingerprint).Replace($"\"corpusFingerprint\":\"{fingerprint}\",", string.Empty, StringComparison.Ordinal),
            "counts" => StoreJson("vs_test", fingerprint, completed: 0, failed: 1),
            "document-count" => StoreJson("vs_test", fingerprint).Replace("\"documentCount\":\"1\"", "\"documentCount\":\"2\"", StringComparison.Ordinal),
            "missing-document-count" => StoreJson("vs_test", fingerprint).Replace("\"documentCount\":\"1\",", string.Empty, StringComparison.Ordinal),
            "file-count" => StoreJson("vs_test", fingerprint).Replace("\"fileCount\":\"1\"", "\"fileCount\":\"0\"", StringComparison.Ordinal),
            "missing-file-count" => StoreJson("vs_test", fingerprint).Replace("\"fileCount\":\"1\",", string.Empty, StringComparison.Ordinal),
            _ => throw new AssertFailedException($"Unknown scenario '{scenario}'."),
        };
        var handler = new QueueHandler(_ => Json(HttpStatusCode.OK, json));
        using var httpClient = new HttpClient(handler);
        var publisher = new AzureOpenAIVectorStoreCorpusPublisher(CreateClient(httpClient));

        await Assert.ThrowsExactlyAsync<InvalidDataException>(() => publisher.VerifyAsync("vs_test", fingerprint, 1));
    }

    [TestMethod]
    [TestCategory("Unit")]
    public async Task VerifyAsync_MatchingStore_ReturnsVerifiedInformation()
    {
        var fingerprint = new string('a', 64);
        var handler = new QueueHandler(_ => Json(HttpStatusCode.OK, StoreJson("vs_test", fingerprint)));
        using var httpClient = new HttpClient(handler);

        var store = await new AzureOpenAIVectorStoreCorpusPublisher(CreateClient(httpClient)).VerifyAsync("vs_test", fingerprint, 1);

        Assert.AreEqual("vs_test", store.Id);
    }

    [TestMethod]
    [TestCategory("Unit")]
    public async Task VerifyAsync_NonPositiveExpectedDocumentCount_ThrowsBeforeNetwork()
    {
        var handler = new QueueHandler();
        using var httpClient = new HttpClient(handler);

        await Assert.ThrowsExactlyAsync<ArgumentOutOfRangeException>(() => new AzureOpenAIVectorStoreCorpusPublisher(CreateClient(httpClient)).VerifyAsync("vs_test", new string('a', 64), 0));

        Assert.HasCount(0, handler.Requests);
    }

    private static AzureOpenAIVectorStoreClient CreateClient(HttpClient httpClient, Func<TimeSpan, CancellationToken, Task>? delayAsync = null)
    {
        var options = new AzureOpenAIVectorStoreClientOptions
        {
            ProjectEndpoint = new Uri("https://openai.example.test/"),
            ModelName = "search-model",
        };
        return delayAsync is null
            ? new AzureOpenAIVectorStoreClient(options, new FakeTokenCredential(), httpClient)
            : new AzureOpenAIVectorStoreClient(options, new FakeTokenCredential(), httpClient, delayAsync);
    }

    private static string StoreJson(string id, string fingerprint, string status = "completed", int completed = 1, int failed = 0, long usageBytes = 123, int documentCount = 1, int fileCount = 1, long segmentCount = 1) => $$"""
        {
          "id":"{{id}}",
          "name":"Test store",
          "status":"{{status}}",
          "usage_bytes":{{usageBytes}},
          "file_counts":{"completed":{{completed}},"failed":{{failed}},"in_progress":0,"cancelled":0,"total":{{completed + failed}}},
          "metadata":{"schemaVersion":"2","corpusFingerprint":"{{fingerprint}}","documentCount":"{{documentCount}}","fileCount":"{{fileCount}}","segmentCount":"{{segmentCount}}","sourceProvider":"Sefaria"}
        }
        """;

    private static string CreateMarkdown(string text) => $"""
        # Genesis

        ## Genesis 1:1
        {text}
        """;

    private static HttpResponseMessage Json(HttpStatusCode status, string content) => new(status)
    {
        Content = new StringContent(content, Encoding.UTF8, "application/json"),
    };

    private sealed class FakeDocumentProvider : INormalizedDocumentProvider
    {
        private readonly string markdown;

        internal FakeDocumentProvider(string markdown)
        {
            this.markdown = markdown;
        }

        public Task<string> LoadAsync(ManifestDocument document, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(markdown);
        }
    }

    private sealed class FakeTokenCredential : TokenCredential
    {
        public override AccessToken GetToken(TokenRequestContext requestContext, CancellationToken cancellationToken) => new("test-token", DateTimeOffset.MaxValue);

        public override ValueTask<AccessToken> GetTokenAsync(TokenRequestContext requestContext, CancellationToken cancellationToken) => ValueTask.FromResult(new AccessToken("test-token", DateTimeOffset.MaxValue));
    }

    private sealed class QueueHandler : HttpMessageHandler
    {
        private readonly Queue<Func<CapturedRequest, HttpResponseMessage>> responses;

        internal QueueHandler(params Func<CapturedRequest, HttpResponseMessage>[] responses)
        {
            this.responses = new Queue<Func<CapturedRequest, HttpResponseMessage>>(responses);
        }

        internal List<CapturedRequest> Requests { get; } = [];

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var body = request.Content is null ? string.Empty : await request.Content.ReadAsStringAsync(cancellationToken);
            var captured = new CapturedRequest(request.Method, request.RequestUri!, body, request.Content?.Headers.ContentType?.ToString() ?? string.Empty, request.Headers.Authorization?.ToString() ?? string.Empty);
            Requests.Add(captured);
            return responses.Count == 0 ? throw new AssertFailedException("Unexpected HTTP request.") : responses.Dequeue()(captured);
        }
    }

    private sealed record CapturedRequest(HttpMethod Method, Uri Uri, string Body, string ContentType, string Authorization);

    private sealed class RecordingProgress<T> : IProgress<T>
    {
        private readonly Action<T> report;

        internal RecordingProgress(Action<T> report)
        {
            this.report = report;
        }

        public void Report(T value) => report(value);
    }
}
