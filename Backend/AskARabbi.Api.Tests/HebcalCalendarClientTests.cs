using System.Net;
using System.Net.Http.Headers;
using AskARabbi.Api.Calendar;
using AskARabbiLIB.Calendar;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace AskARabbi.Api.Tests;

[TestClass]
public sealed class HebcalCalendarClientTests
{
    private const string Schedule = """{"items":[{"title":"Rosh Hashana 5787","date":"2026-09-12","category":"holiday","subcat":"major","link":"https://hebcal.com/h/rosh-hashana-2026"}]}""";
    private const string Solar = """{"location":{"title":"New York, United States","tzid":"America/New_York","geonameid":5128581,"latitude":40.71427,"longitude":-74.00597},"times":{"sunset":{"2026-09-10":"2026-09-10T19:00:00-04:00"},"tzeit85deg":{"2026-09-10":"2026-09-10T19:40:00-04:00"}}}""";
    private readonly MutableCalendarClock clock = new();

    [TestMethod]
    public async Task GetHolidaysAsync_CachedYearCycle_CombinesConcurrentCallsAndHonorsTtl()
    {
        var completion = new TaskCompletionSource<HttpResponseMessage>(TaskCreationOptions.RunContinuationsAsynchronously);
        using var handler = new Handler(_ => completion.Task);
        var client = Create(handler);
        var first = client.GetHolidaysAsync(2026, false, default);
        var second = client.GetHolidaysAsync(2026, false, default);
        Assert.HasCount(1, handler.Requests);
        completion.SetResult(Response(Schedule));
        var results = await Task.WhenAll(first, second);
        await client.GetHolidaysAsync(2026, false, default);
        Assert.HasCount(1, handler.Requests);
        Assert.HasCount(1, results[0].Data!.Events);
        Assert.AreEqual(clock.Now.AddHours(1), results[0].RefreshAtUtc);
    }

    [TestMethod]
    public async Task GetHolidaysAsync_OneCallerCancelled_DoesNotCancelSharedFetch()
    {
        var completion = new TaskCompletionSource<HttpResponseMessage>(TaskCreationOptions.RunContinuationsAsynchronously);
        using var handler = new Handler(_ => completion.Task);
        var client = Create(handler);
        using var cancellation = new CancellationTokenSource();
        var first = client.GetHolidaysAsync(2026, false, cancellation.Token);
        var second = client.GetHolidaysAsync(2026, false, default);
        cancellation.Cancel();
        await Assert.ThrowsAsync<OperationCanceledException>(async () => await first);
        completion.SetResult(Response(Schedule));
        Assert.IsNotNull((await second).Data);
        Assert.HasCount(1, handler.Requests);
    }

    [TestMethod]
    public async Task GetHolidaysAsync_FailedRefresh_OnlyUsesApplicableStaleSchedule()
    {
        using var handler = new Handler(_ => Task.FromResult(Response(Schedule)));
        var client = Create(handler);
        await client.GetHolidaysAsync(2026, false, default);
        clock.Now = clock.Now.AddHours(2);
        handler.Respond = _ => Task.FromResult(new HttpResponseMessage(HttpStatusCode.ServiceUnavailable));

        var stale = await client.GetHolidaysAsync(2026, false, default);
        var otherCycle = await client.GetHolidaysAsync(2026, true, default);
        var otherYear = await client.GetHolidaysAsync(2027, false, default);

        Assert.IsTrue(stale.IsStale);
        Assert.IsNotNull(stale.Data);
        Assert.IsNull(otherCycle.Data);
        Assert.IsNull(otherYear.Data);
        Assert.HasCount(7, handler.Requests); // one original and two attempts per failed key
        clock.Now = clock.Now.AddDays(8);
        Assert.IsNull((await client.GetHolidaysAsync(2026, false, default)).Data);
    }

    [TestMethod]
    public async Task GetHolidaysAsync_RateLimited_HonorsRetryAfterAcrossKeys()
    {
        using var handler = new Handler(_ =>
        {
            var response = new HttpResponseMessage(HttpStatusCode.TooManyRequests);
            response.Headers.RetryAfter = new RetryConditionHeaderValue(TimeSpan.FromMinutes(2));
            return Task.FromResult(response);
        });
        var client = Create(handler);
        var result = await client.GetHolidaysAsync(2026, false, default);
        await client.GetHolidaysAsync(2027, true, default);
        Assert.HasCount(1, handler.Requests);
        Assert.AreEqual("rate_limited", result.Problem);
        Assert.AreEqual(clock.Now.AddMinutes(2), result.RefreshAtUtc);
        clock.Now = clock.Now.AddMinutes(3);
        handler.Respond = _ => Task.FromResult(Response(Schedule));
        Assert.IsNotNull((await client.GetHolidaysAsync(2026, false, default)).Data);
    }

    [TestMethod]
    [DataRow("not json")]
    [DataRow("[]")]
    [DataRow("{\"items\":[null]}")]
    [DataRow("{\"items\":[{\"title\":\"test\",\"date\":\"bad\",\"category\":\"holiday\"}]}")]
    [DataRow("{\"items\":[{\"title\":\"test\",\"date\":\"2026-09-10T12:00:00\",\"category\":\"candles\"}]}")]
    public async Task GetHolidaysAsync_MalformedResponse_ReturnsUnavailable(string json)
    {
        using var handler = new Handler(_ => Task.FromResult(Response(json)));
        var result = await Create(handler).GetHolidaysAsync(2026, false, default);
        Assert.IsNull(result.Data);
        Assert.AreEqual("unavailable", result.Problem);
    }

    [TestMethod]
    public async Task GetSolarTimesAsync_OffsetAndNullAstronomy_PreservesApplicableValuesOnly()
    {
        using var handler = new Handler(_ => Task.FromResult(Response(Solar)));
        var result = await Create(handler).GetSolarTimesAsync(CalendarLocationCatalog.Cities[0], new(2026, 9, 10), new(2026, 9, 11), default);
        Assert.IsNotNull(result.Data);
        Assert.AreEqual(TimeSpan.FromHours(-4), result.Data.SolarDays[new(2026, 9, 10)].Sunset?.Offset);
        Assert.IsNull(result.Data.SolarDays[new(2026, 9, 11)].Sunset);
        Assert.AreEqual("America/New_York", result.Data.Location?.TimeZone);
        Assert.AreEqual(40.71427, result.Data.Location?.Latitude);
        Assert.AreEqual(-74.00597, result.Data.Location?.Longitude);
        StringAssert.Contains(handler.Requests[0], "geo=geoname&geonameid=5128581");
        Assert.IsFalse(handler.Requests[0].Contains("user", StringComparison.OrdinalIgnoreCase));
    }

    [TestMethod]
    public async Task GetSolarTimesAsync_MismatchedTimezone_DoesNotExposeIncorrectLocalTimes()
    {
        using var handler = new Handler(_ => Task.FromResult(Response(Solar)));
        var result = await Create(handler).GetSolarTimesAsync(CalendarLocationCatalog.Cities[0] with { TimeZone = "America/Chicago" }, new(2026, 9, 10), new(2026, 9, 11), default);
        Assert.IsNull(result.Data);
    }

    [TestMethod]
    [DataRow("{\"location\":null}")]
    [DataRow("{\"location\":[],\"times\":{}}")]
    [DataRow("{\"location\":{\"geonameid\":5128581,\"tzid\":\"America/New_York\"},\"times\":null}")]
    [DataRow("{\"location\":{\"geonameid\":5128581,\"tzid\":\"America/New_York\"},\"times\":{\"sunset\":{\"2026-09-10\":\"bad\"}}}")]
    public async Task GetSolarTimesAsync_MalformedObjects_ReturnsPartialFailure(string json)
    {
        using var handler = new Handler(_ => Task.FromResult(Response(json)));
        var result = await Create(handler).GetSolarTimesAsync(CalendarLocationCatalog.Cities[0], new(2026, 9, 10), new(2026, 9, 11), default);
        Assert.IsNull(result.Data);
        Assert.AreEqual("unavailable", result.Problem);
    }

    [TestMethod]
    public async Task GetHolidaysAsync_MaxAgeAndAge_TakePrecedenceOverExpires()
    {
        using var handler = new Handler(_ =>
        {
            var response = Response(Schedule);
            response.Headers.Age = TimeSpan.FromMinutes(30);
            response.Content.Headers.Expires = clock.Now.AddDays(1);
            return Task.FromResult(response);
        });

        var result = await Create(handler).GetHolidaysAsync(2026, false, default);

        Assert.AreEqual(clock.Now.AddMinutes(30), result.RefreshAtUtc);
    }

    [TestMethod]
    public async Task GetLocalEventsAsync_ConventionsAndCycle_AreSeparateCacheKeys()
    {
        const string local = """{"location":{"title":"New York","tzid":"America/New_York","geonameid":5128581},"items":[{"title":"Candle lighting","date":"2026-09-11T18:00:00-04:00","category":"candles"}]}""";
        using var handler = new Handler(_ => Task.FromResult(Response(local)));
        var client = Create(handler);
        var preferences = new CalendarPreferences { Location = CalendarLocationCatalog.Cities[0] };
        await client.GetLocalEventsAsync(preferences, new(2026, 9, 10), new(2026, 9, 24), default);
        await client.GetLocalEventsAsync(preferences with { Havdalah = "fixed", HavdalahMinutes = 72, CandleLightingMinutes = 25, InIsrael = true }, new(2026, 9, 10), new(2026, 9, 24), default);
        Assert.HasCount(2, handler.Requests);
        StringAssert.Contains(handler.Requests[0], "b=18&M=on");
        StringAssert.Contains(handler.Requests[1], "b=25&m=72");
        StringAssert.Contains(handler.Requests[1], "i=on");
    }

    [TestMethod]
    public async Task GetHolidaysAsync_Timeout_ReturnsPartialResultWithoutRetryStorm()
    {
        using var handler = new Handler(_ => Task.FromException<HttpResponseMessage>(new TaskCanceledException("Simulated timeout")));
        var client = Create(handler);
        Assert.IsNull((await client.GetHolidaysAsync(2026, false, default)).Data);
        await client.GetHolidaysAsync(2026, false, default);
        Assert.HasCount(1, handler.Requests);
    }

    [TestMethod]
    [DataRow("no-store")]
    [DataRow("no-cache")]
    [DataRow("max-age=0, must-revalidate")]
    public async Task GetHolidaysAsync_RestrictiveCacheControl_DoesNotServeStale(string directive)
    {
        using var handler = new Handler(_ =>
        {
            var response = Response(Schedule);
            response.Headers.CacheControl = CacheControlHeaderValue.Parse(directive);
            return Task.FromResult(response);
        });
        var client = Create(handler);
        await client.GetHolidaysAsync(2026, false, default);
        clock.Now = clock.Now.AddHours(2);
        handler.Respond = _ => Task.FromResult(new HttpResponseMessage(HttpStatusCode.ServiceUnavailable));
        Assert.IsNull((await client.GetHolidaysAsync(2026, false, default)).Data);
    }

    [TestMethod]
    public async Task GetHolidaysAsync_CacheCapacity_EvictsOldKeys()
    {
        using var handler = new Handler(_ => Task.FromResult(Response(Schedule)));
        var client = Create(handler);
        for (var year = 1900; year < 2030; year++)
        {
            await client.GetHolidaysAsync(year, false, default);
            clock.Now = clock.Now.AddSeconds(11);
        }
        await client.GetHolidaysAsync(1900, false, default);
        Assert.HasCount(131, handler.Requests);
    }

    private HebcalCalendarClient Create(Handler handler) => new(new Factory(handler), clock, NullLogger<HebcalCalendarClient>.Instance);
    private static HttpResponseMessage Response(string json)
    {
        var response = new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(json) };
        response.Headers.CacheControl = new() { MaxAge = TimeSpan.FromHours(1) };
        return response;
    }
    private sealed class Factory(HttpMessageHandler handler) : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) => new(handler, false) { BaseAddress = new Uri("https://www.hebcal.com/") };
    }
    private sealed class Handler(Func<HttpRequestMessage, Task<HttpResponseMessage>> send) : HttpMessageHandler
    {
        internal Func<HttpRequestMessage, Task<HttpResponseMessage>> Respond { get; set; } = send;
        internal List<string> Requests { get; } = [];
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Requests.Add(request.RequestUri!.PathAndQuery);
            return Respond(request);
        }
    }
}
