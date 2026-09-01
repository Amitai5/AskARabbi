using System.Net;
using System.Text;
using AskARabbiLIB.CurrentEvents;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace AskARabbiLIB.Tests;

[TestClass]
public sealed class FreeRssCurrentEventsSourceTests
{
    private static readonly DateTimeOffset CurrentUtc = new(2026, 9, 1, 8, 0, 0, TimeSpan.Zero);

    [TestMethod]
    [TestCategory("Unit")]
    public void DefaultCatalog_NoSubscriptionPolicy_ContainsOnlyApprovedPublicSources()
    {
        CollectionAssert.AreEquivalent(
            new[] { "PBS News", "NPR", "MIT News", "NIST", "NASA", "Federal Reserve" },
            FreeNewsFeedCatalog.Default.Select(feed => feed.Publisher).ToArray());
        Assert.IsTrue(FreeNewsFeedCatalog.Default.All(feed => feed.FeedUrl.Scheme == Uri.UriSchemeHttps));
    }

    [TestMethod]
    [TestCategory("Unit")]
    public async Task GetRecentAsync_RssAndAtomFeeds_ReturnsBoundedRecentItems()
    {
        var first = new Uri("https://one.example.test/feed.xml");
        var second = new Uri("https://two.example.test/feed.xml");
        using var client = new HttpClient(new StubHandler(new Dictionary<Uri, HttpResponseMessage>
        {
            [first] = XmlResponse("""
                <rss version="2.0"><channel>
                  <item><title>Recent civic development</title><description><![CDATA[<p>A careful &amp; useful summary.</p>]]></description><link>https://one.example.test/recent</link><pubDate>Mon, 31 Aug 2026 10:00:00 GMT</pubDate></item>
                  <item><title>Old development</title><description>Too old.</description><link>https://one.example.test/old</link><pubDate>Sat, 01 Aug 2026 10:00:00 GMT</pubDate></item>
                </channel></rss>
                """),
            [second] = XmlResponse("""
                <feed xmlns="http://www.w3.org/2005/Atom">
                  <entry><title>Recent technology development</title><summary>Public research reached a milestone.</summary><link href="https://two.example.test/recent"/><updated>2026-08-30T12:30:00Z</updated></entry>
                </feed>
                """),
        }));
        var source = new FreeRssCurrentEventsSource(client,
        [
            new FreeNewsFeed("Publisher One", "Civic", first),
            new FreeNewsFeed("Publisher Two", "Technology", second),
        ], timeProvider: new FixedTimeProvider(CurrentUtc));

        var items = await source.GetRecentAsync(CurrentUtc.AddDays(-7), CurrentUtc);

        Assert.HasCount(2, items);
        CollectionAssert.AreEquivalent(new[] { "Publisher One", "Publisher Two" }, items.Select(item => item.Publisher).ToArray());
        Assert.AreEqual("A careful & useful summary.", items.Single(item => item.Publisher == "Publisher One").Summary);
        Assert.IsTrue(items.All(item => item.RetrievedAtUtc == CurrentUtc));
    }

    [TestMethod]
    [TestCategory("Unit")]
    public async Task GetRecentAsync_OneFeedFails_ReportsFailureAndUsesHealthyFeed()
    {
        var healthy = new Uri("https://healthy.example.test/feed.xml");
        var failing = new Uri("https://failing.example.test/feed.xml");
        using var client = new HttpClient(new StubHandler(new Dictionary<Uri, HttpResponseMessage>
        {
            [healthy] = XmlResponse("<rss><channel><item><title>Healthy item</title><description>Verified summary.</description><link>https://healthy.example.test/item</link><pubDate>Mon, 31 Aug 2026 10:00:00 GMT</pubDate></item></channel></rss>"),
            [failing] = new HttpResponseMessage(HttpStatusCode.ServiceUnavailable),
        }));
        var failures = new List<string>();
        var source = new FreeRssCurrentEventsSource(client,
        [
            new FreeNewsFeed("Healthy", "General", healthy),
            new FreeNewsFeed("Failing", "General", failing),
        ], timeProvider: new FixedTimeProvider(CurrentUtc), feedFailureObserver: (feed, _) => failures.Add(feed.Publisher));

        var items = await source.GetRecentAsync(CurrentUtc.AddDays(-7), CurrentUtc);

        Assert.HasCount(1, items);
        CollectionAssert.AreEqual(new[] { "Failing" }, failures);
    }

    [TestMethod]
    [TestCategory("Unit")]
    public async Task GetRecentAsync_EveryFeedFails_Throws()
    {
        var first = new Uri("https://one.example.test/feed.xml");
        var second = new Uri("https://two.example.test/feed.xml");
        using var client = new HttpClient(new StubHandler(new Dictionary<Uri, HttpResponseMessage>
        {
            [first] = new HttpResponseMessage(HttpStatusCode.BadGateway),
            [second] = new HttpResponseMessage(HttpStatusCode.ServiceUnavailable),
        }));
        var source = new FreeRssCurrentEventsSource(client,
        [
            new FreeNewsFeed("One", "General", first),
            new FreeNewsFeed("Two", "General", second),
        ], timeProvider: new FixedTimeProvider(CurrentUtc));

        await Assert.ThrowsExactlyAsync<InvalidOperationException>(() => source.GetRecentAsync(CurrentUtc.AddDays(-7), CurrentUtc));
    }

    private static HttpResponseMessage XmlResponse(string xml) => new(HttpStatusCode.OK)
    {
        Content = new StringContent(xml, Encoding.UTF8, "application/rss+xml"),
    };

    private sealed class StubHandler(IReadOnlyDictionary<Uri, HttpResponseMessage> responses) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (request.RequestUri is null || !responses.TryGetValue(request.RequestUri, out var response))
            {
                throw new HttpRequestException("Unexpected request URI.");
            }

            return Task.FromResult(response);
        }
    }

    private sealed class FixedTimeProvider(DateTimeOffset currentUtc) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => currentUtc;
    }
}
