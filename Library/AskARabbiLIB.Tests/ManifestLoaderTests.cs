using System.Text;
using System.Text.Json;
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
    public async Task LoadAsync_UnknownJsonProperty_ThrowsInvalidDataException()
    {
        // Arrange
        var manifest = TestManifestFactory.CreateManifest(TestManifestFactory.CreateDocument());
        var json = JsonSerializer.Serialize(manifest);
        var jsonWithUnknownProperty = $"{json[..^1]},\"unexpected\":true}}";
        await using var stream = new MemoryStream(Encoding.UTF8.GetBytes(jsonWithUnknownProperty));

        // Act and assert
        var exception = await Assert.ThrowsExactlyAsync<InvalidDataException>(() => new ManifestLoader().LoadAsync(stream));
        StringAssert.Contains(exception.Message, "expected schema");
    }

    [TestMethod]
    [TestCategory("Unit")]
    public async Task LoadAsync_NonUtcGenerationTimestamp_ThrowsInvalidDataException()
    {
        // Arrange
        var manifest = TestManifestFactory.CreateManifest(TestManifestFactory.CreateDocument()) with
        {
            GeneratedAtUtc = new DateTimeOffset(2026, 8, 21, 12, 0, 0, TimeSpan.FromHours(-7)),
        };
        await using var stream = TestManifestFactory.ToStream(manifest);

        // Act and assert
        var exception = await Assert.ThrowsExactlyAsync<InvalidDataException>(() => new ManifestLoader().LoadAsync(stream));
        StringAssert.Contains(exception.Message, "must use UTC");
    }

    [TestMethod]
    [TestCategory("Unit")]
    public async Task LoadAsync_NonPermissiveLicenseStatus_ThrowsInvalidDataException()
    {
        // Arrange
        var document = TestManifestFactory.CreateDocument(licenseStatus: "review-required");
        await using var stream = TestManifestFactory.ToStream(TestManifestFactory.CreateManifest(document));

        // Act and assert
        var exception = await Assert.ThrowsExactlyAsync<InvalidDataException>(() => new ManifestLoader().LoadAsync(stream));
        StringAssert.Contains(exception.Message, "must be 'permissive'");
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
        var second = TestManifestFactory.CreateDocument(title: "Exodus", rawFilePath: "Data/Raw/Exodus.json", rawSha256: new string('b', 64)) with { FilePath = first.FilePath };
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

    [TestMethod]
    [TestCategory("Unit")]
    public async Task LoadAsync_UnpairedSupplementalMetadata_ThrowsInvalidDataException()
    {
        // Arrange
        var document = TestManifestFactory.CreateDocument() with { WorkKey = "mishneh_torah" };
        await using var stream = TestManifestFactory.ToStream(TestManifestFactory.CreateManifest(document));
        var loader = new ManifestLoader();

        // Act + Assert
        var exception = await Assert.ThrowsExactlyAsync<InvalidDataException>(() => loader.LoadAsync(stream));
        StringAssert.Contains(exception.Message, "workKey");
    }

    [TestMethod]
    [TestCategory("Unit")]
    public async Task LoadAsync_DuplicateDocumentId_ThrowsInvalidDataException()
    {
        // Arrange
        var first = TestManifestFactory.CreateDocument();
        var second = TestManifestFactory.CreateDocument(title: "Exodus", filePath: "Data/Normalized/Exodus.md", rawFilePath: "Data/Raw/Exodus.json");
        await using var stream = TestManifestFactory.ToStream(TestManifestFactory.CreateManifest(first, second));
        var loader = new ManifestLoader();

        // Act + Assert
        var exception = await Assert.ThrowsExactlyAsync<InvalidDataException>(() => loader.LoadAsync(stream));
        StringAssert.Contains(exception.Message, "duplicate documentId");
    }

    [TestMethod]
    [TestCategory("Unit")]
    public async Task LoadAsync_DocumentIdDoesNotMatchRawChecksum_ThrowsInvalidDataException()
    {
        // Arrange
        var document = TestManifestFactory.CreateDocument() with { DocumentId = $"sefaria:{new string('b', 64)}" };
        await using var stream = TestManifestFactory.ToStream(TestManifestFactory.CreateManifest(document));
        var loader = new ManifestLoader();

        // Act + Assert
        var exception = await Assert.ThrowsExactlyAsync<InvalidDataException>(() => loader.LoadAsync(stream));
        StringAssert.Contains(exception.Message, "documentId");
    }

    [TestMethod]
    [TestCategory("Unit")]
    public async Task LoadAsync_LicenseTermsDoNotMatchExactLicense_ThrowsInvalidDataException()
    {
        // Arrange
        var document = TestManifestFactory.CreateDocument(license: "CC-BY") with { RequiresAttribution = false };
        await using var stream = TestManifestFactory.ToStream(TestManifestFactory.CreateManifest(document));

        // Act + Assert
        var exception = await Assert.ThrowsExactlyAsync<InvalidDataException>(() => new ManifestLoader().LoadAsync(stream));
        StringAssert.Contains(exception.Message, "requiresAttribution");
    }

    [TestMethod]
    [TestCategory("Unit")]
    public async Task LoadAsync_UnreadableOrNullJsonStream_ThrowsExpectedException()
    {
        // Arrange
        var unreadable = new MemoryStream();
        unreadable.Dispose();
        await using var nullJson = new MemoryStream(Encoding.UTF8.GetBytes("null"));
        var loader = new ManifestLoader();

        // Act and assert
        await Assert.ThrowsExactlyAsync<ArgumentException>(() => loader.LoadAsync(unreadable));
        await Assert.ThrowsExactlyAsync<InvalidDataException>(() => loader.LoadAsync(nullJson));
    }

    [TestMethod]
    [DataRow("missingSourceProvider", "sourceProvider")]
    [DataRow("missingGeneratedAt", "generatedAtUtc")]
    [DataRow("negativeDocumentCount", "documentCount")]
    [DataRow("nullDocuments", "documents")]
    [DataRow("nullSourceManifests", "sourceManifests")]
    [DataRow("missingRawManifest", "sourceManifests.raw")]
    [DataRow("invalidRawManifestChecksum", "sourceManifests.rawSha256")]
    [DataRow("nullDocument", "index 0")]
    [DataRow("duplicateRawPath", "duplicate rawFilePath")]
    [DataRow("unsupportedLicense", "license")]
    [DataRow("licenseCategoryMismatch", "licenseCategory")]
    [DataRow("shareAlikeMismatch", "requiresShareAlike")]
    [DataRow("malformedSourceUrl", "sourceUrl")]
    [DataRow("nonHttpAttributionUrl", "attributionUrl")]
    [DataRow("nullCategories", "categories")]
    [DataRow("emptyCategories", "categories")]
    [DataRow("blankCategory", "categories")]
    [DataRow("negativeSegmentCount", "segmentCount")]
    [DataRow("negativeFileSize", "fileSizeBytes")]
    [TestCategory("Unit")]
    public async Task LoadAsync_InvalidManifestBranch_ThrowsInvalidDataException(string scenario, string expectedMessage)
    {
        // Arrange
        var manifest = CreateInvalidManifest(scenario);
        await using var stream = TestManifestFactory.ToStream(manifest);

        // Act and assert
        var exception = await Assert.ThrowsExactlyAsync<InvalidDataException>(() => new ManifestLoader().LoadAsync(stream));
        StringAssert.Contains(exception.Message, expectedMessage);
    }

    [TestMethod]
    [TestCategory("Unit")]
    public async Task LoadAsync_ValidSupplementalDocumentWithoutReferenceRange_ReturnsMetadata()
    {
        // Arrange
        var document = TestManifestFactory.CreateDocument(firstReference: null, lastReference: null) with
        {
            WorkKey = "test_work",
            UsageNote = "Use within its documented scope.",
        };
        await using var stream = TestManifestFactory.ToStream(TestManifestFactory.CreateManifest(document));

        // Act
        var manifest = await new ManifestLoader().LoadAsync(stream);

        // Assert
        Assert.AreEqual("test_work", manifest.Documents[0].WorkKey);
        Assert.IsNull(manifest.Documents[0].FirstReference);
    }

    private static DocumentManifest CreateInvalidManifest(string scenario)
    {
        var document = TestManifestFactory.CreateDocument();
        var manifest = TestManifestFactory.CreateManifest(document);
        return scenario switch
        {
            "missingSourceProvider" => manifest with { SourceProvider = " " },
            "missingGeneratedAt" => manifest with { GeneratedAtUtc = default },
            "negativeDocumentCount" => manifest with { DocumentCount = -1 },
            "nullDocuments" => manifest with { Documents = null! },
            "nullSourceManifests" => manifest with { SourceManifests = null! },
            "missingRawManifest" => manifest with { SourceManifests = manifest.SourceManifests with { Raw = string.Empty } },
            "invalidRawManifestChecksum" => manifest with { SourceManifests = manifest.SourceManifests with { RawSha256 = "invalid" } },
            "nullDocument" => manifest with { Documents = [null!] },
            "duplicateRawPath" => CreateDuplicateRawPathManifest(document),
            "unsupportedLicense" => manifest with { Documents = [document with { License = "MIT" }] },
            "licenseCategoryMismatch" => manifest with { Documents = [document with { LicenseCategory = SourceLicenseCategory.PublicDomain }] },
            "shareAlikeMismatch" => manifest with { Documents = [document with { RequiresShareAlike = true }] },
            "malformedSourceUrl" => manifest with { Documents = [document with { SourceUrl = "not-a-url" }] },
            "nonHttpAttributionUrl" => manifest with { Documents = [document with { AttributionUrl = "ftp://example.test/source" }] },
            "nullCategories" => manifest with { Documents = [document with { Categories = null! }] },
            "emptyCategories" => manifest with { Documents = [document with { Categories = [] }] },
            "blankCategory" => manifest with { Documents = [document with { Categories = ["Torah", " "] }] },
            "negativeSegmentCount" => manifest with { Documents = [document with { SegmentCount = -1 }] },
            "negativeFileSize" => manifest with { Documents = [document with { FileSizeBytes = -1 }] },
            _ => throw new AssertFailedException($"Unknown manifest scenario '{scenario}'."),
        };
    }

    private static DocumentManifest CreateDuplicateRawPathManifest(ManifestDocument first)
    {
        var second = TestManifestFactory.CreateDocument(title: "Exodus", filePath: "Data/Normalized/Exodus.md", rawSha256: new string('b', 64)) with { RawFilePath = first.RawFilePath };
        return TestManifestFactory.CreateManifest(first, second);
    }
}
