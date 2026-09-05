using System.IO.Compression;
using System.Text;
using AskARabbiLIB.Grounding;
using AskARabbiLIB.Models;
using AskARabbiLIB.Retrieval;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace AskARabbiLIB.Tests;

[TestClass]
public sealed class BundledCanonicalSourceReaderTests
{
    private const string Markdown = "# Genesis\n\n## Genesis 1:1\n\nIn the beginning God created the heaven and the earth.\n\n## Genesis 1:2\n\nThe earth was unformed and void.";

    [TestMethod]
    [TestCategory("Regression")]
    public async Task ReadAsync_ExactVerse_ReturnsOnlyRequestedVerseWithStableProvenance()
    {
        var (reader, document) = CreateReader();

        var segments = await reader.ReadAsync("Genesis 1:2", new SourceRetrievalQuery { Languages = ["English"], SourceKeys = ["collection:Torah"] });

        Assert.HasCount(1, segments);
        Assert.AreEqual("Genesis 1:2", segments[0].CanonicalReference);
        Assert.AreEqual(document.DocumentId + ":segment:00000002", segments[0].SegmentId);
        Assert.AreEqual(document.License, segments[0].License);
        Assert.AreEqual(document.VersionTitle, segments[0].Version);
    }

    [TestMethod]
    [DataRow("collection:Talmud", "English")]
    [DataRow("collection:Torah", "Spanish")]
    [TestCategory("Regression")]
    public async Task ReadAsync_ExcludedSourceOrLanguage_DoesNotBypassUserFilters(string source, string language)
    {
        var (reader, _) = CreateReader();

        var segments = await reader.ReadAsync("Genesis 1", new SourceRetrievalQuery { SourceKeys = [source], Languages = [language] });

        Assert.HasCount(0, segments);
    }

    [TestMethod]
    [TestCategory("Regression")]
    public async Task ReadAsync_AlteredArchiveContent_RejectsChecksumMismatch()
    {
        var (reader, _) = CreateReader(Markdown.Replace("God", "god", StringComparison.Ordinal));

        await Assert.ThrowsExactlyAsync<InvalidDataException>(() => reader.ReadAsync("Genesis 1", new SourceRetrievalQuery()));
    }

    [TestMethod]
    [TestCategory("Regression")]
    public async Task CreatePacket_CompleteRange_PreservesBeginningAndEndingInsteadOfOnlyAnchorVerse()
    {
        var (reader, _) = CreateReader();
        var segments = await reader.ReadAsync("Genesis 1:1-2", new SourceRetrievalQuery());

        var packet = CanonicalEvidencePacket.Create(segments);

        Assert.HasCount(1, packet.Items);
        StringAssert.Contains(packet.Items[0].PresentedText, segments[0].Text);
        StringAssert.Contains(packet.Items[0].PresentedText, segments[1].Text);
        Assert.AreEqual("Genesis 1:1-1:2", packet.Items[0].Source.CanonicalReference);
        Assert.IsFalse(packet.Items[0].IsExcerpt);
    }

    [TestMethod]
    [TestCategory("Regression")]
    public async Task ResolveQuotation_SelectionToken_ReturnsUnmodifiedTextFromCorrectEvidence()
    {
        var (reader, _) = CreateReader();
        var segments = await reader.ReadAsync("Genesis 1:1-2", new SourceRetrievalQuery());
        var packet = CanonicalEvidencePacket.Create(segments);
        var choices = GroundedQuotationChoices.Create(packet.Items[0]);

        Assert.HasCount(2, choices);
        Assert.IsTrue(GroundedQuotationResolver.TryResolve(packet.Items[0], choices[1].Selector, out var resolved));
        Assert.AreEqual(segments[1].Text, resolved);
        Assert.IsFalse(GroundedQuotationResolver.TryResolve(packet.Items[0], "@Q999", out _));
    }

    private static (BundledCanonicalSourceReader Reader, ManifestDocument Document) CreateReader(string? archiveText = null)
    {
        var content = Encoding.UTF8.GetBytes(Markdown);
        var document = TestManifestFactory.CreateDocument(segmentCount: 2, lastReference: "Genesis 1:2", sha256: TestManifestFactory.Sha256(content)) with { FileSizeBytes = content.Length };
        using var buffer = new MemoryStream();
        using (var archive = new ZipArchive(buffer, ZipArchiveMode.Create, true))
        {
            using var stream = archive.CreateEntry(document.Sha256 + ".md").Open();
            stream.Write(Encoding.UTF8.GetBytes(archiveText ?? Markdown));
        }
        var archiveBytes = buffer.ToArray();
        return (new BundledCanonicalSourceReader(TestManifestFactory.CreateManifest(document), () => new MemoryStream(archiveBytes, false)), document);
    }
}
