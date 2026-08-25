namespace AskARabbiLIB.Files;

/// <summary>Reads source documents from the physical filesystem.</summary>
public sealed class PhysicalFileContentReader : IFileContentReader
{
    /// <inheritdoc cref="IFileContentReader.ReadAllBytesAsync"/>
    public Task<byte[]> ReadAllBytesAsync(string path, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        return File.ReadAllBytesAsync(path, cancellationToken);
    }
}
