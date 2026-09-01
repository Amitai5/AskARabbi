using AskARabbiLIB.DvarTorah;

namespace AskARabbi.Api.Tests;

internal sealed class InMemoryWeeklyDvarTorahStore : IWeeklyDvarTorahStore
{
    internal WeeklyDvarTorahArticle? CurrentArticle { get; set; }

    internal WeeklyDvarTorahArticle? LatestArticle { get; set; }

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
}
