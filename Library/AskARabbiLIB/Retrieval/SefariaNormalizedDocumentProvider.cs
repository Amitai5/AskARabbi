using AskARabbiLIB.Files;
using AskARabbiLIB.Models;

namespace AskARabbiLIB.Retrieval;

/// <summary>Loads normalized documents through the existing checksum-verifying file loader.</summary>
public sealed class SefariaNormalizedDocumentProvider : INormalizedDocumentProvider
{
    private readonly SefariaDocumentFileLoader fileLoader;

    /// <summary>Creates a normalized document provider.</summary>
    /// <param name="fileLoader">Checksum-verifying Sefaria file loader.</param>
    public SefariaNormalizedDocumentProvider(SefariaDocumentFileLoader fileLoader)
    {
        ArgumentNullException.ThrowIfNull(fileLoader);
        this.fileLoader = fileLoader;
    }

    /// <inheritdoc cref="INormalizedDocumentProvider.LoadAsync"/>
    public Task<string> LoadAsync(ManifestDocument document, CancellationToken cancellationToken = default) => fileLoader.LoadNormalizedMarkdownAsync(document, cancellationToken);
}
