using System.Text.Json;
using AskARabbiLIB.Files;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace AskARabbiLIB.Tests;

[TestClass]
public sealed class SefariaDocumentFileLoaderTests
{
    private static readonly string RepositoryRoot = Path.GetFullPath(Path.Combine(Path.DirectorySeparatorChar.ToString(), "repository"));

    [TestMethod]
    [TestCategory("Unit")]
    public async Task LoadRawFileAsync_NestedText_ReturnsRawSegmentsAndCompleteMetadata()
    {
        // Arrange
        const string rawJson = "{\"title\":\"Genesis\",\"heTitle\":\"בראשית\",\"language\":\"he\",\"versionTitle\":\"Merged\",\"versionSource\":\"https://example.test\",\"license\":\"CC-BY\",\"categories\":[\"Tanakh\",\"Torah\"],\"sectionNames\":[\"Chapter\",\"Verse\"],\"versions\":{\"count\":2},\"text\":[[\"<b>One</b>\",null],{\"named\":[\"Two\",\"שלוש\"]}]}";
        var content = TestManifestFactory.Utf8(rawJson);
        var document = TestManifestFactory.CreateDocument(rawSha256: TestManifestFactory.Sha256(content));
        var reader = new StubFileContentReader(content);
        var loader = new SefariaDocumentFileLoader(RepositoryRoot, reader);

        // Act
        var result = await loader.LoadRawFileAsync(document);

        // Assert
        Assert.AreEqual(rawJson, result.RawJson);
        Assert.AreEqual(JsonValueKind.Array, result.StructuredText.ValueKind);
        Assert.AreEqual("Genesis", result.Title);
        Assert.AreEqual("בראשית", result.HebrewTitle);
        Assert.AreEqual("he", result.ActualLanguage);
        Assert.AreEqual("CC-BY", result.License);
        CollectionAssert.AreEqual(new[] { "Tanakh", "Torah" }, result.Categories.ToArray());
        CollectionAssert.AreEqual(new[] { "Chapter", "Verse" }, result.SectionNames.ToArray());
        CollectionAssert.AreEqual(new[] { "<b>One</b>", "Two", "שלוש" }, result.EnumerateRawTextSegments().ToArray());
        Assert.AreEqual("<b>One</b>|Two|שלוש", result.GetRawText("|"));
        Assert.IsTrue(result.TryGetMetadata("VERSIONS", out var versions));
        Assert.AreEqual(2, versions.GetProperty("count").GetInt32());
        Assert.IsFalse(result.Metadata.ContainsKey("text"));
        Assert.AreEqual(1, reader.ReadCount);
        StringAssert.EndsWith(reader.LastPath!, Path.Combine("Data", "Raw", "Sefaria", "Torah", "Genesis", "English", "Test Version.json"));
    }

    [TestMethod]
    [TestCategory("Unit")]
    public async Task LoadNormalizedMarkdownAsync_ValidChecksum_ReturnsCompleteText()
    {
        // Arrange
        const string markdown = "---\ntitle: Genesis\n---\n\n# Genesis\n";
        var content = TestManifestFactory.Utf8(markdown);
        var document = TestManifestFactory.CreateDocument(sha256: TestManifestFactory.Sha256(content));
        var loader = new SefariaDocumentFileLoader(RepositoryRoot, new StubFileContentReader(content));

        // Act
        var result = await loader.LoadNormalizedMarkdownAsync(document);

        // Assert
        Assert.AreEqual(markdown, result);
    }

    [TestMethod]
    [TestCategory("Unit")]
    public async Task LoadRawFileAsync_ChecksumMismatch_ThrowsInvalidDataException()
    {
        // Arrange
        var document = TestManifestFactory.CreateDocument(rawSha256: TestManifestFactory.DefaultSha256);
        var loader = new SefariaDocumentFileLoader(RepositoryRoot, new StubFileContentReader(TestManifestFactory.Utf8("{\"text\":[]}")));

        // Act + Assert
        var exception = await Assert.ThrowsExactlyAsync<InvalidDataException>(() => loader.LoadRawFileAsync(document));
        StringAssert.Contains(exception.Message, "Checksum mismatch");
    }

    [TestMethod]
    [DataRow("not-json", "invalid JSON")]
    [DataRow("{\"title\":\"Genesis\"}", "does not contain a 'text' property")]
    [DataRow("[]", "object at its root")]
    [TestCategory("Unit")]
    public async Task LoadRawFileAsync_InvalidStructure_ThrowsInvalidDataException(string rawJson, string expectedMessage)
    {
        // Arrange
        var content = TestManifestFactory.Utf8(rawJson);
        var document = TestManifestFactory.CreateDocument(rawSha256: TestManifestFactory.Sha256(content));
        var loader = new SefariaDocumentFileLoader(RepositoryRoot, new StubFileContentReader(content));

        // Act + Assert
        var exception = await Assert.ThrowsExactlyAsync<InvalidDataException>(() => loader.LoadRawFileAsync(document));
        StringAssert.Contains(exception.Message, expectedMessage);
    }

    [TestMethod]
    [DataRow("../outside.json")]
    [DataRow("Data/../../outside.json")]
    [TestCategory("Unit")]
    public async Task LoadRawFileAsync_PathTraversal_RejectsBeforeReading(string rawFilePath)
    {
        // Arrange
        var reader = new StubFileContentReader(TestManifestFactory.Utf8("{\"text\":[]}"));
        var document = TestManifestFactory.CreateDocument(rawFilePath: rawFilePath);
        var loader = new SefariaDocumentFileLoader(RepositoryRoot, reader);

        // Act + Assert
        await Assert.ThrowsExactlyAsync<InvalidDataException>(() => loader.LoadRawFileAsync(document));
        Assert.AreEqual(0, reader.ReadCount);
    }

    private sealed class StubFileContentReader : IFileContentReader
    {
        private readonly byte[] content;

        public StubFileContentReader(byte[] content)
        {
            this.content = content;
        }

        public int ReadCount { get; private set; }

        public string? LastPath { get; private set; }

        public Task<byte[]> ReadAllBytesAsync(string path, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            ReadCount++;
            LastPath = path;
            return Task.FromResult(content);
        }
    }
}
