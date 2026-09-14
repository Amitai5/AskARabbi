using System.Text.RegularExpressions;
using AskARabbiLIB.DvarTorah;
using AskARabbiLIB.DvarTorah.Audio;

namespace AskARabbiLIB.Conversations;

/// <summary>Preserves a published teaching as reading context, never as authoritative grounding evidence.</summary>
public sealed record ConversationTeachingContext(string WeekKey, string Title, string Body, string SourceReferences, string? SelectedText)
{
    /// <summary>Gets the largest supported highlighted passage.</summary>
    public const int MaximumSelectionLength = 4_000;

    /// <summary>Creates context from the server-owned publication and validates the highlighted passage.</summary>
    /// <param name="article">Eligible published teaching.</param>
    /// <param name="selectedText">Optional passage copied from the displayed teaching.</param>
    /// <returns>A validated snapshot with display-numbered references.</returns>
    public static ConversationTeachingContext FromPublished(WeeklyDvarTorahArticle article, string? selectedText)
    {
        ArgumentNullException.ThrowIfNull(article);
        var body = DvarTorahAudioText.Normalize(article.Body);
        var sources = article.Metadata?.Sources ?? [];
        for (var index = 0; index < sources.Count; index++)
        {
            body = body.Replace($"[{sources[index].SourceId}]", $"[{index + 1}]", StringComparison.Ordinal);
        }
        var selection = selectedText is null ? null : CollapseWhitespace(DvarTorahAudioText.Normalize(selectedText));
        if (selection is not null && (selection.Length == 0 || selection.Length > MaximumSelectionLength || !CollapseWhitespace(body).Contains(selection, StringComparison.Ordinal)))
        {
            throw new ArgumentException("The highlighted passage must match this published teaching. Select the passage again.", nameof(selectedText));
        }
        var references = string.Join('\n', sources.Select((source, index) => $"[{index + 1}] {source.CanonicalReference ?? source.Title} — {source.SourceUrl}"));
        var context = new ConversationTeachingContext(article.Week.WeekKey, DvarTorahAudioText.Normalize(article.Title), body, references, selection);
        context.Validate();
        return context;
    }

    /// <summary>Validates bounded persisted reading context before storage or model use.</summary>
    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(WeekKey) || WeekKey.Length > 40 || string.IsNullOrWhiteSpace(Title) || Title.Length > 500 || string.IsNullOrWhiteSpace(Body) || Body.Length > 100_000 || SourceReferences is null || SourceReferences.Length > 20_000 || SelectedText is { Length: > MaximumSelectionLength })
        {
            throw new ArgumentException("The teaching context is invalid or exceeds the supported size.");
        }
    }

    private static string CollapseWhitespace(string text) => Regex.Replace(text, @"\s+", " ", RegexOptions.CultureInvariant, TimeSpan.FromSeconds(1)).Trim();
}
