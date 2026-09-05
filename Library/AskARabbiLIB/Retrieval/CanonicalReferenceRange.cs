using System.Text.RegularExpressions;

namespace AskARabbiLIB.Retrieval;

/// <summary>Matches a canonical chapter, verse, folio, or inclusive range without fuzzy reference substitution.</summary>
internal sealed record CanonicalReferenceRange(string Book, string Start, string End)
{
    private static readonly Regex Pattern = new(@"^(?<book>.+?)\s+(?<start>\d+[ab]?(?::\d+)*)(?:\s*[-–]\s*(?<end>\d+[ab]?(?::\d+)*))?$", RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);

    internal static bool TryParse(string value, out CanonicalReferenceRange? range)
    {
        var match = Pattern.Match(value.Trim().Replace('_', ' '));
        if (!match.Success)
        {
            range = null;
            return false;
        }
        var start = match.Groups["start"].Value;
        var end = match.Groups["end"].Success ? match.Groups["end"].Value : start;
        if (start.Contains(':') && !end.Contains(':') && match.Groups["end"].Success)
        {
            end = start[..(start.LastIndexOf(':') + 1)] + end;
        }
        range = new(match.Groups["book"].Value, start, end);
        return Compare(start, end) <= 0;
    }

    internal bool Contains(string reference)
    {
        if (!TryParse(reference, out var candidate) || candidate is null || !string.Equals(Book, candidate.Book, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }
        return Compare(candidate.Start, Start) >= 0 && (Compare(candidate.Start, End) <= 0 || candidate.Start.StartsWith(End + ":", StringComparison.OrdinalIgnoreCase));
    }

    private static int Compare(string left, string right)
    {
        var leftParts = Regex.Matches(left.ToLowerInvariant(), @"\d+|[ab]").Select(match => int.TryParse(match.Value, out var value) ? value : match.Value[0] - 'a' + 1).ToArray();
        var rightParts = Regex.Matches(right.ToLowerInvariant(), @"\d+|[ab]").Select(match => int.TryParse(match.Value, out var value) ? value : match.Value[0] - 'a' + 1).ToArray();
        for (var index = 0; index < Math.Min(leftParts.Length, rightParts.Length); index++)
        {
            var comparison = leftParts[index].CompareTo(rightParts[index]);
            if (comparison != 0)
            {
                return comparison;
            }
        }
        return leftParts.Length.CompareTo(rightParts.Length);
    }
}
