using System.Net;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace AskARabbiLIB.Lexicon;

/// <summary>Imports original BDB articles from the pinned Aquifer JSON format, without generating explanations.</summary>
public sealed class BdbEntryParser
{
    private static readonly TimeSpan RegexTimeout = TimeSpan.FromSeconds(2);

    /// <summary>Parses and validates a complete source file before any database writes.</summary>
    /// <param name="json">Original UTF-8 source JSON.</param>
    /// <param name="sourceFile">Upstream filename such as 001.content.json.</param>
    /// <returns>Original articles with deterministic plain-text rendering and provenance.</returns>
    public IReadOnlyList<LexiconEntry> Parse(ReadOnlyMemory<byte> json, string sourceFile)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceFile);
        if (!Regex.IsMatch(sourceFile, @"^\d{3}\.content\.json$", RegexOptions.CultureInvariant, RegexTimeout) || json.Length > 10_000_000)
        {
            throw new ArgumentException("Expected a bounded BDB source file with an upstream numeric filename.", nameof(sourceFile));
        }
        using var document = JsonDocument.Parse(json);
        if (document.RootElement.ValueKind != JsonValueKind.Array)
        {
            throw new InvalidDataException("BDB source content must be an array of articles.");
        }
        var hash = Convert.ToHexStringLower(SHA256.HashData(json.Span));
        var entries = new List<LexiconEntry>();
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var article in document.RootElement.EnumerateArray())
        {
            var id = Required(article, "content_id");
            var title = Required(article, "title");
            if (id.Length > 80 || !Regex.IsMatch(id, @"^[A-Za-z0-9._-]+$", RegexOptions.CultureInvariant, RegexTimeout) || !seen.Add(id) || Required(article, "language") != "eng" || Required(article, "media_type") != "Text")
            {
                throw new InvalidDataException("Invalid, duplicate, or unsupported BDB article metadata.");
            }
            var html = Required(article, "content");
            if (html.Length > 2_000_000)
            {
                throw new InvalidDataException("BDB article exceeds the import size limit.");
            }
            var text = ToPlainText(html);
            if (string.IsNullOrWhiteSpace(text))
            {
                throw new InvalidDataException("BDB article has no readable text.");
            }
            entries.Add(new LexiconEntry
            {
                DictionaryId = BdbCorpus.DictionaryId,
                Revision = BdbCorpus.Revision,
                EntryId = id,
                Headword = WebUtility.HtmlDecode(title),
                Text = text,
                SourceFile = sourceFile,
                SourceSha256 = hash,
                SourceUrl = $"{BdbCorpus.RepositoryUrl}/blob/{BdbCorpus.Revision}/eng/json/{sourceFile}",
                Edition = $"Aquifer BDB, article version {Required(article, "version")}",
                License = BdbCorpus.License,
                LicenseUrl = BdbCorpus.LicenseUrl,
            });
        }
        return entries;
    }

    private static string Required(JsonElement article, string name) => article.ValueKind == JsonValueKind.Object && article.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String && !string.IsNullOrWhiteSpace(value.GetString()) ? value.GetString()! : throw new InvalidDataException($"BDB article is missing {name}.");

    internal static string ToPlainText(string html)
    {
        // Remove executable/styling content first; never expose upstream HTML to the UI or model.
        var text = Regex.Replace(html, @"<(style|script)\b[^>]*>.*?</\1\s*>", "", RegexOptions.IgnoreCase | RegexOptions.Singleline | RegexOptions.CultureInvariant, RegexTimeout);
        text = Regex.Replace(text, @"</?(?:p|blockquote|h[1-6]|div|br|li)\b[^>]*>", "\n", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant, RegexTimeout);
        text = Regex.Replace(text, @"<[^>]+>", "", RegexOptions.CultureInvariant, RegexTimeout);
        text = WebUtility.HtmlDecode(text);
        text = Regex.Replace(text, @"[^\S\r\n]+", " ", RegexOptions.CultureInvariant, RegexTimeout);
        return string.Join('\n', text.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries).Select(line => line.Trim()).Where(line => line.Length > 0));
    }
}
