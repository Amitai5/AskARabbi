using AskARabbiLIB.DvarTorah;

namespace AskARabbi.DvarTorahJob;

/// <summary>Defers AI configuration and corpus loading until the weekly publication lease is owned.</summary>
internal sealed class DeferredWeeklyDvarTorahGenerator(Func<CancellationToken, Task<IWeeklyDvarTorahGenerator>> createGenerator) : IWeeklyDvarTorahGenerator
{
    /// <inheritdoc/>
    public async Task<WeeklyDvarTorahDraft> GenerateAsync(WeeklyDvarTorahWeek week, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var generator = await createGenerator(cancellationToken).ConfigureAwait(false);
        return await generator.GenerateAsync(week, cancellationToken).ConfigureAwait(false);
    }
}
