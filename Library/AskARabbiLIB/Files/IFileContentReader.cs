namespace AskARabbiLIB.Files;

/// <summary>Abstracts asynchronous file reads for source-document loading.</summary>
public interface IFileContentReader
{
    /// <summary>Reads all bytes from a file.</summary>
    /// <param name="path">Absolute file path.</param>
    /// <param name="cancellationToken">Token used to cancel asynchronous reading.</param>
    /// <returns>The complete file contents.</returns>
    Task<byte[]> ReadAllBytesAsync(string path, CancellationToken cancellationToken);
}

/// <summary>Reads source documents from the physical filesystem.</summary>
public sealed class PhysicalFileContentReader : IFileContentReader
{
    /// <inheritdoc cref="IFileContentReader.ReadAllBytesAsync"/>
    public Task<byte[]> ReadAllBytesAsync(string path, CancellationToken cancellationToken) => File.ReadAllBytesAsync(path, cancellationToken);
}
