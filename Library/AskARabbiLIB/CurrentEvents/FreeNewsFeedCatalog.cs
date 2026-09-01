namespace AskARabbiLIB.CurrentEvents;

/// <summary>Provides curated public feeds that require no API key or publisher subscription.</summary>
public static class FreeNewsFeedCatalog
{
    /// <summary>Gets the approved U.S.-centered general, technology, science, health, and economic feeds.</summary>
    public static IReadOnlyList<FreeNewsFeed> Default { get; } =
    [
        new("PBS News", "United States and world", new Uri("https://www.pbs.org/newshour/feeds/rss/headlines")),
        new("NPR", "United States and world", new Uri("https://feeds.npr.org/1001/rss.xml")),
        new("MIT News", "Technology and science", new Uri("https://news.mit.edu/rss/feed")),
        new("NIST", "Technology, science, and standards", new Uri("https://www.nist.gov/news-events/news/rss.xml")),
        new("NASA", "Science and space", new Uri("https://www.nasa.gov/news-release/feed/")),
        new("Federal Reserve", "United States economy", new Uri("https://www.federalreserve.gov/feeds/press_all.xml")),
    ];
}
