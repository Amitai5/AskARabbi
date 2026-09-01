using System.Globalization;
using System.Net;
using System.Text;
using System.Xml;
using System.Xml.Linq;

namespace AskARabbiLIB.CurrentEvents;

/// <summary>Aggregates bounded metadata from curated no-key RSS and Atom feeds.</summary>
public sealed class FreeRssCurrentEventsSource : ICurrentEventsSource
{
    private readonly HttpClient httpClient;
    private readonly IReadOnlyList<FreeNewsFeed> feeds;
    private readonly FreeRssCurrentEventsOptions options;
    private readonly TimeProvider timeProvider;
    private readonly Action<FreeNewsFeed, Exception>? feedFailureObserver;

    /// <summary>Initializes a resilient free-feed current-events source.</summary>
    /// <param name="httpClient">HTTP client used only for the curated feed endpoints.</param>
    /// <param name="feeds">Approved no-key RSS or Atom feeds.</param>
    /// <param name="options">Request and result bounds.</param>
    /// <param name="timeProvider">Clock used to record retrieval time.</param>
    /// <param name="feedFailureObserver">Optional observer for recoverable individual-feed failures.</param>
    public FreeRssCurrentEventsSource(HttpClient httpClient, IReadOnlyList<FreeNewsFeed> feeds, FreeRssCurrentEventsOptions? options = null, TimeProvider? timeProvider = null, Action<FreeNewsFeed, Exception>? feedFailureObserver = null)
    {
        ArgumentNullException.ThrowIfNull(httpClient);
        ArgumentNullException.ThrowIfNull(feeds);
        if (feeds.Count < 2 || feeds.Any(feed => feed is null) || feeds.Select(feed => feed.Publisher).Distinct(StringComparer.OrdinalIgnoreCase).Count() != feeds.Count)
        {
            throw new ArgumentException("At least two feeds with unique publisher names are required.", nameof(feeds));
        }

        this.options = options ?? new FreeRssCurrentEventsOptions();
        this.options.Validate();
        this.httpClient = httpClient;
        this.feeds = feeds.ToArray();
        this.timeProvider = timeProvider ?? TimeProvider.System;
        this.feedFailureObserver = feedFailureObserver;
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<CurrentEventItem>> GetRecentAsync(DateTimeOffset fromUtc, DateTimeOffset throughUtc, CancellationToken cancellationToken = default)
    {
        var normalizedFrom = fromUtc.ToUniversalTime();
        var normalizedThrough = throughUtc.ToUniversalTime();
        if (normalizedThrough < normalizedFrom || normalizedThrough - normalizedFrom > TimeSpan.FromDays(31))
        {
            throw new ArgumentException("The current-events research window must be ordered and no longer than thirty-one days.", nameof(throughUtc));
        }

        var tasks = feeds.Select(feed => FetchSafelyAsync(feed, normalizedFrom, normalizedThrough, cancellationToken)).ToArray();
        var outcomes = await Task.WhenAll(tasks).ConfigureAwait(false);
        var successfulFeedCount = outcomes.Count(outcome => outcome.Exception is null);
        if (successfulFeedCount == 0)
        {
            throw new InvalidOperationException("Every configured free current-events feed failed.", new AggregateException(outcomes.Where(outcome => outcome.Exception is not null).Select(outcome => outcome.Exception!)));
        }

        var deduplicated = new Dictionary<string, CurrentEventItem>(StringComparer.OrdinalIgnoreCase);
        foreach (var item in outcomes.SelectMany(outcome => outcome.Items))
        {
            var key = $"{item.Publisher}\n{NormalizeTitle(item.Title)}";
            if (!deduplicated.TryGetValue(key, out var existing) || item.PublishedAtUtc > existing.PublishedAtUtc)
            {
                deduplicated[key] = item;
            }
        }

        return deduplicated.Values
            .OrderByDescending(item => item.PublishedAtUtc)
            .ThenBy(item => item.Publisher, StringComparer.OrdinalIgnoreCase)
            .ThenBy(item => item.Title, StringComparer.OrdinalIgnoreCase)
            .Take(options.MaximumTotalItems)
            .ToArray();
    }

    private async Task<FeedOutcome> FetchSafelyAsync(FreeNewsFeed feed, DateTimeOffset fromUtc, DateTimeOffset throughUtc, CancellationToken cancellationToken)
    {
        try
        {
            return new FeedOutcome(await FetchAsync(feed, fromUtc, throughUtc, cancellationToken).ConfigureAwait(false), null);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException or XmlException or InvalidDataException)
        {
            feedFailureObserver?.Invoke(feed, exception);
            return new FeedOutcome([], exception);
        }
    }

    private async Task<IReadOnlyList<CurrentEventItem>> FetchAsync(FreeNewsFeed feed, DateTimeOffset fromUtc, DateTimeOffset throughUtc, CancellationToken cancellationToken)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(options.RequestTimeout);
        using var request = new HttpRequestMessage(HttpMethod.Get, feed.FeedUrl);
        request.Headers.UserAgent.ParseAdd("AskARabbi-WeeklyDvarTorah/1.0 (+https://askarabbi.app)");
        request.Headers.Accept.ParseAdd("application/rss+xml, application/atom+xml, application/xml, text/xml;q=0.9");
        using var response = await httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, timeout.Token).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
        if (response.Content.Headers.ContentLength > options.MaximumFeedBytes)
        {
            throw new InvalidDataException($"Feed '{feed.Publisher}' exceeded the configured response-size limit.");
        }

        var bytes = await ReadBoundedAsync(response.Content, options.MaximumFeedBytes, timeout.Token).ConfigureAwait(false);
        var retrievedAtUtc = timeProvider.GetUtcNow();
        using var reader = XmlReader.Create(new MemoryStream(bytes, false), new XmlReaderSettings
        {
            DtdProcessing = DtdProcessing.Prohibit,
            XmlResolver = null,
            MaxCharactersInDocument = options.MaximumFeedBytes,
        });
        var document = XDocument.Load(reader, LoadOptions.None);
        return document.Descendants()
            .Where(element => element.Name.LocalName is "item" or "entry")
            .Select(element => TryCreateItem(feed, element, retrievedAtUtc))
            .Where(item => item is not null && item.PublishedAtUtc >= fromUtc && item.PublishedAtUtc <= throughUtc)
            .Select(item => item!)
            .OrderByDescending(item => item.PublishedAtUtc)
            .Take(options.MaximumItemsPerFeed)
            .ToArray();
    }

    private static CurrentEventItem? TryCreateItem(FreeNewsFeed feed, XElement element, DateTimeOffset retrievedAtUtc)
    {
        var title = NormalizeText(GetElementValue(element, "title"), 400);
        var summary = NormalizeText(GetElementValue(element, "description", "summary", "content", "encoded"), 1_200);
        var url = GetLink(element);
        var publishedText = GetElementValue(element, "pubDate", "published", "updated", "date");
        if (title is null || summary is null || url is null || !DateTimeOffset.TryParse(publishedText, CultureInfo.InvariantCulture, DateTimeStyles.AllowWhiteSpaces | DateTimeStyles.AssumeUniversal, out var publishedAtUtc))
        {
            return null;
        }

        try
        {
            return new CurrentEventItem(feed.Publisher, feed.Category, title, summary, url, publishedAtUtc, retrievedAtUtc);
        }
        catch (ArgumentException)
        {
            return null;
        }
    }

    private static string? GetLink(XElement element)
    {
        foreach (var link in element.Elements().Where(child => child.Name.LocalName == "link"))
        {
            var href = link.Attribute("href")?.Value;
            var candidate = string.IsNullOrWhiteSpace(href) ? link.Value : href;
            if (Uri.TryCreate(candidate?.Trim(), UriKind.Absolute, out var uri) && string.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase))
            {
                return uri.AbsoluteUri;
            }
        }

        return null;
    }

    private static string? GetElementValue(XElement element, params string[] localNames)
    {
        foreach (var localName in localNames)
        {
            var value = element.Elements().FirstOrDefault(child => string.Equals(child.Name.LocalName, localName, StringComparison.OrdinalIgnoreCase))?.Value;
            if (!string.IsNullOrWhiteSpace(value))
            {
                return value;
            }
        }

        return null;
    }

    private static string? NormalizeText(string? value, int maximumCharacters)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var decoded = WebUtility.HtmlDecode(value);
        var builder = new StringBuilder(decoded.Length);
        var insideTag = false;
        foreach (var character in decoded)
        {
            if (character == '<')
            {
                insideTag = true;
            }
            else if (character == '>')
            {
                insideTag = false;
                builder.Append(' ');
            }
            else if (!insideTag)
            {
                builder.Append(char.IsWhiteSpace(character) ? ' ' : character);
            }
        }

        var normalized = string.Join(' ', builder.ToString().Split(' ', StringSplitOptions.RemoveEmptyEntries));
        if (normalized.Length == 0)
        {
            return null;
        }

        return normalized.Length <= maximumCharacters ? normalized : normalized[..maximumCharacters].TrimEnd();
    }

    private static string NormalizeTitle(string title) => string.Concat(title.Where(char.IsLetterOrDigit)).ToLowerInvariant();

    private static async Task<byte[]> ReadBoundedAsync(HttpContent content, int maximumBytes, CancellationToken cancellationToken)
    {
        await using var input = await content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
        using var output = new MemoryStream();
        var buffer = new byte[16_384];
        while (true)
        {
            var read = await input.ReadAsync(buffer, cancellationToken).ConfigureAwait(false);
            if (read == 0)
            {
                break;
            }
            if (output.Length + read > maximumBytes)
            {
                throw new InvalidDataException("A current-events feed exceeded the configured response-size limit.");
            }

            output.Write(buffer, 0, read);
        }

        return output.ToArray();
    }

    private sealed record FeedOutcome(IReadOnlyList<CurrentEventItem> Items, Exception? Exception);
}
