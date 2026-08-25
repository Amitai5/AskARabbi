using AskARabbiLIB.Models;

namespace AskARabbiLIB.Grounding;

/// <summary>Materializes citation provenance exclusively from trusted library data.</summary>
public sealed record SourceCitation(int Number, string EvidenceId, string SegmentId, string Title, string HebrewTitle, string CanonicalReference, string Edition, string Language, string LanguageCode, string Collection, IReadOnlyList<string> Categories, string License, SourceLicenseCategory LicenseCategory, string SourceUrl, string FilePath, bool IsExcerpt)
{
    /// <summary>Gets whether the source license requires attribution.</summary>
    public bool RequiresAttribution => SourceLicensePolicy.RequiresAttribution(LicenseCategory);

    /// <summary>Gets whether adaptations must use compatible ShareAlike terms.</summary>
    public bool RequiresShareAlike => SourceLicensePolicy.RequiresShareAlike(LicenseCategory);

    /// <summary>Gets a Markdown link to the original source using trusted citation metadata.</summary>
    public string MarkdownSourceLink => $"[{EscapeMarkdownLinkText($"{CanonicalReference} — {Edition}")}](<{new Uri(SourceUrl, UriKind.Absolute).AbsoluteUri}>)";

    private static string EscapeMarkdownLinkText(string value) => value.Replace("\\", "\\\\", StringComparison.Ordinal).Replace("[", "\\[", StringComparison.Ordinal).Replace("]", "\\]", StringComparison.Ordinal);
}
