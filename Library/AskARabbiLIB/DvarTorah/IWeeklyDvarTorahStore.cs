namespace AskARabbiLIB.DvarTorah;

/// <summary>Reads published weekly Dvar Torah articles.</summary>
public interface IWeeklyDvarTorahStore
{
    /// <summary>Gets the published article for an exact reading week.</summary>
    /// <param name="week">Exact reading week.</param>
    /// <param name="cancellationToken">Token that can cancel the operation.</param>
    /// <returns>The published article when present; otherwise, <see langword="null"/>.</returns>
    Task<WeeklyDvarTorahArticle?> GetPublishedAsync(WeeklyDvarTorahWeek week, CancellationToken cancellationToken = default);

    /// <summary>Gets the most recent published article at or before a Shabbat.</summary>
    /// <param name="inIsrael">Whether to use the Israel reading cycle.</param>
    /// <param name="notAfter">Latest eligible Shabbat date.</param>
    /// <param name="cancellationToken">Token that can cancel the operation.</param>
    /// <returns>The most recent eligible publication when present; otherwise, <see langword="null"/>.</returns>
    Task<WeeklyDvarTorahArticle?> GetLatestPublishedAsync(bool inIsrael, DateOnly notAfter, CancellationToken cancellationToken = default);

    /// <summary>Gets a published article by its stable weekly key.</summary>
    /// <param name="weekKey">Stable reading-cycle and Shabbat key.</param>
    /// <param name="cancellationToken">Token that can cancel the operation.</param>
    /// <returns>The published article when present; otherwise, <see langword="null"/>.</returns>
    Task<WeeklyDvarTorahArticle?> GetPublishedByWeekKeyAsync(string weekKey, CancellationToken cancellationToken = default);

    /// <summary>Searches and pages metadata for published articles before a Shabbat.</summary>
    /// <param name="inIsrael">Whether to use the Israel reading cycle.</param>
    /// <param name="before">Exclusive upper bound for the Shabbat date.</param>
    /// <param name="search">Optional title, reading, date, holiday, or tag search.</param>
    /// <param name="skip">Number of matching publications to skip.</param>
    /// <param name="limit">Maximum number of metadata records to return.</param>
    /// <param name="cancellationToken">Token that can cancel the operation.</param>
    /// <returns>The requested metadata page and total matching count.</returns>
    Task<WeeklyDvarTorahArchiveResult> SearchPublishedAsync(bool inIsrael, DateOnly before, string? search, int skip, int limit, CancellationToken cancellationToken = default);
}
