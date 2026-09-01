using AskARabbiLIB.Calendar;

namespace AskARabbiLIB.DvarTorah;

/// <summary>Resolves the current reading week and its current or latest publication.</summary>
public sealed class WeeklyDvarTorahService
{
    private readonly IHebrewCalendarService calendar;
    private readonly IWeeklyDvarTorahStore store;
    private readonly TimeProvider timeProvider;
    private readonly WeeklyDvarTorahOptions options;

    /// <summary>Initializes the weekly publication service.</summary>
    /// <param name="calendar">Trusted Hebrew-calendar service.</param>
    /// <param name="store">Published-article store.</param>
    /// <param name="timeProvider">Current-time provider.</param>
    /// <param name="options">Reading-cycle configuration.</param>
    public WeeklyDvarTorahService(IHebrewCalendarService calendar, IWeeklyDvarTorahStore store, TimeProvider timeProvider, WeeklyDvarTorahOptions options)
    {
        this.calendar = calendar ?? throw new ArgumentNullException(nameof(calendar));
        this.store = store ?? throw new ArgumentNullException(nameof(store));
        this.timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
        this.options = options ?? throw new ArgumentNullException(nameof(options));
        options.Validate();
    }

    /// <summary>Calculates the upcoming Shabbat for the current UTC civil date.</summary>
    /// <returns>The current weekly publication key.</returns>
    public WeeklyDvarTorahWeek GetCurrentWeek()
    {
        var now = timeProvider.GetUtcNow();
        var parashah = calendar.FindParashahForWeek(now.UtcDateTime, options.InIsrael);
        return WeeklyDvarTorahWeek.FromParashah(parashah);
    }

    /// <summary>Loads this week's article or the latest earlier publication when this week is not ready.</summary>
    /// <param name="cancellationToken">Token that can cancel the operation.</param>
    /// <returns>The current week and its current or fallback publication.</returns>
    public async Task<WeeklyDvarTorahPublication> GetCurrentOrLatestAsync(CancellationToken cancellationToken = default)
    {
        var currentWeek = GetCurrentWeek();
        var article = await store.GetPublishedAsync(currentWeek, cancellationToken).ConfigureAwait(false);
        if (article is not null)
        {
            return new WeeklyDvarTorahPublication(currentWeek, article, true);
        }

        article = await store.GetLatestPublishedAsync(options.InIsrael, currentWeek.ShabbatDate, cancellationToken).ConfigureAwait(false);
        var isCurrentWeek = article?.Week.WeekKey == currentWeek.WeekKey;
        return new WeeklyDvarTorahPublication(currentWeek, article, isCurrentWeek);
    }
}
