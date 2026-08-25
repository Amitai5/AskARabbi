using System.Text.RegularExpressions;

namespace AskARabbiLIB.Models;

/// <summary>Maps exact source-license labels to validated application behavior.</summary>
public static partial class SourceLicensePolicy
{
    /// <summary>Classifies one exact permissive source-license label.</summary>
    /// <param name="license">Exact license label retained from the source metadata.</param>
    /// <returns>The supported source-license category.</returns>
    public static SourceLicenseCategory Classify(string license)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(license);
        var normalized = license.Trim();
        if (PublicDomainPattern().IsMatch(normalized))
        {
            return SourceLicenseCategory.PublicDomain;
        }

        if (Cc0Pattern().IsMatch(normalized))
        {
            return SourceLicenseCategory.Cc0;
        }

        if (CcBySaPattern().IsMatch(normalized))
        {
            return SourceLicenseCategory.CcBySa;
        }

        if (CcByPattern().IsMatch(normalized))
        {
            return SourceLicenseCategory.CcBy;
        }

        throw new ArgumentException($"Unsupported source license '{license}'.", nameof(license));
    }

    /// <summary>Returns whether a source-license category requires attribution.</summary>
    /// <param name="category">License category to evaluate.</param>
    /// <returns>True when reuse requires source attribution.</returns>
    public static bool RequiresAttribution(SourceLicenseCategory category) => category is SourceLicenseCategory.CcBy or SourceLicenseCategory.CcBySa;

    /// <summary>Returns whether a source-license category requires ShareAlike treatment for adaptations.</summary>
    /// <param name="category">License category to evaluate.</param>
    /// <returns>True when adaptations carry a ShareAlike obligation.</returns>
    public static bool RequiresShareAlike(SourceLicenseCategory category) => category == SourceLicenseCategory.CcBySa;

    /// <summary>Returns a compact human-readable name for a source-license category.</summary>
    /// <param name="category">License category to format.</param>
    /// <returns>The display name used in citations and metadata views.</returns>
    public static string GetDisplayName(SourceLicenseCategory category) => category switch
    {
        SourceLicenseCategory.PublicDomain => "Public Domain",
        SourceLicenseCategory.Cc0 => "CC0",
        SourceLicenseCategory.CcBy => "CC BY",
        SourceLicenseCategory.CcBySa => "CC BY-SA",
        _ => throw new ArgumentOutOfRangeException(nameof(category), category, "Unsupported source-license category."),
    };

    [GeneratedRegex("^(?:public domain|pd)$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex PublicDomainPattern();

    [GeneratedRegex("^cc0(?:[ -]\\d+(?:\\.\\d+)*)?$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex Cc0Pattern();

    [GeneratedRegex("^cc(?:-| )by(?:[ -]\\d+(?:\\.\\d+)*)?$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex CcByPattern();

    [GeneratedRegex("^cc(?:-| )by(?:-| )sa(?:[ -]\\d+(?:\\.\\d+)*)?$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex CcBySaPattern();
}
