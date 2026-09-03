using AskARabbiLIB.DvarTorah;

namespace AskARabbi.Api.Tests;

internal sealed class InMemoryWeeklyDvarTorahStore : IWeeklyDvarTorahStore
{
    internal WeeklyDvarTorahArticle? CurrentArticle { get; set; }

    internal WeeklyDvarTorahArticle? LatestArticle { get; set; }

    internal List<WeeklyDvarTorahArticle> ArchivedArticles { get; } = [];

    public Task<WeeklyDvarTorahArticle?> GetPublishedAsync(WeeklyDvarTorahWeek week, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var article = CurrentArticle?.Week.WeekKey == week.WeekKey ? CurrentArticle : null;
        return Task.FromResult(article);
    }

    public Task<WeeklyDvarTorahArticle?> GetLatestPublishedAsync(bool inIsrael, DateOnly notAfter, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var article = LatestArticle is { } candidate && candidate.Week.InIsrael == inIsrael && candidate.Week.ShabbatDate <= notAfter ? candidate : null;
        return Task.FromResult(article);
    }

    public Task<WeeklyDvarTorahArticle?> GetPublishedByWeekKeyAsync(string weekKey, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var article = ArchivedArticles.FirstOrDefault(candidate => candidate.Week.WeekKey == weekKey);
        return Task.FromResult(article);
    }

    public Task<WeeklyDvarTorahArchiveResult> SearchPublishedAsync(bool inIsrael, DateOnly before, string? search, int skip, int limit, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var candidates = ArchivedArticles
            .Where(article => article.Week.InIsrael == inIsrael && article.Week.ShabbatDate < before)
            .Where(article => MatchesSearch(article, search))
            .OrderByDescending(article => article.Week.ShabbatDate)
            .ToArray();
        var items = candidates.Skip(skip).Take(limit).Select(article => new WeeklyDvarTorahArchiveItem(article.Week, article.Title, article.Metadata?.Tags.Take(3).ToArray() ?? [], article.PublishedAtUtc)).ToArray();
        return Task.FromResult(new WeeklyDvarTorahArchiveResult(items, candidates.LongLength));
    }

    private static bool MatchesSearch(WeeklyDvarTorahArticle article, string? search)
    {
        if (string.IsNullOrWhiteSpace(search))
        {
            return true;
        }

        var values = new[] { article.Title, article.Week.HebrewDate, article.Week.Parashah, article.Week.Holiday, article.Week.ShabbatDate.ToString("yyyy-MM-dd") }
            .Concat(article.Metadata?.Tags ?? []);
        return values.Any(value => value?.Contains(search.Trim(), StringComparison.OrdinalIgnoreCase) == true);
    }
}
