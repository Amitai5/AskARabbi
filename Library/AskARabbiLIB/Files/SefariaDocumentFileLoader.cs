using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using AskARabbiLIB.Models;

namespace AskARabbiLIB.Files;

/// <summary>Loads checksum-verified raw JSON and normalized Markdown for manifest documents.</summary>
public sealed class SefariaDocumentFileLoader
{
    private readonly string repositoryRoot;
    private readonly IFileContentReader fileContentReader;

    /// <summary>Creates a repository-bound source-document loader.</summary>
    /// <param name="repositoryRoot">Absolute or relative repository root used by manifest paths.</param>
    /// <param name="fileContentReader">Optional file reader; defaults to physical file access.</param>
    public SefariaDocumentFileLoader(string repositoryRoot, IFileContentReader? fileContentReader = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(repositoryRoot);
        this.repositoryRoot = Path.GetFullPath(repositoryRoot);
        this.fileContentReader = fileContentReader ?? new PhysicalFileContentReader();
    }

    /// <summary>Loads and validates the original Sefaria JSON represented by a manifest entry.</summary>
    /// <param name="document">Manifest document whose raw JSON should be loaded.</param>
    /// <param name="cancellationToken">Token used to cancel asynchronous reading.</param>
    /// <returns>A parsed file object containing structured text and all source metadata.</returns>
    public async Task<SefariaDocumentFile> LoadRawFileAsync(ManifestDocument document, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(document);
        var path = ResolveRepositoryPath(document.RawFilePath, nameof(document.RawFilePath));
        if (!string.Equals(Path.GetExtension(path), ".json", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidDataException($"Raw source path must identify a JSON file: {document.RawFilePath}");
        }

        var content = await fileContentReader.ReadAllBytesAsync(path, cancellationToken).ConfigureAwait(false);
        VerifyChecksum(content, document.RawSha256, document.RawFilePath);
        try
        {
            using var jsonDocument = JsonDocument.Parse(content);
            if (jsonDocument.RootElement.ValueKind != JsonValueKind.Object)
            {
                throw new InvalidDataException($"Raw source JSON must contain an object at its root: {document.RawFilePath}");
            }
            if (!jsonDocument.RootElement.TryGetProperty("text", out var text))
            {
                throw new InvalidDataException($"Raw source JSON does not contain a 'text' property: {document.RawFilePath}");
            }

            var metadata = new Dictionary<string, JsonElement>(StringComparer.OrdinalIgnoreCase);
            foreach (var property in jsonDocument.RootElement.EnumerateObject())
            {
                if (!string.Equals(property.Name, "text", StringComparison.OrdinalIgnoreCase))
                {
                    metadata[property.Name] = property.Value.Clone();
                }
            }
            return new SefariaDocumentFile(document, Encoding.UTF8.GetString(content), text.Clone(), metadata);
        }
        catch (JsonException exception)
        {
            throw new InvalidDataException($"Raw source contains invalid JSON: {document.RawFilePath}", exception);
        }
    }

    /// <summary>Loads and validates the normalized Markdown represented by a manifest entry.</summary>
    /// <param name="document">Manifest document whose Markdown should be loaded.</param>
    /// <param name="cancellationToken">Token used to cancel asynchronous reading.</param>
    /// <returns>The complete normalized Markdown document.</returns>
    public async Task<string> LoadNormalizedMarkdownAsync(ManifestDocument document, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(document);
        var path = ResolveRepositoryPath(document.FilePath, nameof(document.FilePath));
        if (!string.Equals(Path.GetExtension(path), ".md", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidDataException($"Normalized source path must identify a Markdown file: {document.FilePath}");
        }

        var content = await fileContentReader.ReadAllBytesAsync(path, cancellationToken).ConfigureAwait(false);
        VerifyChecksum(content, document.Sha256, document.FilePath);
        return Encoding.UTF8.GetString(content);
    }

    private string ResolveRepositoryPath(string relativePath, string parameterName)
    {
        if (string.IsNullOrWhiteSpace(relativePath))
        {
            throw new ArgumentException("A repository-relative path is required.", parameterName);
        }
        if (Path.IsPathRooted(relativePath))
        {
            throw new InvalidDataException($"Manifest paths must be repository-relative: {relativePath}");
        }

        var platformPath = relativePath.Replace('/', Path.DirectorySeparatorChar).Replace('\\', Path.DirectorySeparatorChar);
        var fullPath = Path.GetFullPath(Path.Combine(repositoryRoot, platformPath));
        var resolvedRelativePath = Path.GetRelativePath(repositoryRoot, fullPath);
        if (string.Equals(resolvedRelativePath, "..", StringComparison.Ordinal) || resolvedRelativePath.StartsWith($"..{Path.DirectorySeparatorChar}", StringComparison.Ordinal) || Path.IsPathRooted(resolvedRelativePath))
        {
            throw new InvalidDataException($"Manifest path escapes the repository root: {relativePath}");
        }
        return fullPath;
    }

    private static void VerifyChecksum(byte[] content, string expectedChecksum, string relativePath)
    {
        if (string.IsNullOrWhiteSpace(expectedChecksum) || expectedChecksum.Length != 64 || expectedChecksum.Any(character => !Uri.IsHexDigit(character)))
        {
            throw new InvalidDataException($"Manifest checksum is invalid for {relativePath}.");
        }

        var actualChecksum = Convert.ToHexString(SHA256.HashData(content)).ToLowerInvariant();
        if (!string.Equals(actualChecksum, expectedChecksum, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidDataException($"Checksum mismatch for {relativePath}: expected {expectedChecksum}, found {actualChecksum}.");
        }
    }
}
