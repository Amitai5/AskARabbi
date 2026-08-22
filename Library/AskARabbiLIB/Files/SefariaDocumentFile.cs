using System.Collections.Frozen;
using System.Text.Json;
using AskARabbiLIB.Models;

namespace AskARabbiLIB.Files;

/// <summary>Represents one original Sefaria JSON document and all of its source metadata.</summary>
public sealed class SefariaDocumentFile
{
    internal SefariaDocumentFile(ManifestDocument manifestDocument, string rawJson, JsonElement structuredText, IReadOnlyDictionary<string, JsonElement> metadata)
    {
        ManifestDocument = manifestDocument;
        RawJson = rawJson;
        StructuredText = structuredText;
        Metadata = metadata.ToFrozenDictionary(StringComparer.OrdinalIgnoreCase);
        Categories = GetStringArray("categories");
        SectionNames = GetStringArray("sectionNames");
    }

    public ManifestDocument ManifestDocument { get; }

    public string RawJson { get; }

    public JsonElement StructuredText { get; }

    public IReadOnlyDictionary<string, JsonElement> Metadata { get; }

    public string? Title => GetString("title");

    public string? HebrewTitle => GetString("heTitle");

    public string? Language => GetString("language");

    public string? ActualLanguage => GetString("actualLanguage") ?? Language;

    public string? VersionTitle => GetString("versionTitle");

    public string? VersionSource => GetString("versionSource");

    public string? License => GetString("license");

    public IReadOnlyList<string> Categories { get; }

    public IReadOnlyList<string> SectionNames { get; }

    /// <summary>Enumerates every string leaf under the source JSON text property in source order.</summary>
    /// <returns>The unmodified source text segments.</returns>
    public IEnumerable<string> EnumerateRawTextSegments() => EnumerateTextSegments(StructuredText);

    /// <summary>Joins every source text segment without modifying HTML, Unicode, or punctuation.</summary>
    /// <param name="separator">Text inserted between source segments.</param>
    /// <returns>The flattened raw text.</returns>
    public string GetRawText(string separator = "\n")
    {
        ArgumentNullException.ThrowIfNull(separator);
        return string.Join(separator, EnumerateRawTextSegments());
    }

    /// <summary>Tries to retrieve one source JSON metadata property by name.</summary>
    /// <param name="name">Metadata property name.</param>
    /// <param name="value">Cloned JSON value when found.</param>
    /// <returns>True when the metadata property exists.</returns>
    public bool TryGetMetadata(string name, out JsonElement value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        return Metadata.TryGetValue(name, out value);
    }

    private string? GetString(string name)
    {
        if (!Metadata.TryGetValue(name, out var value) || value.ValueKind != JsonValueKind.String)
        {
            return null;
        }
        return value.GetString();
    }

    private IReadOnlyList<string> GetStringArray(string name)
    {
        if (!Metadata.TryGetValue(name, out var value) || value.ValueKind != JsonValueKind.Array)
        {
            return Array.Empty<string>();
        }

        return value.EnumerateArray()
            .Where(element => element.ValueKind == JsonValueKind.String)
            .Select(element => element.GetString())
            .Where(element => element is not null)
            .Select(element => element!)
            .ToArray();
    }

    private static IEnumerable<string> EnumerateTextSegments(JsonElement value)
    {
        if (value.ValueKind == JsonValueKind.String)
        {
            var text = value.GetString();
            if (text is not null)
            {
                yield return text;
            }
            yield break;
        }

        if (value.ValueKind == JsonValueKind.Array)
        {
            foreach (var element in value.EnumerateArray())
            {
                foreach (var text in EnumerateTextSegments(element))
                {
                    yield return text;
                }
            }
            yield break;
        }

        if (value.ValueKind == JsonValueKind.Object)
        {
            foreach (var property in value.EnumerateObject())
            {
                foreach (var text in EnumerateTextSegments(property.Value))
                {
                    yield return text;
                }
            }
        }
    }
}
