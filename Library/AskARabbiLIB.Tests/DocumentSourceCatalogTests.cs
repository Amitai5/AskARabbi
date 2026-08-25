using AskARabbiLIB.Retrieval;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace AskARabbiLIB.Tests;

[TestClass]
public sealed class DocumentSourceCatalogTests
{
    [TestMethod]
    [TestCategory("Unit")]
    public void Create_CoreCollectionAndNamedWork_ReturnsCompleteExclusiveInventory()
    {
        // Arrange
        var englishTorah = TestManifestFactory.CreateDocument(segmentCount: 10);
        var hebrewTorah = TestManifestFactory.CreateDocument(language: "Hebrew", languageCode: "he", segmentCount: 5, rawSha256: new string('b', 64));
        var zohar = TestManifestFactory.CreateDocument(title: "Zohar", collection: "Kabbalah", segmentCount: 7, rawSha256: new string('c', 64)) with
        {
            WorkKey = "zohar",
            UsageNote = "Mystical interpretation should be identified explicitly.",
        };
        var manifest = TestManifestFactory.CreateManifest(englishTorah, hebrewTorah, zohar);

        // Act
        var catalog = DocumentSourceCatalog.Create(manifest);

        // Assert
        Assert.AreEqual(2, catalog.SourceCount);
        Assert.AreEqual(3, catalog.DocumentCount);
        Assert.AreEqual(22L, catalog.SegmentCount);
        Assert.AreEqual("collection:Torah", catalog.Sources[0].Key);
        Assert.AreEqual("Torah", catalog.Sources[0].DisplayName);
        Assert.AreEqual(2, catalog.Sources[0].DocumentCount);
        CollectionAssert.AreEqual(new[] { "English", "Hebrew" }, catalog.Sources[0].Languages.ToArray());
        Assert.AreEqual("work:zohar", catalog.Sources[1].Key);
        Assert.AreEqual("Zohar", catalog.Sources[1].DisplayName);
        Assert.AreEqual(1, catalog.Sources[1].DocumentCount);
    }
}
