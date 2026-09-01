namespace AskARabbiLIB.DvarTorah;

/// <summary>Routes canonical range validation to the regular or festival weekly Torah reading.</summary>
internal static class WeeklyTorahReadingRangeCatalog
{
    internal static bool IsSupported(WeeklyDvarTorahWeek week)
    {
        ArgumentNullException.ThrowIfNull(week);
        return week.Parashah is not null
            ? ParashahTorahRangeCatalog.IsSupported(week.Parashah)
            : FestivalTorahRangeCatalog.IsSupported(week);
    }

    internal static bool Contains(WeeklyDvarTorahWeek week, string canonicalReference)
    {
        ArgumentNullException.ThrowIfNull(week);
        ArgumentException.ThrowIfNullOrWhiteSpace(canonicalReference);
        return week.Parashah is not null
            ? ParashahTorahRangeCatalog.Contains(week.Parashah, canonicalReference)
            : FestivalTorahRangeCatalog.Contains(week, canonicalReference);
    }
}
