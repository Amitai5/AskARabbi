using System.IO.Compression;
using System.Text;
using AskARabbiLIB.Models;
using AskARabbiLIB.Retrieval;
using Microsoft.Data.Sqlite;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace AskARabbiLIB.Tests;

[TestClass]
public sealed class BundledNormalizedDocumentProviderTests
{
    private const string Markdown = "# Shulchan Arukh, Orach Chayim\n\n## Shulchan Arukh, Orach Chayim 583:1\n\nCustomary foods for Rosh Hashanah. " + SourceResearchTestData.Quotation;

    [TestMethod]
    public async Task LoadAsync_ApprovedArchive_PreservesOriginalTextAndSelectsOnlyBundledDocuments()
    {
        var (provider, document) = Create();

        var text = await provider.LoadAsync(document);

        Assert.AreEqual(Markdown, text);
        Assert.HasCount(1, provider.Manifest.Documents);
        Assert.AreEqual(1, provider.Manifest.DocumentCount);
    }

    [TestMethod]
    [DataRow("changed")]
    [DataRow("size")]
    public async Task LoadAsync_InvalidDocumentIntegrity_RejectsContent(string failure)
    {
        var (provider, document) = Create(failure);

        await Assert.ThrowsExactlyAsync<InvalidDataException>(() => provider.LoadAsync(document));
    }

    [TestMethod]
    public async Task LoadAsync_UnapprovedDocument_RejectsRequest()
    {
        var (provider, document) = Create();

        await Assert.ThrowsExactlyAsync<ArgumentException>(() => provider.LoadAsync(document with { DocumentId = "unapproved" }));
    }

    [TestMethod]
    public async Task LoadAsync_CanceledRequest_PropagatesCancellation()
    {
        var (provider, document) = Create();

        await Assert.ThrowsAsync<OperationCanceledException>(() => provider.LoadAsync(document, new CancellationToken(true)));
    }

    [TestMethod]
    public void Constructor_NoApprovedArchiveEntries_RejectsArchive()
    {
        using var buffer = new MemoryStream();
        using (new ZipArchive(buffer, ZipArchiveMode.Create, true))
        {
        }
        var bytes = buffer.ToArray();

        Assert.ThrowsExactly<InvalidDataException>(() => new BundledNormalizedDocumentProvider(TestManifestFactory.CreateManifest(), () => new MemoryStream(bytes, false)));
    }

    [TestMethod]
    [DataRow("squash")]
    [DataRow("gourd")]
    [DataRow("pumpkin")]
    public async Task BuildAndSearchAsync_AlternateFoodNames_ReturnsOriginalPrayerWithProvenance(string food)
    {
        var (provider, document) = Create();
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await new SourceIndexBuilder().BuildAsync(provider.Manifest, provider, connection);
        await using var retriever = new SqliteSourceRetriever(connection, provider.Manifest);

        var hits = await retriever.SearchAsync(new SourceRetrievalQuery { QueryText = $"{food} Rosh Hashanah prayer", SourceKeys = ["work:shulchan_arukh_with_rema"], Languages = ["English"] });

        Assert.HasCount(1, hits);
        Assert.AreEqual(SourceResearchTestData.Reference, hits[0].Segment.CanonicalReference);
        StringAssert.Contains(hits[0].Segment.Text, SourceResearchTestData.Quotation);
        Assert.AreEqual(document.DocumentId, hits[0].Segment.DocumentId);
        Assert.AreEqual(document.License, hits[0].Segment.License);
        Assert.AreEqual(document.VersionTitle, hits[0].Segment.Version);
    }

    [TestMethod]
    [DataRow("collection:Torah", "English")]
    [DataRow("work:shulchan_arukh_with_rema", "Hebrew")]
    public async Task BuildAndSearchAsync_ExcludedSourceOrLanguage_ReturnsNoHits(string sourceKey, string language)
    {
        var (provider, _) = Create();
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await new SourceIndexBuilder().BuildAsync(provider.Manifest, provider, connection);
        await using var retriever = new SqliteSourceRetriever(connection, provider.Manifest);

        var hits = await retriever.SearchAsync(new SourceRetrievalQuery { QueryText = SourceResearchTestData.Question, SourceKeys = [sourceKey], Languages = [language] });

        Assert.HasCount(0, hits);
    }

    private static (BundledNormalizedDocumentProvider Provider, ManifestDocument Document) Create(string? failure = null)
    {
        var content = Encoding.UTF8.GetBytes(Markdown);
        var document = TestManifestFactory.CreateDocument(title: "Shulchan Arukh, Orach Chayim", collection: "Halakhah", categories: ["Halakhah"], segmentCount: 1, firstReference: SourceResearchTestData.Reference, lastReference: SourceResearchTestData.Reference, sha256: TestManifestFactory.Sha256(content)) with
        {
            FileSizeBytes = content.Length + (failure == "size" ? 1 : 0), WorkKey = "shulchan_arukh_with_rema", UsageNote = "Base text and glosses have distinct attribution.",
        };
        using var buffer = new MemoryStream();
        using (var archive = new ZipArchive(buffer, ZipArchiveMode.Create, true))
        {
            using var stream = archive.CreateEntry(document.Sha256 + ".md").Open();
            stream.Write(failure == "changed" ? Encoding.UTF8.GetBytes(Markdown.Replace("God", "god", StringComparison.Ordinal)) : content);
        }
        var bytes = buffer.ToArray();
        var unavailable = TestManifestFactory.CreateDocument(rawSha256: new string('b', 64));
        return (new BundledNormalizedDocumentProvider(TestManifestFactory.CreateManifest(document, unavailable), () => new MemoryStream(bytes, false)), document);
    }
}
