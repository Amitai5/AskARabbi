using System.Text;
using AskARabbiLIB.Models;

namespace AskARabbiLIB.Retrieval;

/// <summary>Parses normalized Sefaria Markdown into stable citation-addressable segments.</summary>
public sealed class NormalizedMarkdownSegmentParser
{
    /// <summary>Parses and validates every segment in a normalized Markdown document.</summary>
    /// <param name="document">Manifest metadata that defines the expected document shape.</param>
    /// <param name="markdown">Normalized Markdown produced by the Sefaria pipeline.</param>
    /// <returns>Segments in canonical document order.</returns>
    public IReadOnlyList<SourceSegment> Parse(ManifestDocument document, string markdown)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(markdown);

        var segments = new List<SourceSegment>(document.SegmentCount);
        using var reader = new StringReader(markdown.TrimStart('\uFEFF'));
        string? currentReference = null;
        StringBuilder? currentText = null;
        var firstLine = reader.ReadLine();
        if (string.Equals(firstLine, "---", StringComparison.Ordinal))
        {
            SkipFrontMatter(reader, document.FilePath);
        }
        else if (firstLine is not null)
        {
            ProcessLine(firstLine, document, segments, ref currentReference, ref currentText);
        }

        string? line;
        while ((line = reader.ReadLine()) is not null)
        {
            ProcessLine(line, document, segments, ref currentReference, ref currentText);
        }
        AppendSegment(document, segments, currentReference, currentText);
        ValidateParsedDocument(document, segments);
        return segments;
    }

    private static void SkipFrontMatter(StringReader reader, string filePath)
    {
        string? line;
        while ((line = reader.ReadLine()) is not null)
        {
            if (string.Equals(line, "---", StringComparison.Ordinal))
            {
                return;
            }
        }
        throw new InvalidDataException($"Normalized Markdown front matter is not closed: {filePath}");
    }

    private static void ProcessLine(string line, ManifestDocument document, List<SourceSegment> segments, ref string? currentReference, ref StringBuilder? currentText)
    {
        if (line.StartsWith("## ", StringComparison.Ordinal))
        {
            AppendSegment(document, segments, currentReference, currentText);
            currentReference = line[3..].Trim();
            if (currentReference.Length == 0)
            {
                throw new InvalidDataException($"Normalized Markdown contains an empty segment heading: {document.FilePath}");
            }
            currentText = new StringBuilder();
            return;
        }

        if (currentText is null)
        {
            if (!string.IsNullOrWhiteSpace(line) && !line.StartsWith("# ", StringComparison.Ordinal))
            {
                throw new InvalidDataException($"Normalized Markdown contains content before its first segment heading: {document.FilePath}");
            }
            return;
        }

        if (currentText.Length > 0)
        {
            currentText.Append('\n');
        }
        currentText.Append(line);
    }

    private static void AppendSegment(ManifestDocument document, List<SourceSegment> segments, string? canonicalReference, StringBuilder? textBuilder)
    {
        if (canonicalReference is null)
        {
            return;
        }

        var text = textBuilder?.ToString().Trim() ?? string.Empty;
        if (text.Length == 0)
        {
            throw new InvalidDataException($"Normalized segment '{canonicalReference}' has no text: {document.FilePath}");
        }

        var ordinal = segments.Count;
        segments.Add(new SourceSegment
        {
            SegmentId = $"{document.DocumentId}:segment:{ordinal + 1:D8}",
            DocumentId = document.DocumentId,
            CanonicalReference = canonicalReference,
            DocumentOrdinal = ordinal,
            Text = text,
            Title = document.FileTitle,
            HebrewTitle = document.HebrewTitle,
            Language = document.FileLanguage,
            LanguageCode = document.FileLanguageCode,
            Collection = document.Collection,
            Categories = document.Categories.ToArray(),
            Version = document.VersionTitle,
            License = document.License,
            LicenseCategory = document.LicenseCategory,
            SourceUrl = document.AttributionUrl,
            FilePath = document.FilePath,
            WorkKey = document.WorkKey,
            UsageNote = document.UsageNote,
        });
    }

    private static void ValidateParsedDocument(ManifestDocument document, IReadOnlyList<SourceSegment> segments)
    {
        if (segments.Count != document.SegmentCount)
        {
            throw new InvalidDataException($"Normalized segment count mismatch for {document.FilePath}: expected {document.SegmentCount}, found {segments.Count}.");
        }
        if (segments.Count == 0)
        {
            if (document.FirstReference is not null || document.LastReference is not null)
            {
                throw new InvalidDataException($"Empty normalized document has a reference range: {document.FilePath}");
            }
            return;
        }
        if (!string.Equals(segments[0].CanonicalReference, document.FirstReference, StringComparison.Ordinal) || !string.Equals(segments[^1].CanonicalReference, document.LastReference, StringComparison.Ordinal))
        {
            throw new InvalidDataException($"Normalized reference range mismatch for {document.FilePath}: expected {document.FirstReference} through {document.LastReference}, found {segments[0].CanonicalReference} through {segments[^1].CanonicalReference}.");
        }
    }
}
