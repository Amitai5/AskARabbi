namespace AskARabbiLIB.Lexicon;

/// <summary>Explicitly reports unavailable dictionary storage in unconfigured and demo environments.</summary>
public sealed class UnavailableLexiconStore : ILexiconStore
{
    /// <inheritdoc/>
    public Task<IReadOnlyList<LexiconEntry>> SearchAsync(string dictionaryId, string revision, string query, int limit, CancellationToken cancellationToken = default) => throw new InvalidOperationException("The dictionary is not available in this environment.");

    /// <inheritdoc/>
    public Task<LexiconEntry?> FindAsync(string dictionaryId, string revision, string entryId, CancellationToken cancellationToken = default) => throw new InvalidOperationException("The dictionary is not available in this environment.");
}
