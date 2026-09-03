using System.Net;
using System.Net.Http.Json;
using AskARabbi.Api.Contracts.DvarTorah;
using AskARabbiLIB.DvarTorah;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace AskARabbi.Api.Tests;

[TestClass]
public sealed class DvarTorahControllerTests
{
    private static readonly DateTimeOffset PublishedAtUtc = new(2026, 8, 24, 18, 0, 0, TimeSpan.Zero);

    [TestMethod]
    [TestCategory("Integration")]
    public async Task Get_Unauthenticated_ReturnsUnauthorized()
    {
        await using var application = new TestApplicationFactory();
        using var client = application.CreateNonRedirectingClient();

        using var response = await client.GetAsync("/api/dvar-torah");

        Assert.AreEqual(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [TestMethod]
    [TestCategory("Integration")]
    public async Task Get_CurrentPublicationExists_ReturnsCurrentArticle()
    {
        await using var application = new TestApplicationFactory();
        application.WeeklyDvarTorah.CurrentArticle = CreateArticle(new DateOnly(2026, 8, 29), "Current teaching");
        using var client = await application.CreateAuthenticatedClientAsync();

        using var response = await client.GetAsync("/api/dvar-torah");
        var result = await response.Content.ReadFromJsonAsync<WeeklyDvarTorahResponse>();

        Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
        Assert.IsNotNull(result?.DvarTorah);
        Assert.IsTrue(result.IsCurrentWeek);
        Assert.AreEqual("diaspora:2026-08-29", result.CurrentWeek.WeekKey);
        Assert.AreEqual("Current teaching", result.DvarTorah.Title);
        Assert.AreEqual("Choose responsibility.", result.DvarTorah.CentralTeaching);
        Assert.AreEqual(80, result.DvarTorah.TorahGroundingPercent);
        CollectionAssert.AreEqual(new[] { "responsibility", "parashah", "current events" }, result.DvarTorah.Tags.ToArray());
        Assert.HasCount(2, result.DvarTorah.Sources);
        Assert.AreEqual(WeeklyDvarTorahSourceKind.Torah, result.DvarTorah.Sources[0].Kind);
    }

    [TestMethod]
    [TestCategory("Integration")]
    public async Task Get_CurrentPublicationMissing_ReturnsLatestEarlierArticleAsFallback()
    {
        await using var application = new TestApplicationFactory();
        application.WeeklyDvarTorah.LatestArticle = CreateArticle(new DateOnly(2026, 8, 22), "Earlier teaching");
        using var client = await application.CreateAuthenticatedClientAsync();

        using var response = await client.GetAsync("/api/dvar-torah");
        var result = await response.Content.ReadFromJsonAsync<WeeklyDvarTorahResponse>();

        Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
        Assert.IsNotNull(result?.DvarTorah);
        Assert.IsFalse(result.IsCurrentWeek);
        Assert.AreEqual(new DateOnly(2026, 8, 22), result.DvarTorah.Week.ShabbatDate);
    }

    [TestMethod]
    [TestCategory("Integration")]
    public async Task Get_NoPublicationExists_ReturnsCurrentWeekAndPendingContent()
    {
        await using var application = new TestApplicationFactory();
        using var client = await application.CreateAuthenticatedClientAsync();

        using var response = await client.GetAsync("/api/dvar-torah");
        var result = await response.Content.ReadFromJsonAsync<WeeklyDvarTorahResponse>();

        Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
        Assert.IsNotNull(result);
        Assert.IsNull(result.DvarTorah);
        Assert.IsFalse(result.IsCurrentWeek);
        Assert.AreEqual(new DateOnly(2026, 8, 29), result.CurrentWeek.ShabbatDate);
    }

    [TestMethod]
    [TestCategory("Integration")]
    public async Task GetArchive_Unauthenticated_ReturnsUnauthorized()
    {
        await using var application = new TestApplicationFactory();
        using var client = application.CreateNonRedirectingClient();

        using var response = await client.GetAsync("/api/dvar-torah/archive");

        Assert.AreEqual(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [TestMethod]
    [TestCategory("Integration")]
    public async Task GetArchive_DefaultQuery_ReturnsNewestPastMetadataAndTopThreeTags()
    {
        await using var application = new TestApplicationFactory();
        application.WeeklyDvarTorah.ArchivedArticles.Add(CreateArticle(new DateOnly(2026, 8, 15), "A teaching about memory", tags: ["memory", "community", "history", "fourth"]));
        application.WeeklyDvarTorah.ArchivedArticles.Add(CreateArticle(new DateOnly(2026, 8, 22), "A teaching about responsibility", tags: ["responsibility", "community", "dignity", "fourth"]));
        using var client = await application.CreateAuthenticatedClientAsync();

        using var response = await client.GetAsync("/api/dvar-torah/archive");
        var result = await response.Content.ReadFromJsonAsync<WeeklyDvarTorahArchiveResponse>();

        Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
        Assert.IsNotNull(result);
        Assert.AreEqual(1, result.Page);
        Assert.AreEqual(10, result.PageSize);
        Assert.AreEqual(2L, result.TotalCount);
        Assert.AreEqual(1L, result.TotalPages);
        Assert.HasCount(2, result.Items);
        Assert.AreEqual("A teaching about responsibility", result.Items[0].Title);
        Assert.AreEqual(new DateOnly(2026, 8, 22), result.Items[0].Week.ShabbatDate);
        Assert.AreEqual("Test Hebrew date", result.Items[0].Week.HebrewDate);
        Assert.AreEqual("Test parashah", result.Items[0].Week.Parashah);
        CollectionAssert.AreEqual(new[] { "responsibility", "community", "dignity" }, result.Items[0].Tags.ToArray());
    }

    [TestMethod]
    [TestCategory("Integration")]
    public async Task GetArchive_SearchAndPage_ReturnMatchingRequestedPage()
    {
        await using var application = new TestApplicationFactory();
        application.WeeklyDvarTorah.ArchivedArticles.Add(CreateArticle(new DateOnly(2026, 8, 8), "Earlier responsibility", tags: ["responsibility", "ethics", "community"]));
        application.WeeklyDvarTorah.ArchivedArticles.Add(CreateArticle(new DateOnly(2026, 8, 15), "Recent responsibility", tags: ["responsibility", "dignity", "community"]));
        application.WeeklyDvarTorah.ArchivedArticles.Add(CreateArticle(new DateOnly(2026, 8, 22), "Unrelated teaching", tags: ["memory", "history", "study"]));
        using var client = await application.CreateAuthenticatedClientAsync();

        using var response = await client.GetAsync("/api/dvar-torah/archive?search=responsibility&page=2&pageSize=1");
        var result = await response.Content.ReadFromJsonAsync<WeeklyDvarTorahArchiveResponse>();

        Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
        Assert.IsNotNull(result);
        Assert.AreEqual(2, result.Page);
        Assert.AreEqual(1, result.PageSize);
        Assert.AreEqual(2L, result.TotalCount);
        Assert.AreEqual(2L, result.TotalPages);
        Assert.HasCount(1, result.Items);
        Assert.AreEqual("Earlier responsibility", result.Items[0].Title);
    }

    [TestMethod]
    [DataRow("?page=0")]
    [DataRow("?pageSize=51")]
    [DataRow("?page=2147483647&pageSize=50")]
    [TestCategory("Integration")]
    public async Task GetArchive_InvalidPagination_ReturnsBadRequest(string query)
    {
        await using var application = new TestApplicationFactory();
        using var client = await application.CreateAuthenticatedClientAsync();

        using var response = await client.GetAsync($"/api/dvar-torah/archive{query}");

        Assert.AreEqual(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [TestMethod]
    [TestCategory("Integration")]
    public async Task GetArchived_PastPublicationExists_ReturnsFullArticle()
    {
        await using var application = new TestApplicationFactory();
        var article = CreateArticle(new DateOnly(2026, 8, 22), "A past teaching");
        application.WeeklyDvarTorah.ArchivedArticles.Add(article);
        using var client = await application.CreateAuthenticatedClientAsync();

        using var response = await client.GetAsync("/api/dvar-torah/archive/diaspora%3A2026-08-22");
        var result = await response.Content.ReadFromJsonAsync<WeeklyDvarTorahArticleResponse>();

        Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
        Assert.IsNotNull(result);
        Assert.AreEqual(article.Title, result.Title);
        Assert.AreEqual(article.Body, result.Body);
        Assert.HasCount(2, result.Sources);
    }

    [TestMethod]
    [TestCategory("Integration")]
    public async Task GetArchived_CurrentOrUnknownPublication_ReturnsNotFound()
    {
        await using var application = new TestApplicationFactory();
        application.WeeklyDvarTorah.ArchivedArticles.Add(CreateArticle(new DateOnly(2026, 8, 29), "Current teaching"));
        using var client = await application.CreateAuthenticatedClientAsync();

        using var currentResponse = await client.GetAsync("/api/dvar-torah/archive/diaspora%3A2026-08-29");
        using var unknownResponse = await client.GetAsync("/api/dvar-torah/archive/diaspora%3A2026-08-15");

        Assert.AreEqual(HttpStatusCode.NotFound, currentResponse.StatusCode);
        Assert.AreEqual(HttpStatusCode.NotFound, unknownResponse.StatusCode);
    }

    private static WeeklyDvarTorahArticle CreateArticle(DateOnly shabbatDate, string title, IReadOnlyList<string>? tags = null)
    {
        var week = new WeeklyDvarTorahWeek(shabbatDate, "Test Hebrew date", "Test parashah", null, false);
        var sources = new WeeklyDvarTorahSource[]
        {
            new("T1", WeeklyDvarTorahSourceKind.Torah, "Deuteronomy", "Test edition", "https://www.sefaria.org/Deuteronomy.29.9", "You stand this day.", PublishedAtUtc, "Deuteronomy 29:9", license: "CC-BY"),
            new("N1", WeeklyDvarTorahSourceKind.News, "Current event", "Public publisher", "https://example.test/current-event", "A bounded public summary.", PublishedAtUtc, publishedAtUtc: PublishedAtUtc.AddHours(-1)),
        };
        var metadata = new WeeklyDvarTorahContentMetadata("Choose responsibility.", tags ?? ["responsibility", "parashah", "current events"], sources, 80, "review-v1", "model-v1", PublishedAtUtc.AddDays(-7), PublishedAtUtc);
        return new WeeklyDvarTorahArticle(week, title, "First paragraph.\n\nSecond paragraph.", "test-v1", PublishedAtUtc, PublishedAtUtc, metadata);
    }
}
