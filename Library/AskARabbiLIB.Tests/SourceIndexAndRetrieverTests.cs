using AskARabbiLIB.Models;
using AskARabbiLIB.Retrieval;
using Microsoft.Data.Sqlite;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace AskARabbiLIB.Tests;

[TestClass]
public sealed class SourceIndexAndRetrieverTests
{
    [TestMethod]
    [TestCategory("Unit")]
    public async Task BuildAndVerifyAsync_ValidCorpus_StoresCountsAndFingerprint()
    {
        // Arrange
        var fixture = CreateFixture();
        await using var connection = new SqliteConnection("Data Source=:memory:");
        var builder = new SourceIndexBuilder();

        // Act
        var statistics = await builder.BuildAsync(fixture.Manifest, fixture.Provider, connection);
        var verification = await builder.VerifyAsync(connection, fixture.Manifest);

        // Assert
        Assert.AreEqual(SourceIndexBuilder.IndexSchemaVersion, statistics.SchemaVersion);
        Assert.AreEqual(3, statistics.DocumentCount);
        Assert.AreEqual(5L, statistics.SegmentCount);
        Assert.IsTrue(verification.IsValid);
        Assert.IsNotNull(verification.Statistics);
        Assert.AreEqual(statistics.CorpusFingerprint, verification.Statistics.CorpusFingerprint);
    }

    [TestMethod]
    [TestCategory("Unit")]
    public async Task VerifyAsync_ChangedManifestFingerprint_ReturnsStale()
    {
        // Arrange
        var fixture = CreateFixture();
        await using var connection = new SqliteConnection("Data Source=:memory:");
        var builder = new SourceIndexBuilder();
        await builder.BuildAsync(fixture.Manifest, fixture.Provider, connection);
        var changed = fixture.Manifest with
        {
            SourceManifests = fixture.Manifest.SourceManifests with { NormalizedSha256 = new string('d', 64) },
        };

        // Act
        var result = await builder.VerifyAsync(connection, changed);

        // Assert
        Assert.IsFalse(result.IsValid);
        StringAssert.Contains(result.Message, "fingerprint");
    }

    [TestMethod]
    [TestCategory("Unit")]
    public async Task SearchAsync_ExactReference_ReturnsBilingualVersions()
    {
        // Arrange
        var fixture = CreateFixture();
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await new SourceIndexBuilder().BuildAsync(fixture.Manifest, fixture.Provider, connection);
        await using var retriever = new SqliteSourceRetriever(connection, fixture.Manifest);

        // Act
        var hits = await retriever.SearchAsync(new SourceRetrievalQuery { ExactCanonicalReference = "Shabbat 20a:1", CandidateLimit = 10 });

        // Assert
        Assert.HasCount(2, hits);
        Assert.IsTrue(hits.All(hit => hit.IsExactReference));
        CollectionAssert.AreEquivalent(new[] { "en", "he" }, hits.Select(hit => hit.Segment.LanguageCode).ToArray());
    }

    [TestMethod]
    [TestCategory("Unit")]
    public async Task SearchAsync_KeywordAndFilters_RanksMatchingApprovedSegment()
    {
        // Arrange
        var fixture = CreateFixture();
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await new SourceIndexBuilder().BuildAsync(fixture.Manifest, fixture.Provider, connection);
        await using var retriever = new SqliteSourceRetriever(connection, fixture.Manifest);

        // Act
        var hits = await retriever.SearchAsync(new SourceRetrievalQuery
        {
            QueryText = "kindled lamp",
            Languages = new[] { "English" },
            Collections = new[] { "Talmud" },
            Categories = new[] { "Seder Moed" },
        });

        // Assert
        Assert.IsTrue(hits.Count > 0);
        Assert.AreEqual("Shabbat 20a:1", hits[0].Segment.CanonicalReference);
        Assert.AreEqual("English", hits[0].Segment.Language);
        Assert.AreEqual("Talmud", hits[0].Segment.Collection);
        Assert.AreEqual(SourceLicenseCategory.CcBy, hits[0].Segment.LicenseCategory);
        Assert.AreEqual(fixture.Manifest.Documents.Single(document => document.DocumentId == hits[0].Segment.DocumentId).AttributionUrl, hits[0].Segment.SourceUrl);
    }

    [TestMethod]
    [TestCategory("Unit")]
    public async Task SearchAsync_WorkFilter_RoundTripsSupplementalMetadata()
    {
        // Arrange
        var fixture = CreateFixture();
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await new SourceIndexBuilder().BuildAsync(fixture.Manifest, fixture.Provider, connection);
        await using var retriever = new SqliteSourceRetriever(connection, fixture.Manifest);

        // Act
        var hits = await retriever.SearchAsync(new SourceRetrievalQuery { QueryText = "kindled lamp", WorkKeys = new[] { "test_work" } });

        // Assert
        Assert.IsTrue(hits.Count > 0);
        Assert.IsTrue(hits.All(hit => string.Equals(hit.Segment.WorkKey, "test_work", StringComparison.Ordinal)));
        Assert.IsTrue(hits.All(hit => string.Equals(hit.Segment.UsageNote, "Use this test work with its supplied interpretive limitation.", StringComparison.Ordinal)));
    }

    [TestMethod]
    [DataRow("work:test_work", "en")]
    [DataRow("collection:Talmud", "he")]
    [TestCategory("Unit")]
    public async Task SearchAsync_SourceKey_SelectsExclusiveLogicalSource(string sourceKey, string expectedLanguageCode)
    {
        // Arrange
        var fixture = CreateFixture();
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await new SourceIndexBuilder().BuildAsync(fixture.Manifest, fixture.Provider, connection);
        await using var retriever = new SqliteSourceRetriever(connection, fixture.Manifest);

        // Act
        var hits = await retriever.SearchAsync(new SourceRetrievalQuery { ExactCanonicalReference = "Shabbat 20a:1", SourceKeys = new[] { sourceKey } });

        // Assert
        Assert.HasCount(1, hits);
        Assert.AreEqual(expectedLanguageCode, hits[0].Segment.LanguageCode);
    }

    [TestMethod]
    [TestCategory("Unit")]
    public async Task SearchAsync_InvalidSourceKey_ThrowsArgumentException()
    {
        // Arrange
        var fixture = CreateFixture();
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await new SourceIndexBuilder().BuildAsync(fixture.Manifest, fixture.Provider, connection);
        await using var retriever = new SqliteSourceRetriever(connection, fixture.Manifest);

        // Act
        async Task ActAsync() => await retriever.SearchAsync(new SourceRetrievalQuery { QueryText = "lamp", SourceKeys = new[] { "zohar" } });

        // Assert
        await Assert.ThrowsExactlyAsync<ArgumentException>(ActAsync);
    }

    [TestMethod]
    [TestCategory("Unit")]
    public async Task SearchAsync_HebrewWithNiqqud_NormalizesUnicodeForFts()
    {
        // Arrange
        var fixture = CreateFixture();
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await new SourceIndexBuilder().BuildAsync(fixture.Manifest, fixture.Provider, connection);
        await using var retriever = new SqliteSourceRetriever(connection, fixture.Manifest);

        // Act
        var hits = await retriever.SearchAsync(new SourceRetrievalQuery { QueryText = "אור הנר", Languages = new[] { "he" } });

        // Assert
        Assert.IsTrue(hits.Count > 0);
        Assert.AreEqual("he", hits[0].Segment.LanguageCode);
        StringAssert.Contains(hits[0].Segment.Text, "אוֹר הַנֵּר");
    }

    [TestMethod]
    [TestCategory("Unit")]
    public async Task GetContextAsync_RadiusOne_ReturnsOrderedNeighbors()
    {
        // Arrange
        var fixture = CreateFixture();
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await new SourceIndexBuilder().BuildAsync(fixture.Manifest, fixture.Provider, connection);
        await using var retriever = new SqliteSourceRetriever(connection, fixture.Manifest);
        var englishDocument = fixture.Manifest.Documents.Single(document => document.FileLanguageCode == "en" && document.Collection == "Talmud");

        // Act
        var segments = await retriever.GetContextAsync(englishDocument.DocumentId, 1, 1);

        // Assert
        Assert.HasCount(2, segments);
        CollectionAssert.AreEqual(new[] { 0, 1 }, segments.Select(segment => segment.DocumentOrdinal).ToArray());
    }

    [TestMethod]
    [TestCategory("Unit")]
    public async Task SearchAsync_OnlyQuestionStopWords_ReturnsNoCandidates()
    {
        // Arrange
        var fixture = CreateFixture();
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await new SourceIndexBuilder().BuildAsync(fixture.Manifest, fixture.Provider, connection);
        await using var retriever = new SqliteSourceRetriever(connection, fixture.Manifest);

        // Act
        var hits = await retriever.SearchAsync(new SourceRetrievalQuery { QueryText = "what does the text say about this" });

        // Assert
        Assert.HasCount(0, hits);
    }

    [TestMethod]
    [TestCategory("Unit")]
    public async Task SearchAsync_NaturalDietaryQuestion_RanksWholeExpandedConceptAboveIncidentalWords()
    {
        // Arrange
        var fixture = CreateDietaryFixture();
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await new SourceIndexBuilder().BuildAsync(fixture.Manifest, fixture.Provider, connection);
        await using var retriever = new SqliteSourceRetriever(connection, fixture.Manifest);

        // Act
        var hits = await retriever.SearchAsync(new SourceRetrievalQuery { QueryText = "Why do many Jewish communities avoid mixing chicken and milk?" });

        // Assert
        Assert.IsTrue(hits.Count >= 2);
        Assert.AreEqual("Mishnah Chullin 8:1", hits[0].Segment.CanonicalReference);
        StringAssert.Contains(hits[0].Segment.Text, "Fowl and cheese");
        Assert.IsTrue(hits[0].Score > hits.Single(hit => hit.Segment.CanonicalReference == "Mishnah Meilah 3:5").Score);
    }

    [TestMethod]
    [TestCategory("Regression")]
    public void Plan_ServerQuestion_PrioritizesMappedConceptsAndPreservesShabbatAnchor()
    {
        // Act
        var plan = RetrievalQueryPlanner.Plan("If I have a server and my server runs on Saturday for my business, but I do not operate it, it just automatically comes on");

        // Assert
        Assert.IsNotNull(plan.TopicAnchor);
        Assert.AreEqual("shabbat", plan.TopicAnchor.Key);
        CollectionAssert.IsSubsetOf(new[] { "automation", "business", "technology" }, plan.SupportingConcepts.Select(concept => concept.Key).ToArray());
        Assert.IsFalse(plan.Concepts.Any(concept => concept.Key is "if" or "my" or "but"));
    }

    [TestMethod]
    [TestCategory("Regression")]
    public async Task SearchAsync_ShabbatServerQuestion_UsesOnlyTopicAnchoredFallbacks()
    {
        // Arrange
        var fixture = CreateShabbatAutomationFixture();
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await new SourceIndexBuilder().BuildAsync(fixture.Manifest, fixture.Provider, connection);
        await using var retriever = new SqliteSourceRetriever(connection, fixture.Manifest);

        // Act
        var hits = await retriever.SearchAsync(new SourceRetrievalQuery { QueryText = "If my business server runs automatically on Saturday, is that allowed?", CandidateLimit = 10 });

        // Assert
        CollectionAssert.AreEquivalent(new[] { "Shulchan Arukh, Orach Chayim 252:1", "Shulchan Arukh, Orach Chayim 245:1" }, hits.Select(hit => hit.Segment.CanonicalReference).ToArray());
        Assert.IsFalse(hits.Any(hit => hit.Segment.CanonicalReference == "Jerusalem Talmud Ketubot 9:4:1"));
    }

    [TestMethod]
    [DataRow("candidateLow")]
    [DataRow("candidateHigh")]
    [DataRow("missingText")]
    [DataRow("punctuationOnly")]
    [DataRow("nullLanguages")]
    [DataRow("blankCollection")]
    [DataRow("nullCategories")]
    [DataRow("blankWorkKey")]
    [DataRow("nullSourceKeys")]
    [DataRow("invalidSourceKey")]
    [TestCategory("Unit")]
    public void SearchAsync_InvalidQuery_ThrowsBeforeOpeningIndex(string scenario)
    {
        // Arrange
        var fixture = CreateFixture();
        using var connection = new SqliteConnection("Data Source=:memory:");
        var retriever = new SqliteSourceRetriever(connection, fixture.Manifest);
        var query = scenario switch
        {
            "candidateLow" => new SourceRetrievalQuery { QueryText = "lamp", CandidateLimit = 0 },
            "candidateHigh" => new SourceRetrievalQuery { QueryText = "lamp", CandidateLimit = 201 },
            "missingText" => new SourceRetrievalQuery(),
            "punctuationOnly" => new SourceRetrievalQuery { QueryText = "?!" },
            "nullLanguages" => new SourceRetrievalQuery { QueryText = "lamp", Languages = null! },
            "blankCollection" => new SourceRetrievalQuery { QueryText = "lamp", Collections = [" "] },
            "nullCategories" => new SourceRetrievalQuery { QueryText = "lamp", Categories = null! },
            "blankWorkKey" => new SourceRetrievalQuery { QueryText = "lamp", WorkKeys = [""] },
            "nullSourceKeys" => new SourceRetrievalQuery { QueryText = "lamp", SourceKeys = null! },
            "invalidSourceKey" => new SourceRetrievalQuery { QueryText = "lamp", SourceKeys = ["invalid"] },
            _ => throw new AssertFailedException($"Unknown query scenario '{scenario}'."),
        };

        // Act and assert
        if (scenario is "candidateLow" or "candidateHigh")
        {
            Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => retriever.SearchAsync(query));
        }
        else
        {
            Assert.ThrowsExactly<ArgumentException>(() => retriever.SearchAsync(query));
        }
    }

    [TestMethod]
    [TestCategory("Unit")]
    public async Task ConstructorsContextValidationAndDisposal_RejectInvalidUsage()
    {
        // Arrange
        var fixture = CreateFixture();
        using var connection = new SqliteConnection("Data Source=:memory:");

        // Act and assert
        Assert.ThrowsExactly<ArgumentException>(() => new SqliteSourceRetriever(" ", fixture.Manifest));
        Assert.ThrowsExactly<ArgumentNullException>(() => new SqliteSourceRetriever("index.sqlite", null!));
        Assert.ThrowsExactly<ArgumentNullException>(() => new SqliteSourceRetriever((SqliteConnection)null!, fixture.Manifest));
        Assert.ThrowsExactly<ArgumentNullException>(() => new SqliteSourceRetriever(connection, null!));

        await using var diskRetriever = new SqliteSourceRetriever("index.sqlite", fixture.Manifest);
        var retriever = new SqliteSourceRetriever(connection, fixture.Manifest);
        Assert.ThrowsExactly<ArgumentException>(() => retriever.GetContextAsync(" ", 0, 0));
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => retriever.GetContextAsync("document", -1, 0));
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => retriever.GetContextAsync("document", 0, -1));
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => retriever.GetContextAsync("document", 0, 11));

        await retriever.DisposeAsync();
        await retriever.DisposeAsync();
        await Assert.ThrowsExactlyAsync<ObjectDisposedException>(() => retriever.SearchAsync(new SourceRetrievalQuery { QueryText = "lamp" }));
    }

    private static CorpusFixture CreateFixture()
    {
        var english = TestManifestFactory.CreateDocument(
            title: "Shabbat",
            hebrewTitle: "שבת",
            language: "English",
            languageCode: "en",
            collection: "Talmud",
            categories: new[] { "Talmud", "Bavli", "Seder Moed" },
            versionTitle: "English Test",
            segmentCount: 2,
            firstReference: "Shabbat 20a:1",
            lastReference: "Shabbat 20a:2") with
        {
            WorkKey = "test_work",
            UsageNote = "Use this test work with its supplied interpretive limitation.",
        };
        var hebrew = TestManifestFactory.CreateDocument(
            title: "Shabbat",
            hebrewTitle: "שבת",
            language: "Hebrew",
            languageCode: "he",
            collection: "Talmud",
            categories: new[] { "Talmud", "Bavli", "Seder Moed" },
            versionTitle: "Hebrew Test",
            segmentCount: 2,
            firstReference: "Shabbat 20a:1",
            lastReference: "Shabbat 20a:2",
            rawSha256: new string('b', 64));
        var mishnah = TestManifestFactory.CreateDocument(
            title: "Mishnah Berakhot",
            hebrewTitle: "משנה ברכות",
            language: "English",
            languageCode: "en",
            collection: "Mishnah",
            categories: new[] { "Mishnah", "Seder Zeraim" },
            versionTitle: "Mishnah Test",
            segmentCount: 1,
            firstReference: "Mishnah Berakhot 1:1",
            lastReference: "Mishnah Berakhot 1:1",
            rawSha256: new string('c', 64));
        var manifest = TestManifestFactory.CreateManifest(english, hebrew, mishnah);
        var content = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            [english.DocumentId] = Markdown("Shabbat", ("Shabbat 20a:1", "A lamp may not be kindled shortly before Shabbat."), ("Shabbat 20a:2", "The flame must catch before nightfall.")),
            [hebrew.DocumentId] = Markdown("Shabbat", ("Shabbat 20a:1", "אוֹר הַנֵּר דולק."), ("Shabbat 20a:2", "השלהבת עולה.")),
            [mishnah.DocumentId] = Markdown("Mishnah Berakhot", ("Mishnah Berakhot 1:1", "The evening Shema is recited from the time the priests enter.")),
        };
        return new CorpusFixture(manifest, new DictionaryDocumentProvider(content));
    }

    private static CorpusFixture CreateDietaryFixture()
    {
        var relevant = TestManifestFactory.CreateDocument(
            title: "Mishnah Chullin",
            hebrewTitle: "משנה חולין",
            language: "English",
            languageCode: "en",
            collection: "Mishnah",
            categories: new[] { "Mishnah", "Seder Kodashim" },
            segmentCount: 1,
            firstReference: "Mishnah Chullin 8:1",
            lastReference: "Mishnah Chullin 8:1",
            rawSha256: new string('d', 64));
        var incidental = TestManifestFactory.CreateDocument(
            title: "Mishnah Meilah",
            hebrewTitle: "משנה מעילה",
            language: "English",
            languageCode: "en",
            collection: "Mishnah",
            categories: new[] { "Mishnah", "Seder Kodashim" },
            segmentCount: 1,
            firstReference: "Mishnah Meilah 3:5",
            lastReference: "Mishnah Meilah 3:5",
            rawSha256: new string('e', 64));
        var manifest = TestManifestFactory.CreateManifest(relevant, incidental);
        var content = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            [relevant.DocumentId] = Markdown("Mishnah Chullin", ("Mishnah Chullin 8:1", "Fowl and cheese may not be brought together on the table or eaten together.")),
            [incidental.DocumentId] = Markdown("Mishnah Meilah", ("Mishnah Meilah 3:5", "The milk of a consecrated animal and the eggs of a consecrated chicken are discussed.")),
        };
        return new CorpusFixture(manifest, new DictionaryDocumentProvider(content));
    }

    private static CorpusFixture CreateShabbatAutomationFixture()
    {
        var automation = TestManifestFactory.CreateDocument(
            title: "Shulchan Arukh, Orach Chayim",
            hebrewTitle: "שולחן ערוך אורח חיים",
            language: "English",
            languageCode: "en",
            collection: "Halakhah",
            categories: new[] { "Halakhah", "Shulchan Arukh", "Shabbat" },
            segmentCount: 1,
            firstReference: "Shulchan Arukh, Orach Chayim 252:1",
            lastReference: "Shulchan Arukh, Orach Chayim 252:1",
            rawSha256: new string('f', 64));
        var business = TestManifestFactory.CreateDocument(
            title: "Shulchan Arukh, Orach Chayim",
            hebrewTitle: "שולחן ערוך אורח חיים",
            language: "English",
            languageCode: "en",
            collection: "Halakhah",
            categories: new[] { "Halakhah", "Shulchan Arukh", "Shabbat" },
            segmentCount: 1,
            firstReference: "Shulchan Arukh, Orach Chayim 245:1",
            lastReference: "Shulchan Arukh, Orach Chayim 245:1",
            rawSha256: new string('1', 64));
        var tangential = TestManifestFactory.CreateDocument(
            title: "Jerusalem Talmud Ketubot",
            hebrewTitle: "תלמוד ירושלמי כתובות",
            language: "English",
            languageCode: "en",
            collection: "Talmud",
            categories: new[] { "Talmud", "Yerushalmi", "Seder Nashim" },
            segmentCount: 1,
            firstReference: "Jerusalem Talmud Ketubot 9:4:1",
            lastReference: "Jerusalem Talmud Ketubot 9:4:1",
            rawSha256: new string('2', 64));
        var manifest = TestManifestFactory.CreateManifest(automation, business, tangential);
        var content = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            [automation.DocumentId] = Markdown(automation.FileTitle, ("Shulchan Arukh, Orach Chayim 252:1", "On Friday, one may open a water channel so that water continues to flow throughout Shabbat without a new human action.")),
            [business.DocumentId] = Markdown(business.FileTitle, ("Shulchan Arukh, Orach Chayim 245:1", "A business partnership arrangement must account for profit earned from work performed on Shabbat.")),
            [tangential.DocumentId] = Markdown(tangential.FileTitle, ("Jerusalem Talmud Ketubot 9:4:1", "A steward may run a business and account for its profits.")),
        };
        return new CorpusFixture(manifest, new DictionaryDocumentProvider(content));
    }

    private static string Markdown(string title, params (string Reference, string Text)[] segments) => $"# {title}\n\n" + string.Join("\n\n", segments.Select(segment => $"## {segment.Reference}\n\n{segment.Text}"));

    private sealed record CorpusFixture(DocumentManifest Manifest, INormalizedDocumentProvider Provider);

    private sealed class DictionaryDocumentProvider : INormalizedDocumentProvider
    {
        private readonly IReadOnlyDictionary<string, string> content;

        internal DictionaryDocumentProvider(IReadOnlyDictionary<string, string> content)
        {
            this.content = content;
        }

        public Task<string> LoadAsync(ManifestDocument document, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(content[document.DocumentId]);
        }
    }
}
