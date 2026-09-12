using System.Text.RegularExpressions;
using AskARabbiLIB.Lexicon;
using AskARabbiLIB.Models;
using AskARabbiLIB.Retrieval;

namespace AskARabbiLIB.AI.Tools;

/// <summary>Provides bounded local BDB research with original, independently verifiable citations.</summary>
public sealed class BdbDictionaryAITools
{
    private const int ExcerptLength = 2_400;
    private readonly ILexiconStore store;

    /// <summary>Initializes the dictionary capability.</summary>
    /// <param name="store">Indexed local dictionary storage.</param>
    public BdbDictionaryAITools(ILexiconStore store) => this.store = store ?? throw new ArgumentNullException(nameof(store));

    /// <summary>Searches Biblical Hebrew and Aramaic words or English meanings in BDB.</summary>
    /// <param name="query">Hebrew headword, short English definition query, or exact BDB article identifier.</param>
    /// <param name="cancellationToken">Operation cancellation.</param>
    /// <returns>Up to three cited article excerpts, or an explicit lookup failure.</returns>
    [AITool("search_bdb_dictionary", "Search the locally indexed Brown–Driver–Briggs dictionary for Biblical Hebrew/Biblical Aramaic word meanings. Use Hebrew spelling, a short English meaning, or an exact returned article ID. English searches match words, not semantic similarity; try alternate search terms when necessary. A headword match differs from a mention in an article. Read further with read_bdb_entry when needed. BDB does not establish later customs, halakhic rulings, or sound-based wordplay; verify those in religious sources. Never mention the internal lookup process in the final answer.", "hebrew", "aramaic", "word", "meaning", "mean", "root", "translate", "translation", "dictionary", "bdb", "etymology", "pun", "עברית", "ארמית", "מילה", "משמעות", "שורש")]
    public async Task<AIToolExecutionResult> SearchAsync([AIToolParameter("One Hebrew word, one to six English search terms (not an entire question), or an exact BDB article ID; maximum 100 characters.")] string query, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(query);
        if (query.Length > 100)
        {
            return AIToolExecutionResult.Failure("Use a dictionary query of at most 100 characters.");
        }
        var entries = await store.SearchAsync(BdbCorpus.DictionaryId, BdbCorpus.Revision, query, 3, cancellationToken).ConfigureAwait(false);
        if (entries.Count == 0)
        {
            return AIToolExecutionResult.Failure("No BDB article matched this spelling or these search terms. Try another spelling or the dictionary form; absence here is not evidence that the word or custom does not exist. BDB is not a comprehensive dictionary of later rabbinic Aramaic.");
        }
        var excerpts = entries.Take(3).Select(entry => CreateSource(entry, LexiconSearchText.HeadwordKey(entry.Headword) == LexiconSearchText.HeadwordKey(query) ? 0 : FindExcerptStart(entry.Text, query))).ToArray();
        return AIToolExecutionResult.FromSources(new
        {
            dictionary = BdbCorpus.Title,
            matches = entries.Take(3).Select((entry, index) => new
            {
                entryId = entry.EntryId,
                headword = entry.Headword,
                matchKind = LexiconSearchText.HeadwordKey(entry.Headword) == LexiconSearchText.HeadwordKey(query) ? "headword" : "article text or identifier",
                excerptStart = excerpts[index].ExcerptStart,
                nextOffset = excerpts[index].ExcerptStart + excerpts[index].Text.Length < entry.Text.Length ? excerpts[index].ExcerptStart + excerpts[index].Text.Length : (int?)null,
                totalCharacters = entry.Text.Length,
            }),
        }, excerpts);
    }

    /// <summary>Reads additional contiguous context from a previously identified BDB article.</summary>
    /// <param name="entryId">Exact article identifier returned by dictionary search.</param>
    /// <param name="offset">Zero-based character offset within the article.</param>
    /// <param name="cancellationToken">Operation cancellation.</param>
    /// <returns>An independently citable article excerpt.</returns>
    [AITool("read_bdb_entry", "Read additional original text from a BDB article returned by search_bdb_dictionary. Use nextOffset to continue a long entry; do not infer unread definitions or invent a root relationship.")]
    public async Task<AIToolExecutionResult> ReadAsync([AIToolParameter("Exact BDB entryId returned by dictionary search.")] string entryId, [AIToolParameter("Zero-based character offset; use zero to read the beginning or the previous result's nextOffset to continue.")] int offset = 0, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(entryId);
        if (entryId.Length > 80 || offset < 0)
        {
            return AIToolExecutionResult.Failure("Invalid article identifier or character offset.");
        }
        var entry = await store.FindAsync(BdbCorpus.DictionaryId, BdbCorpus.Revision, entryId, cancellationToken).ConfigureAwait(false);
        if (entry is null || offset >= entry.Text.Length)
        {
            return AIToolExecutionResult.Failure("The requested BDB article or text offset was not found.");
        }
        var source = CreateSource(entry, offset);
        var end = source.ExcerptStart + source.Text.Length;
        return AIToolExecutionResult.FromSources(new { entryId, headword = entry.Headword, excerptStart = source.ExcerptStart, nextOffset = end < entry.Text.Length ? end : (int?)null, totalCharacters = entry.Text.Length }, [source]);
    }

    private static int FindExcerptStart(string text, string query)
    {
        var terms = LexiconSearchText.QueryKeys(query).Select(key => key[3..]).ToArray();
        var firstMatch = -1;
        foreach (var term in terms)
        {
            // Permit vowel/cantillation marks between consonants without treating different letters as equal.
            var pattern = @"(?<![a-zא-ת])" + string.Join(@"\p{M}*", term.Select(character => Regex.Escape(character.ToString()))) + @"(?![a-zא-ת])";
            foreach (Match match in Regex.Matches(text, pattern, RegexOptions.IgnoreCase | RegexOptions.CultureInvariant, TimeSpan.FromSeconds(1)))
            {
                if (firstMatch < 0)
                {
                    firstMatch = match.Index;
                }
                var paragraphStart = match.Index == 0 ? 0 : text.LastIndexOf('\n', match.Index - 1) + 1;
                if (match.Index - paragraphStart < LexiconSearchText.ParagraphLeadLength)
                {
                    return Math.Max(0, match.Index - 160);
                }
            }
        }
        return Math.Max(0, firstMatch - 160);
    }

    private static SourceSegment CreateSource(LexiconEntry entry, int start)
    {
        if (start > 0 && char.IsLowSurrogate(entry.Text[start]))
        {
            start--;
        }
        var end = Math.Min(entry.Text.Length, start + ExcerptLength);
        if (end < entry.Text.Length && char.IsHighSurrogate(entry.Text[end - 1]))
        {
            end--;
        }
        return new SourceSegment
        {
            SegmentId = $"lexicon:{entry.DictionaryId}:{entry.Revision}:{entry.EntryId}:{start}",
            OriginalSegmentId = $"lexicon:{entry.DictionaryId}:{entry.Revision}:{entry.EntryId}",
            DocumentId = $"lexicon:{entry.DictionaryId}:{entry.Revision}",
            CanonicalReference = $"BDB, {entry.Headword} ({entry.EntryId})",
            DocumentOrdinal = 0,
            Text = entry.Text[start..end],
            Title = BdbCorpus.Title,
            HebrewTitle = "מילון בראון–דרייבר–בריגס",
            Language = entry.DefinitionLanguage,
            LanguageCode = "en",
            Collection = "Dictionaries",
            Categories = ["Reference", "Dictionary"],
            Version = entry.Edition,
            License = entry.License,
            LicenseCategory = SourceLicenseCategory.PublicDomain,
            SourceUrl = entry.SourceUrl,
            FilePath = entry.SourceFile,
            UsageNote = BdbCorpus.UsageNote,
            IsExcerpt = start != 0 || end != entry.Text.Length,
            ExcerptStart = start,
            OriginalCharacterCount = entry.Text.Length,
        };
    }
}
