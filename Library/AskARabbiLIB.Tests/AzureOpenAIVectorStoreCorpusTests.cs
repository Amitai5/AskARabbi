using System.Text;
using AskARabbiLIB.Retrieval;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace AskARabbiLIB.Tests;

[TestClass]
public sealed class AzureOpenAIVectorStoreCorpusTests
{
    private static readonly string Fingerprint = new('b', 64);

    [TestMethod]
    [TestCategory("Unit")]
    public void FormatAndParse_ShortSegment_RoundTripsTrustedProvenance()
    {
        var document = CreateDocument();
        var formatter = new AzureOpenAIVectorStoreCorpusFormatter();
        var formatted = formatter.Format(document, CreateMarkdown("The beginning of a tested passage."), Fingerprint);

        var segments = new AzureOpenAIVectorStoreCorpusParser().Parse(formatted.Attributes, [Encoding.UTF8.GetString(formatted.Content)], Fingerprint);

        Assert.AreEqual("sefaria-aaaaaaaaaaaaaaaa.md", formatted.FileName);
        Assert.HasCount(1, segments);
        Assert.AreEqual($"{document.DocumentId}:segment:00000001", segments[0].SegmentId);
        Assert.AreEqual("Genesis 1:1", segments[0].CanonicalReference);
        Assert.AreEqual("The beginning of a tested passage.", segments[0].Text);
        Assert.IsFalse(segments[0].IsExcerpt);
        Assert.AreEqual(document.AttributionUrl, segments[0].SourceUrl);
        Assert.AreEqual(document.LicenseCategory, segments[0].LicenseCategory);
        Assert.HasCount(16, formatted.Attributes);
    }

    [TestMethod]
    [TestCategory("Unit")]
    public void FormatAndParse_OverlongSegment_CreatesStableExplicitOverlappingExcerpts()
    {
        var text = string.Concat(Enumerable.Repeat("0123456789", 310));
        var formatted = new AzureOpenAIVectorStoreCorpusFormatter().Format(CreateDocument(), CreateMarkdown(text), Fingerprint);

        var segments = new AzureOpenAIVectorStoreCorpusParser().Parse(formatted.Attributes, [Encoding.UTF8.GetString(formatted.Content)], Fingerprint);

        Assert.AreEqual(3, formatted.SearchRecordCount);
        Assert.HasCount(3, segments);
        Assert.IsTrue(segments.All(segment => segment.IsExcerpt));
        CollectionAssert.AreEqual(new[] { 0, 1_200, 2_400 }, segments.Select(segment => segment.ExcerptStart).ToArray());
        Assert.IsTrue(segments.All(segment => segment.OriginalCharacterCount == text.Length));
        Assert.AreEqual($"{CreateDocument().DocumentId}:segment:00000001:excerpt:0003", segments[2].SegmentId);
        Assert.AreEqual(text[2_400..], segments[2].Text);
    }

    [TestMethod]
    [TestCategory("Unit")]
    public void Parse_PartialRecord_IgnoresUnsafeFragment()
    {
        var formatted = new AzureOpenAIVectorStoreCorpusFormatter().Format(CreateDocument(), CreateMarkdown("Complete source text."), Fingerprint);
        var content = Encoding.UTF8.GetString(formatted.Content);
        var partial = content[..content.IndexOf("ASKARABBI_SEGMENT_V1_END", StringComparison.Ordinal)];

        var segments = new AzureOpenAIVectorStoreCorpusParser().Parse(formatted.Attributes, [partial], Fingerprint);

        Assert.HasCount(0, segments);
    }

    [TestMethod]
    [TestCategory("Unit")]
    public void Parse_WrongFingerprint_RejectsEntireResult()
    {
        var formatted = new AzureOpenAIVectorStoreCorpusFormatter().Format(CreateDocument(), CreateMarkdown("Complete source text."), Fingerprint);

        var exception = Assert.ThrowsExactly<InvalidDataException>(() => new AzureOpenAIVectorStoreCorpusParser().Parse(formatted.Attributes, [Encoding.UTF8.GetString(formatted.Content)], new string('c', 64)));

        StringAssert.Contains(exception.Message, "fingerprint");
    }

    [TestMethod]
    [TestCategory("Unit")]
    public void Parse_UnsupportedLicense_RejectsEntireResult()
    {
        var formatted = new AzureOpenAIVectorStoreCorpusFormatter().Format(CreateDocument(), CreateMarkdown("Complete source text."), Fingerprint);
        var attributes = formatted.Attributes.ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.Ordinal);
        attributes["license"] = "CC-BY-NC";

        var exception = Assert.ThrowsExactly<InvalidDataException>(() => new AzureOpenAIVectorStoreCorpusParser().Parse(attributes, [Encoding.UTF8.GetString(formatted.Content)], Fingerprint));

        StringAssert.Contains(exception.Message, "approved source license");
    }

    [TestMethod]
    [TestCategory("Unit")]
    public void Parse_ProviderOmitsAttributes_UsesTrustedManifestProvenance()
    {
        var document = CreateDocument();
        var manifest = TestManifestFactory.CreateManifest(document);
        var formatted = new AzureOpenAIVectorStoreCorpusFormatter().Format(document, CreateMarkdown("Complete source text."), Fingerprint);

        var segments = new AzureOpenAIVectorStoreCorpusParser(manifest).Parse(new Dictionary<string, string>(), [Encoding.UTF8.GetString(formatted.Content)], Fingerprint);

        Assert.HasCount(1, segments);
        Assert.AreEqual(document.DocumentId, segments[0].DocumentId);
        Assert.AreEqual(document.FileTitle, segments[0].Title);
        Assert.AreEqual(document.License, segments[0].License);
    }

    [TestMethod]
    [TestCategory("Unit")]
    public void Parse_ProviderOmitsAttributesAndDocumentIsUnknown_RejectsResult()
    {
        var formattedDocument = TestManifestFactory.CreateDocument(segmentCount: 1, firstReference: "Genesis 1:1", lastReference: "Genesis 1:1", rawSha256: new string('b', 64));
        var trustedManifest = TestManifestFactory.CreateManifest(CreateDocument());
        var formatted = new AzureOpenAIVectorStoreCorpusFormatter().Format(formattedDocument, CreateMarkdown("Complete source text."), Fingerprint);

        var exception = Assert.ThrowsExactly<InvalidDataException>(() => new AzureOpenAIVectorStoreCorpusParser(trustedManifest).Parse(new Dictionary<string, string>(), [Encoding.UTF8.GetString(formatted.Content)], Fingerprint));

        StringAssert.Contains(exception.Message, "unknown manifest document");
    }

    [TestMethod]
    [TestCategory("Unit")]
    public void FormatParts_LargeDocument_ProducesDeterministicBoundedFilesWithoutLosingSegments()
    {
        const int segmentCount = 220;
        var document = TestManifestFactory.CreateDocument(segmentCount: segmentCount, firstReference: "Genesis 1:1", lastReference: $"Genesis 1:{segmentCount}");
        var markdown = new StringBuilder("# Genesis\n\n");
        for (var index = 1; index <= segmentCount; index++)
        {
            markdown.Append("## Genesis 1:").Append(index).Append('\n').Append('x', 1_000).Append("\n\n");
        }

        var parts = new AzureOpenAIVectorStoreCorpusFormatter().FormatParts(document, markdown.ToString(), Fingerprint);

        Assert.IsGreaterThan(1, parts.Count);
        Assert.IsTrue(parts.All(part => part.Content.Length <= AzureOpenAIVectorStoreCorpusFormatter.MaximumUploadBytes));
        Assert.AreEqual(segmentCount, parts.Sum(part => part.SourceSegmentCount));
        Assert.AreEqual(segmentCount, parts.Sum(part => part.SearchRecordCount));
        Assert.AreEqual("sefaria-aaaaaaaaaaaaaaaa-part-0001.md", parts[0].FileName);
        Assert.AreEqual($"sefaria-aaaaaaaaaaaaaaaa-part-{parts.Count:D4}.md", parts[^1].FileName);
    }

    [TestMethod]
    [TestCategory("Unit")]
    public void Format_LargeDocument_RequiresMultiPartApi()
    {
        const int segmentCount = 220;
        var document = TestManifestFactory.CreateDocument(segmentCount: segmentCount, firstReference: "Genesis 1:1", lastReference: $"Genesis 1:{segmentCount}");
        var markdown = new StringBuilder("# Genesis\n\n");
        for (var index = 1; index <= segmentCount; index++)
        {
            markdown.Append("## Genesis 1:").Append(index).Append('\n').Append('x', 1_000).Append("\n\n");
        }

        var exception = Assert.ThrowsExactly<InvalidOperationException>(() => new AzureOpenAIVectorStoreCorpusFormatter().Format(document, markdown.ToString(), Fingerprint));

        StringAssert.Contains(exception.Message, "FormatParts");
    }

    [TestMethod]
    [TestCategory("Unit")]
    public void Parse_ProviderAttributeConflictsWithManifest_RejectsResult()
    {
        var document = CreateDocument();
        var formatted = new AzureOpenAIVectorStoreCorpusFormatter().Format(document, CreateMarkdown("Complete source text."), Fingerprint);
        var attributes = new Dictionary<string, string> { ["documentId"] = "sefaria:wrong" };

        var exception = Assert.ThrowsExactly<InvalidDataException>(() => new AzureOpenAIVectorStoreCorpusParser(TestManifestFactory.CreateManifest(document)).Parse(attributes, [Encoding.UTF8.GetString(formatted.Content)], Fingerprint));

        StringAssert.Contains(exception.Message, "conflicts");
    }

    [TestMethod]
    [DataRow("schema")]
    [DataRow("provider")]
    [DataRow("count")]
    [DataRow("duplicate")]
    [TestCategory("Unit")]
    public void CorpusParser_InvalidManifest_Throws(string scenario)
    {
        var document = CreateDocument();
        var manifest = scenario switch
        {
            "schema" => TestManifestFactory.CreateManifest(document) with { SchemaVersion = "old" },
            "provider" => TestManifestFactory.CreateManifest(document) with { SourceProvider = "Other" },
            "count" => TestManifestFactory.CreateManifest(document) with { DocumentCount = 2 },
            "duplicate" => TestManifestFactory.CreateManifest(document, document),
            _ => throw new AssertFailedException($"Unknown scenario '{scenario}'."),
        };

        Assert.ThrowsExactly<ArgumentException>(() => new AzureOpenAIVectorStoreCorpusParser(manifest));
    }

    [TestMethod]
    [TestCategory("Unit")]
    public void CreateLookupToken_NegativeOrdinal_Throws()
    {
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => AzureOpenAIVectorStoreCorpusFormatter.CreateLookupToken("sefaria:test", -1));
    }

    [TestMethod]
    [TestCategory("Unit")]
    public void Format_ReservedMarkerInSource_Throws()
    {
        var markdown = CreateMarkdown("Text containing ASKARABBI_SEGMENT_V1_END as data.");

        Assert.ThrowsExactly<InvalidDataException>(() => new AzureOpenAIVectorStoreCorpusFormatter().Format(CreateDocument(), markdown, Fingerprint));
    }

    [TestMethod]
    [DataRow("client-endpoint")]
    [DataRow("client-timeout")]
    [DataRow("retriever-id")]
    [DataRow("retriever-fingerprint")]
    [DataRow("retriever-score")]
    [TestCategory("Unit")]
    public void Validate_InvalidVectorStoreOptions_Throws(string scenario)
    {
        Action action = scenario switch
        {
            "client-endpoint" => () => new AzureOpenAIVectorStoreClientOptions { ProjectEndpoint = new Uri("http://example.test") }.Validate(),
            "client-timeout" => () => new AzureOpenAIVectorStoreClientOptions { ProjectEndpoint = new Uri("https://example.test"), Timeout = TimeSpan.Zero }.Validate(),
            "retriever-id" => () => new AzureOpenAIVectorStoreRetrieverOptions { VectorStoreId = " ", ExpectedCorpusFingerprint = Fingerprint }.Validate(),
            "retriever-fingerprint" => () => new AzureOpenAIVectorStoreRetrieverOptions { VectorStoreId = "vs_test", ExpectedCorpusFingerprint = "ABC" }.Validate(),
            "retriever-score" => () => new AzureOpenAIVectorStoreRetrieverOptions { VectorStoreId = "vs_test", ExpectedCorpusFingerprint = Fingerprint, ScoreThreshold = 1.1 }.Validate(),
            _ => throw new AssertFailedException($"Unknown scenario '{scenario}'."),
        };

        if (scenario is "client-timeout" or "retriever-score")
        {
            Assert.ThrowsExactly<ArgumentOutOfRangeException>(action);
        }
        else
        {
            Assert.ThrowsExactly<ArgumentException>(action);
        }
    }

    private static AskARabbiLIB.Models.ManifestDocument CreateDocument() => TestManifestFactory.CreateDocument(segmentCount: 1, firstReference: "Genesis 1:1", lastReference: "Genesis 1:1");

    private static string CreateMarkdown(string text) => $"""
        ---
        source: test
        ---
        # Genesis

        ## Genesis 1:1
        {text}
        """;
}
