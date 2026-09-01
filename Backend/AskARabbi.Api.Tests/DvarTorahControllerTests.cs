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

    private static WeeklyDvarTorahArticle CreateArticle(DateOnly shabbatDate, string title)
    {
        var week = new WeeklyDvarTorahWeek(shabbatDate, "Test Hebrew date", "Test parashah", null, false);
        return new WeeklyDvarTorahArticle(week, title, "First paragraph.\n\nSecond paragraph.", "test-v1", PublishedAtUtc, PublishedAtUtc);
    }
}
