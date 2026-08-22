using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using AskARabbiLIB.Models;

namespace AskARabbiLIB.Tests;

internal static class TestManifestFactory
{
    public const string DefaultSha256 = "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";

    public static DocumentManifest CreateManifest(params ManifestDocument[] documents) => new()
    {
        SchemaVersion = "1.1",
        SourceProvider = "Sefaria",
        GeneratedAtUtc = new DateTimeOffset(2026, 8, 21, 12, 0, 0, TimeSpan.Zero),
        FilePathBase = "repository root",
        Description = "Test manifest.",
        DocumentCount = documents.Length,
        SourceManifests = new SourceManifestReferences
        {
            Raw = "Data/Raw/Sefaria/Metadata/manifest.jsonl",
            RawSha256 = DefaultSha256,
            Normalized = "Data/NormalizedData/Sefaria/Metadata/manifest.jsonl",
            NormalizedSha256 = DefaultSha256,
        },
        Documents = documents,
    };

    public static ManifestDocument CreateDocument(string title = "Genesis", string hebrewTitle = "בראשית", string language = "English", string languageCode = "en", string collection = "Torah", IReadOnlyList<string>? categories = null, string description = "Genesis is a book of the Torah.", string versionTitle = "Test Version", string? license = "CC-BY", string licenseStatus = "permissive", int segmentCount = 10, string? firstReference = "Genesis 1:1", string? lastReference = "Genesis 1:10", string? filePath = null, string? rawFilePath = null, string rawSha256 = DefaultSha256, string sha256 = DefaultSha256) => new()
    {
        FilePath = filePath ?? $"Data/NormalizedData/Sefaria/{collection}/{title}/{language}/{versionTitle}.md",
        FileDescription = description,
        FileLanguage = language,
        FileTitle = title,
        FileLanguageCode = languageCode,
        Collection = collection,
        Categories = categories ?? new[] { "Tanakh", "Torah" },
        HebrewTitle = hebrewTitle,
        VersionTitle = versionTitle,
        VersionSource = "https://example.test/version",
        FirstReference = firstReference,
        LastReference = lastReference,
        SegmentCount = segmentCount,
        License = license,
        LicenseStatus = licenseStatus,
        SourceUrl = "https://example.test/source.json",
        RawFilePath = rawFilePath ?? $"Data/Raw/Sefaria/{collection}/{title}/{language}/{versionTitle}.json",
        RawSha256 = rawSha256,
        Sha256 = sha256,
        FileSizeBytes = 100,
    };

    public static MemoryStream ToStream(DocumentManifest manifest)
    {
        var bytes = JsonSerializer.SerializeToUtf8Bytes(manifest);
        return new MemoryStream(bytes);
    }

    public static string Sha256(byte[] content) => Convert.ToHexString(SHA256.HashData(content)).ToLowerInvariant();

    public static byte[] Utf8(string value) => Encoding.UTF8.GetBytes(value);
}
