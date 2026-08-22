using System.Text;
using AskARabbiLIB.Models;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace AskARabbiLIB.Tests;

[TestClass]
public sealed class ManifestLoaderTests
{
    [TestMethod]
    [TestCategory("Unit")]
    public async Task LoadAsync_ValidManifest_ReturnsImmutableSnapshotAndLeavesStreamOpen()
    {
        // Arrange
        var sourceCategories = new List<string> { "Tanakh", "Torah" };
        var sourceDocument = TestManifestFactory.CreateDocument(categories: sourceCategories);
        var sourceManifest = TestManifestFactory.CreateManifest(sourceDocument);
        await using var stream = TestManifestFactory.ToStream(sourceManifest);
        var loader = new ManifestLoader();

        // Act
        var result = await loader.LoadAsync(stream);
        sourceCategories.Add("Changed");

        // Assert
        Assert.AreEqual(1, result.DocumentCount);
        Assert.AreEqual("Genesis", result.Documents[0].FileTitle);
        Assert.AreEqual(2, result.Documents[0].Categories.Count);
        Assert.IsTrue(stream.CanRead);
    }

    [TestMethod]
    [TestCategory("Unit")]
    public async Task LoadAsync_InvalidJson_ThrowsInvalidDataException()
    {
        // Arrange
        await using var stream = new MemoryStream(Encoding.UTF8.GetBytes("{not-json"));
        var loader = new ManifestLoader();

        // Act + Assert
        await Assert.ThrowsExactlyAsync<InvalidDataException>(() => loader.LoadAsync(stream));
    }

    [TestMethod]
    [TestCategory("Unit")]
    public async Task LoadAsync_UnsupportedSchema_ThrowsInvalidDataException()
    {
        // Arrange
        var manifest = TestManifestFactory.CreateManifest(TestManifestFactory.CreateDocument()) with { SchemaVersion = "1.0" };
        await using var stream = TestManifestFactory.ToStream(manifest);
        var loader = new ManifestLoader();

        // Act + Assert
        var exception = await Assert.ThrowsExactlyAsync<InvalidDataException>(() => loader.LoadAsync(stream));
        StringAssert.Contains(exception.Message, "Unsupported manifest schema version");
    }

    [TestMethod]
    [TestCategory("Unit")]
    public async Task LoadAsync_DocumentCountMismatch_ThrowsInvalidDataException()
    {
        // Arrange
        var manifest = TestManifestFactory.CreateManifest(TestManifestFactory.CreateDocument()) with { DocumentCount = 2 };
        await using var stream = TestManifestFactory.ToStream(manifest);
        var loader = new ManifestLoader();

        // Act + Assert
        var exception = await Assert.ThrowsExactlyAsync<InvalidDataException>(() => loader.LoadAsync(stream));
        StringAssert.Contains(exception.Message, "documents contains 1 entries");
    }

    [TestMethod]
    [TestCategory("Unit")]
    public async Task LoadAsync_DuplicateNormalizedPath_ThrowsInvalidDataException()
    {
        // Arrange
        var first = TestManifestFactory.CreateDocument();
        var second = TestManifestFactory.CreateDocument(title: "Exodus", rawFilePath: "Data/Raw/Exodus.json") with { FilePath = first.FilePath };
        await using var stream = TestManifestFactory.ToStream(TestManifestFactory.CreateManifest(first, second));
        var loader = new ManifestLoader();

        // Act + Assert
        var exception = await Assert.ThrowsExactlyAsync<InvalidDataException>(() => loader.LoadAsync(stream));
        StringAssert.Contains(exception.Message, "duplicate filePath");
    }

    [TestMethod]
    [DataRow("")]
    [DataRow("short")]
    [DataRow("gggggggggggggggggggggggggggggggggggggggggggggggggggggggggggggggg")]
    [TestCategory("Unit")]
    public async Task LoadAsync_InvalidRawChecksum_ThrowsInvalidDataException(string checksum)
    {
        // Arrange
        var document = TestManifestFactory.CreateDocument(rawSha256: checksum);
        await using var stream = TestManifestFactory.ToStream(TestManifestFactory.CreateManifest(document));
        var loader = new ManifestLoader();

        // Act + Assert
        await Assert.ThrowsExactlyAsync<InvalidDataException>(() => loader.LoadAsync(stream));
    }

    [TestMethod]
    [TestCategory("Unit")]
    public async Task LoadAsync_UnpairedReferenceRange_ThrowsInvalidDataException()
    {
        // Arrange
        var document = TestManifestFactory.CreateDocument(lastReference: null);
        await using var stream = TestManifestFactory.ToStream(TestManifestFactory.CreateManifest(document));
        var loader = new ManifestLoader();

        // Act + Assert
        var exception = await Assert.ThrowsExactlyAsync<InvalidDataException>(() => loader.LoadAsync(stream));
        StringAssert.Contains(exception.Message, "must both be present or both be null");
    }
}
