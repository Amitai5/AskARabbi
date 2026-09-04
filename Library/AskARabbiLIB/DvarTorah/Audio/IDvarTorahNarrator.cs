namespace AskARabbiLIB.DvarTorah.Audio;

/// <summary>Generates a complete narration independently from article generation.</summary>
public interface IDvarTorahNarrator
{
    /// <summary>Synthesizes title and body, excluding citation markers, with exact display positions.</summary>
    /// <param name="article">Published article to narrate.</param>
    /// <param name="version">Expected narration version.</param>
    /// <param name="cancellationToken">Operation cancellation.</param>
    /// <returns>An MP3 with its validated word timing manifest.</returns>
    Task<DvarTorahNarration> GenerateAsync(WeeklyDvarTorahArticle article, string version, CancellationToken cancellationToken = default);
}
