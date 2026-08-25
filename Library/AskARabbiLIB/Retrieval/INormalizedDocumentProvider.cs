using AskARabbiLIB.Models;

namespace AskARabbiLIB.Retrieval;

/// <summary>Provides checksum-verified normalized Markdown to the segment indexer.</summary>
public interface INormalizedDocumentProvider
{
    /// <summary>Loads normalized Markdown for a manifest document.</summary>
    /// <param name="document">Manifest document to read.</param>
    /// <param name="cancellationToken">Token used to cancel reading.</param>
    /// <returns>The complete checksum-verified Markdown content.</returns>
    Task<string> LoadAsync(ManifestDocument document, CancellationToken cancellationToken = default);
}
