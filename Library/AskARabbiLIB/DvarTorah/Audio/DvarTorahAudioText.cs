using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

namespace AskARabbiLIB.DvarTorah.Audio;

/// <summary>Maintains the exact browser display-text contract and deterministic narration identity.</summary>
public static partial class DvarTorahAudioText
{
    private const string NarrationFormatVersion = "speech-pcm24-mp3-96-v2-silent-references";

    /// <summary>Normalizes legacy control punctuation exactly as the frontend display normalizer does.</summary>
    /// <param name="value">Original stored text.</param>
    /// <returns>Display text without trimming, collapsing whitespace, or removing source markers.</returns>
    public static string Normalize(string value)
    {
        ArgumentNullException.ThrowIfNull(value);
        var result = new StringBuilder(value.Length);
        foreach (var character in value)
        {
            var replacement = character switch
            {
                '\u0013' or '\u0096' => '–',
                '\u0014' or '\u0097' => '—',
                '\u0018' or '\u0091' => '‘',
                '\u0019' or '\u0092' => '’',
                '\u001c' or '\u0093' => '“',
                '\u001d' or '\u0094' => '”',
                '\u0085' => '…',
                _ => character,
            };
            if (replacement is <= '\u0008' or '\u000b' or '\u000c' or >= '\u000e' and <= '\u0012' or >= '\u0015' and <= '\u0017' or '\u001a' or '\u001b' or >= '\u001e' and <= '\u001f' or >= '\u007f' and <= '\u009f')
            {
                continue;
            }
            result.Append(replacement);
        }
        return result.ToString();
    }

    /// <summary>Hashes canonical text, voice, and encoding rules to invalidate stale recordings.</summary>
    /// <param name="article">Published article.</param>
    /// <param name="voice">Configured neural voice.</param>
    /// <returns>A lowercase SHA-256 version.</returns>
    public static string GetVersion(WeeklyDvarTorahArticle article, string voice)
    {
        ArgumentNullException.ThrowIfNull(article);
        ArgumentException.ThrowIfNullOrWhiteSpace(voice);
        var text = string.Join('\0', NarrationFormatVersion, voice, Normalize(article.Title), Normalize(article.Body));
        return Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(text)));
    }

    internal static IReadOnlyList<NarrationChunk> GetChunks(string section, string displayText, int maximumCharacters = 1800)
    {
        // Replace markers with equal-length spaces so every spoken character keeps its display position.
        var spoken = CitationPattern().Replace(displayText, match => new string(' ', match.Length));
        spoken = ReferenceLabelPattern().Replace(spoken, match => new string(' ', match.Length));
        var chunks = new List<NarrationChunk>();
        for (var start = 0; start < spoken.Length;)
        {
            var end = Math.Min(start + maximumCharacters, spoken.Length);
            if (end < spoken.Length)
            {
                var split = spoken.LastIndexOfAny(['\n', ' ', '\t'], end - 1, end - start);
                if (split > start)
                {
                    end = split + 1;
                }
                else if (char.IsHighSurrogate(spoken[end - 1]))
                {
                    end--;
                }
            }
            var text = spoken[start..end];
            if (!string.IsNullOrWhiteSpace(text))
            {
                chunks.Add(new NarrationChunk(section, start, text));
            }
            start = end;
        }
        return chunks;
    }

    [GeneratedRegex(@"\[(?:[TNO][A-Z]{1,2}|[A-Za-z]+\d+|\d+)(?:\s*[,;–-]\s*(?:[TNO][A-Z]{1,2}|[A-Za-z]+\d+|\d+))*\]", RegexOptions.CultureInvariant)]
    private static partial Regex CitationPattern();

    // Only application-rendered labels are silent; the actual Torah quotation remains narrated.
    [GeneratedRegex(@"^[\t ]*(?:Torah text — [^\r\n“]+: (?=“)|Sources:(?=[\t ]*\r?$))", RegexOptions.Multiline | RegexOptions.CultureInvariant)]
    private static partial Regex ReferenceLabelPattern();
}
