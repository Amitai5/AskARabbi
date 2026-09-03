using AskARabbiLIB.Calendar;
using System.Globalization;

namespace AskARabbiLIB.DvarTorah;

/// <summary>Resolves the current reading week and its current or latest publication.</summary>
public sealed class WeeklyDvarTorahService
{
    /// <summary>Gets the number of archive records returned when no page size is specified.</summary>
    public const int DefaultArchivePageSize = 10;

    /// <summary>Gets the largest supported archive page size.</summary>
    public const int MaximumArchivePageSize = 50;

    /// <summary>Gets the largest supported archive search length.</summary>
    public const int MaximumArchiveSearchCharacters = 120;

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

    /// <summary>Searches metadata for past publications in reverse chronological order.</summary>
    /// <param name="search">Optional title, reading, date, holiday, or tag search.</param>
    /// <param name="page">One-based page number.</param>
    /// <param name="pageSize">Number of metadata records to return per page.</param>
    /// <param name="cancellationToken">Token that can cancel the operation.</param>
    /// <returns>The requested archive page and total matching count.</returns>
    public Task<WeeklyDvarTorahArchiveResult> SearchArchiveAsync(string? search, int page = 1, int pageSize = DefaultArchivePageSize, CancellationToken cancellationToken = default)
    {
        ValidateArchivePagination(page, pageSize);
        var normalizedSearch = NormalizeArchiveSearch(search);
        var skip = checked((page - 1) * pageSize);
        return store.SearchPublishedAsync(options.InIsrael, GetCurrentWeek().ShabbatDate, normalizedSearch, skip, pageSize, cancellationToken);
    }

    /// <summary>Loads a published past article by its stable weekly key.</summary>
    /// <param name="weekKey">Stable reading-cycle and Shabbat key.</param>
    /// <param name="cancellationToken">Token that can cancel the operation.</param>
    /// <returns>The past publication when it belongs to the configured cycle; otherwise, <see langword="null"/>.</returns>
    public Task<WeeklyDvarTorahArticle?> GetArchivedAsync(string weekKey, CancellationToken cancellationToken = default)
    {
        if (!TryGetArchiveDate(weekKey, out var shabbatDate) || shabbatDate >= GetCurrentWeek().ShabbatDate)
        {
            return Task.FromResult<WeeklyDvarTorahArticle?>(null);
        }

        return store.GetPublishedByWeekKeyAsync(weekKey, cancellationToken);
    }

    private static void ValidateArchivePagination(int page, int pageSize)
    {
        if (page < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(page), "The archive page must be at least one.");
        }
        if (pageSize is < 1 or > MaximumArchivePageSize)
        {
            throw new ArgumentOutOfRangeException(nameof(pageSize), $"The archive page size must be between one and {MaximumArchivePageSize}.");
        }
        if ((long)(page - 1) * pageSize > int.MaxValue)
        {
            throw new ArgumentOutOfRangeException(nameof(page), "The requested archive page is too large.");
        }
    }

    private static string? NormalizeArchiveSearch(string? search)
    {
        if (string.IsNullOrWhiteSpace(search))
        {
            return null;
        }

        var normalized = search.Trim();
        if (normalized.Length > MaximumArchiveSearchCharacters)
        {
            throw new ArgumentException($"Archive search cannot exceed {MaximumArchiveSearchCharacters} characters.", nameof(search));
        }

        return normalized;
    }

    private bool TryGetArchiveDate(string weekKey, out DateOnly shabbatDate)
    {
        shabbatDate = default;
        if (string.IsNullOrWhiteSpace(weekKey))
        {
            return false;
        }

        var prefix = options.InIsrael ? "israel:" : "diaspora:";
        return weekKey.Length == prefix.Length + 10
            && weekKey.StartsWith(prefix, StringComparison.Ordinal)
            && DateOnly.TryParseExact(weekKey.AsSpan(prefix.Length), "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out shabbatDate)
            && shabbatDate.DayOfWeek == DayOfWeek.Saturday;
    }
}
