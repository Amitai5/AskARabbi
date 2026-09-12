namespace AskARabbiLIB.Lexicon;

/// <summary>Reads published dictionary articles without a remote dictionary API.</summary>
public interface ILexiconStore
{
    /// <summary>Searches an imported dictionary edition using bounded indexed lookups.</summary>
    /// <param name="dictionaryId">Dictionary identifier.</param>
    /// <param name="revision">Pinned edition revision.</param>
    /// <param name="query">A headword, article identifier, or short English definition query.</param>
    /// <param name="limit">Maximum number of articles, from one through five.</param>
    /// <param name="cancellationToken">Operation cancellation.</param>
    /// <returns>Exact headword matches followed by matching article text.</returns>
    Task<IReadOnlyList<LexiconEntry>> SearchAsync(string dictionaryId, string revision, string query, int limit, CancellationToken cancellationToken = default);

    /// <summary>Reads one article from a published dictionary edition.</summary>
    /// <param name="dictionaryId">Dictionary identifier.</param>
    /// <param name="revision">Pinned edition revision.</param>
    /// <param name="entryId">Exact upstream article identifier.</param>
    /// <param name="cancellationToken">Operation cancellation.</param>
    /// <returns>The article, or null when it does not exist in a published edition.</returns>
    Task<LexiconEntry?> FindAsync(string dictionaryId, string revision, string entryId, CancellationToken cancellationToken = default);
}
