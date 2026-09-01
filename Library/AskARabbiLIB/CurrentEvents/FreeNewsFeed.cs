using System.Net;

namespace AskARabbiLIB.CurrentEvents;

/// <summary>Defines one no-key public RSS or Atom feed approved for current-events discovery.</summary>
public sealed record FreeNewsFeed
{
    /// <summary>Initializes a validated public feed definition.</summary>
    /// <param name="publisher">Publisher or issuing organization.</param>
    /// <param name="category">Subject category supplied to research.</param>
    /// <param name="feedUrl">Public HTTPS RSS or Atom endpoint.</param>
    public FreeNewsFeed(string publisher, string category, Uri feedUrl)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(publisher);
        ArgumentException.ThrowIfNullOrWhiteSpace(category);
        ArgumentNullException.ThrowIfNull(feedUrl);
        if (!feedUrl.IsAbsoluteUri || !string.Equals(feedUrl.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase) || string.IsNullOrWhiteSpace(feedUrl.Host) || IPAddress.TryParse(feedUrl.Host, out _) || !string.IsNullOrEmpty(feedUrl.UserInfo))
        {
            throw new ArgumentException("A free news feed must use a public hostname over HTTPS.", nameof(feedUrl));
        }

        Publisher = publisher.Trim();
        Category = category.Trim();
        FeedUrl = feedUrl;
    }

    /// <summary>Gets the publisher or issuing organization.</summary>
    public string Publisher { get; }

    /// <summary>Gets the subject category.</summary>
    public string Category { get; }

    /// <summary>Gets the public RSS or Atom endpoint.</summary>
    public Uri FeedUrl { get; }
}
