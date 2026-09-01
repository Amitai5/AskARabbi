namespace AskARabbiLIB.DvarTorah;

/// <summary>Produces one validated content draft for a calculated reading week.</summary>
public interface IWeeklyDvarTorahGenerator
{
    /// <summary>Generates a content draft for a reading week.</summary>
    /// <param name="week">Reading week and calendar metadata.</param>
    /// <param name="cancellationToken">Token that can cancel the operation.</param>
    /// <returns>The generated draft.</returns>
    Task<WeeklyDvarTorahDraft> GenerateAsync(WeeklyDvarTorahWeek week, CancellationToken cancellationToken = default);
}
