using AskARabbiLIB.Lexicon;

namespace AskARabbiLIB.Tests;

internal static class LexiconTestData
{
    // Small synthetic fixtures exercise structure and plumbing, not a replacement dictionary corpus.
    internal static LexiconEntry Entry(string id = "BDB00001") => new()
    {
        DictionaryId = BdbCorpus.DictionaryId,
        Revision = BdbCorpus.Revision,
        EntryId = id,
        Headword = "קָרָא",
        Text = "קָרָא call, proclaim, read.",
        SourceUrl = BdbCorpus.RepositoryUrl + "/blob/" + BdbCorpus.Revision + "/eng/json/018.content.json",
        SourceFile = "018.content.json",
        SourceSha256 = new string('a', 64),
        Edition = "BDB test edition",
        License = BdbCorpus.License,
        LicenseUrl = BdbCorpus.LicenseUrl,
    };

    internal sealed class Store(params LexiconEntry[] entries) : ILexiconStore
    {
        internal int Calls { get; private set; }
        public Task<IReadOnlyList<LexiconEntry>> SearchAsync(string dictionaryId, string revision, string query, int limit, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Calls++;
            return Task.FromResult<IReadOnlyList<LexiconEntry>>(entries.Take(limit).ToArray());
        }
        public Task<LexiconEntry?> FindAsync(string dictionaryId, string revision, string entryId, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Calls++;
            return Task.FromResult(entries.FirstOrDefault(entry => entry.EntryId == entryId));
        }
    }
}
