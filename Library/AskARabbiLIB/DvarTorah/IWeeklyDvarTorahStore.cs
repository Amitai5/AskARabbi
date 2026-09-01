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
}
