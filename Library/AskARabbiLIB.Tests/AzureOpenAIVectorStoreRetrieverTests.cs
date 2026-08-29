using System.Text;
using AskARabbiLIB.Retrieval;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace AskARabbiLIB.Tests;

[TestClass]
public sealed class AzureOpenAIVectorStoreRetrieverTests
{
    private static readonly AskARabbiLIB.Models.ManifestDocument Document = TestManifestFactory.CreateDocument(segmentCount: 1, firstReference: "Genesis 1:1", lastReference: "Genesis 1:1");
    private static readonly AskARabbiLIB.Models.DocumentManifest Manifest = TestManifestFactory.CreateManifest(Document);
    private static readonly string Fingerprint = SourceIndexBuilder.ComputeCorpusFingerprint(Manifest);

    [TestMethod]
    [DataRow("short-fingerprint")]
    [DataRow("uppercase-fingerprint")]
    [DataRow("low-threshold")]
    [DataRow("high-threshold")]
    [TestCategory("Unit")]
    public void Validate_InvalidRetrieverOptions_Throws(string scenario)
    {
        var options = scenario switch
        {
            "short-fingerprint" => new AzureOpenAIVectorStoreRetrieverOptions { VectorStoreId = "vs_test", ExpectedCorpusFingerprint = "abc" },
            "uppercase-fingerprint" => new AzureOpenAIVectorStoreRetrieverOptions { VectorStoreId = "vs_test", ExpectedCorpusFingerprint = new string('A', 64) },
            "low-threshold" => new AzureOpenAIVectorStoreRetrieverOptions { VectorStoreId = "vs_test", ExpectedCorpusFingerprint = Fingerprint, ScoreThreshold = -0.01 },
            "high-threshold" => new AzureOpenAIVectorStoreRetrieverOptions { VectorStoreId = "vs_test", ExpectedCorpusFingerprint = Fingerprint, ScoreThreshold = 1.01 },
            _ => throw new AssertFailedException($"Unknown scenario '{scenario}'."),
        };

        if (scenario.EndsWith("fingerprint", StringComparison.Ordinal))
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
    public async Task SearchAsync_SemanticResult_ReturnsParsedTrustedHitAndVerifiesOnce()
    {
        var fixture = CreateFixture();
        var client = new FakeSearchClient(fixture.Store, fixture.Page);
        var retriever = CreateRetriever(client);
        var query = new SourceRetrievalQuery { QueryText = "beginning tested passage", Languages = ["English"], SourceKeys = ["collection:Torah"] };

        var first = await retriever.SearchAsync(query);
        var second = await retriever.SearchAsync(query);

        Assert.HasCount(1, first);
        Assert.AreEqual("Genesis 1:1", first[0].Segment.CanonicalReference);
        Assert.IsFalse(first[0].IsExactReference);
        Assert.AreEqual(0.83, first[0].Score);
        Assert.HasCount(1, second);
        Assert.AreEqual(1, client.GetCallCount);
        Assert.AreEqual(2, client.SearchCallCount);
        Assert.IsTrue(client.Requests.All(request => request.RewriteQuery));
    }

    [TestMethod]
    [TestCategory("Unit")]
    public async Task SearchAsync_ExactReference_PostFiltersNonExactRecords()
    {
        var fixture = CreateFixture();
        var client = new FakeSearchClient(fixture.Store, fixture.Page);
        var retriever = CreateRetriever(client);

        var hits = await retriever.SearchAsync(new SourceRetrievalQuery { ExactCanonicalReference = "Genesis 1:1" });

        Assert.HasCount(1, hits);
        Assert.IsTrue(hits[0].IsExactReference);
        Assert.IsFalse(client.Requests.Single().RewriteQuery);
        StringAssert.Contains(client.Requests.Single().Queries.Single(), "Genesis 1:1");
    }

    [TestMethod]
    [TestCategory("Unit")]
    public async Task GetContextAsync_RangeQuery_ReturnsOnlyRequestedDocumentOrdinals()
    {
        var fixture = CreateFixture();
        var client = new FakeSearchClient(fixture.Store, fixture.Page);
        var retriever = CreateRetriever(client);

        var context = await retriever.GetContextAsync(fixture.DocumentId, 0, 2);

        Assert.HasCount(1, context);
        Assert.AreEqual(0, context[0].DocumentOrdinal);
        Assert.HasCount(3, client.Requests.Single().Queries);
        CollectionAssert.AreEqual(new[] { fixture.DocumentId }, client.Requests.Single().DocumentIds.ToArray());
    }

    [TestMethod]
    [TestCategory("Unit")]
    public async Task SearchAsync_StaleStore_ThrowsBeforeSearching()
    {
        var fixture = CreateFixture();
        var metadata = fixture.Store.Metadata.ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.Ordinal);
        metadata["corpusFingerprint"] = new string('e', 64);
        var client = new FakeSearchClient(fixture.Store with { Metadata = metadata }, fixture.Page);
        var retriever = CreateRetriever(client);

        var exception = await Assert.ThrowsExactlyAsync<InvalidOperationException>(() => retriever.SearchAsync(new SourceRetrievalQuery { QueryText = "beginning" }));

        StringAssert.Contains(exception.Message, "fingerprint");
        Assert.AreEqual(0, client.SearchCallCount);
    }

    [TestMethod]
    [TestCategory("Unit")]
    public async Task SearchAsync_CategoryMismatch_ReturnsNoHits()
    {
        var fixture = CreateFixture();
        var retriever = CreateRetriever(new FakeSearchClient(fixture.Store, fixture.Page));

        var hits = await retriever.SearchAsync(new SourceRetrievalQuery { QueryText = "beginning", Categories = ["Talmud"] });

        Assert.HasCount(0, hits);
    }

    [TestMethod]
    [DataRow("empty")]
    [DataRow("limit")]
    [DataRow("source")]
    [DataRow("context-ordinal")]
    [DataRow("context-radius")]
    [TestCategory("Unit")]
    public async Task Retrieval_InvalidRequest_Throws(string scenario)
    {
        var fixture = CreateFixture();
        var retriever = CreateRetriever(new FakeSearchClient(fixture.Store, fixture.Page));

        Task action = scenario switch
        {
            "empty" => retriever.SearchAsync(new SourceRetrievalQuery()),
            "limit" => retriever.SearchAsync(new SourceRetrievalQuery { QueryText = "text", CandidateLimit = 51 }),
            "source" => retriever.SearchAsync(new SourceRetrievalQuery { QueryText = "text", SourceKeys = ["bad"] }),
            "context-ordinal" => retriever.GetContextAsync(fixture.DocumentId, -1, 1),
            "context-radius" => retriever.GetContextAsync(fixture.DocumentId, 0, 11),
            _ => throw new AssertFailedException($"Unknown scenario '{scenario}'."),
        };

        if (scenario is "limit" or "context-ordinal" or "context-radius")
        {
            await Assert.ThrowsExactlyAsync<ArgumentOutOfRangeException>(() => action);
        }
        else
        {
            await Assert.ThrowsExactlyAsync<ArgumentException>(() => action);
        }
    }

    private static AzureOpenAIVectorStoreRetriever CreateRetriever(IAzureOpenAIVectorStoreSearchClient client) => new(client, new AzureOpenAIVectorStoreRetrieverOptions
    {
        VectorStoreId = "vs_test",
        ExpectedCorpusFingerprint = Fingerprint,
    }, Manifest);

    private static Fixture CreateFixture()
    {
        var markdown = """
            # Genesis

            ## Genesis 1:1
            The beginning of a tested passage.
            """;
        var formatted = new AzureOpenAIVectorStoreCorpusFormatter().Format(Document, markdown, Fingerprint);
        var result = new AzureOpenAIVectorStoreSearchResult("file_test", formatted.FileName, 0.83, new Dictionary<string, string>(), [Encoding.UTF8.GetString(formatted.Content)]);
        var page = new AzureOpenAIVectorStoreSearchPage([result], false);
        var metadata = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["schemaVersion"] = "2",
            ["corpusFingerprint"] = Fingerprint,
            ["documentCount"] = "1",
            ["fileCount"] = "1",
            ["sourceProvider"] = "Sefaria",
        };
        var store = new AzureOpenAIVectorStoreInfo("vs_test", "Test", "completed", 123, 1, 0, metadata);
        return new Fixture(Document.DocumentId, store, page);
    }

    private sealed record Fixture(string DocumentId, AzureOpenAIVectorStoreInfo Store, AzureOpenAIVectorStoreSearchPage Page);

    private sealed class FakeSearchClient : IAzureOpenAIVectorStoreSearchClient
    {
        private readonly AzureOpenAIVectorStoreInfo store;
        private readonly AzureOpenAIVectorStoreSearchPage page;

        internal FakeSearchClient(AzureOpenAIVectorStoreInfo store, AzureOpenAIVectorStoreSearchPage page)
        {
            this.store = store;
            this.page = page;
        }

        internal int GetCallCount { get; private set; }

        internal int SearchCallCount { get; private set; }

        internal List<AzureOpenAIVectorStoreSearchRequest> Requests { get; } = [];

        public Task<AzureOpenAIVectorStoreInfo> GetAsync(string vectorStoreId, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            GetCallCount++;
            return Task.FromResult(store);
        }

        public Task<AzureOpenAIVectorStoreSearchPage> SearchAsync(string vectorStoreId, AzureOpenAIVectorStoreSearchRequest request, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            SearchCallCount++;
            Requests.Add(request);
            return Task.FromResult(page);
        }
    }
}
