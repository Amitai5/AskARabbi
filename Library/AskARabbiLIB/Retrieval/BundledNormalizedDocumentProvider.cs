using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using AskARabbiLIB.Models;

namespace AskARabbiLIB.Retrieval;

/// <summary>Exposes only checksum-verified original documents from the immutable deployment archive.</summary>
public sealed class BundledNormalizedDocumentProvider : INormalizedDocumentProvider
{
    private readonly Func<Stream> openArchive;

    /// <summary>Opens the approved archive and selects the manifest entries that it actually contains.</summary>
    /// <param name="manifest">Approved corpus manifest.</param>
    /// <param name="archivePath">Deployment-owned archive path, never user input.</param>
    public BundledNormalizedDocumentProvider(DocumentManifest manifest, string archivePath) : this(manifest, CreateArchiveFactory(archivePath))
    {
    }

    /// <summary>Opens an archive through an injected stream factory.</summary>
    /// <param name="manifest">Approved corpus manifest.</param>
    /// <param name="openArchive">Factory for fresh readable archive streams.</param>
    public BundledNormalizedDocumentProvider(DocumentManifest manifest, Func<Stream> openArchive)
    {
        ArgumentNullException.ThrowIfNull(manifest);
        this.openArchive = openArchive ?? throw new ArgumentNullException(nameof(openArchive));
        using var archive = new ZipArchive(openArchive(), ZipArchiveMode.Read);
        var entries = archive.Entries.Select(entry => entry.FullName).ToHashSet(StringComparer.Ordinal);
        var documents = manifest.Documents.Where(document => entries.Contains(document.Sha256 + ".md")).ToArray();
        if (documents.Length == 0)
        {
            throw new InvalidDataException("The canonical source archive does not match the approved manifest.");
        }
        Manifest = manifest with { Documents = documents, DocumentCount = documents.Length };
    }

    /// <summary>Gets the approved subset available for reproducible local indexing.</summary>
    public DocumentManifest Manifest { get; }

    /// <inheritdoc/>
    public async Task<string> LoadAsync(ManifestDocument document, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(document);
        if (!Manifest.Documents.Contains(document))
        {
            throw new ArgumentException("The document is not in the approved archive manifest.", nameof(document));
        }
        using var archive = new ZipArchive(openArchive(), ZipArchiveMode.Read);
        var entry = archive.GetEntry(document.Sha256 + ".md") ?? throw new InvalidDataException("A canonical document is missing from the archive.");
        if (entry.Length != document.FileSizeBytes || entry.Length > 20_000_000)
        {
            throw new InvalidDataException("A canonical document has invalid size metadata.");
        }
        await using var stream = entry.Open();
        using var buffer = new MemoryStream((int)entry.Length);
        await stream.CopyToAsync(buffer, cancellationToken).ConfigureAwait(false);
        var bytes = buffer.ToArray();
        if (!string.Equals(Convert.ToHexStringLower(SHA256.HashData(bytes)), document.Sha256, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidDataException("A canonical source document failed checksum verification.");
        }
        return Encoding.UTF8.GetString(bytes);
    }

    private static Func<Stream> CreateArchiveFactory(string archivePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(archivePath);
        var path = Path.GetFullPath(archivePath);
        return () => File.OpenRead(path);
    }
}
