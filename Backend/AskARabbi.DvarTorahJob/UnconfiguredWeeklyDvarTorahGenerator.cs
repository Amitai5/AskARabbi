using AskARabbiLIB.DvarTorah;

namespace AskARabbi.DvarTorahJob;

internal sealed class UnconfiguredWeeklyDvarTorahGenerator : IWeeklyDvarTorahGenerator
{
    public Task<WeeklyDvarTorahDraft> GenerateAsync(WeeklyDvarTorahWeek week, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(week);
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromException<WeeklyDvarTorahDraft>(new InvalidOperationException("Weekly Dvar Torah generation is not configured. Replace UnconfiguredWeeklyDvarTorahGenerator after the content, source, prompt, and model contract is approved."));
    }
}
