using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using AskARabbi.Api.Contracts.DvarTorah;
using AskARabbiLIB.DvarTorah;
using AskARabbiLIB.DvarTorah.Audio;
using Azure;
using Microsoft.AspNetCore.Mvc;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace AskARabbi.Api.Tests;

[TestClass]
public sealed class DvarTorahAudioControllerTests
{
    private const string WeekKey = "diaspora:2026-08-29";
    private const string AudioPath = "/api/dvar-torah/archive/diaspora%3A2026-08-29/audio";
    private static readonly DateTimeOffset PublishedAtUtc = new(2026, 8, 24, 18, 0, 0, TimeSpan.Zero);
    private static readonly string Version = new('a', 64);

    [TestMethod]
    [DataRow("GET", "")]
    [DataRow("HEAD", "")]
    [DataRow("GET", "/timings")]
    [TestCategory("Integration")]
    public async Task Get_Unauthenticated_RejectsBeforeStorage(string method, string suffix)
    {
        await using var application = CreateApplication();
        using var client = application.CreateNonRedirectingClient();
        using var request = new HttpRequestMessage(new HttpMethod(method), AudioPath + suffix);

        using var response = await client.SendAsync(request);

        Assert.AreEqual(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.AreEqual(0, application.DvarTorahAudio.InfoCalls);
        Assert.AreEqual(0, application.DvarTorahAudio.TimingCalls);
        Assert.IsEmpty(application.DvarTorahAudio.ReadCalls);
    }

    [TestMethod]
    [TestCategory("Integration")]
    public async Task GetAudio_ReadyCurrentPublication_StreamsPrivateMp3AndDisposesStream()
    {
        await using var application = CreateApplication();
        using var client = await application.CreateAuthenticatedClientAsync();

        using var response = await client.GetAsync($"{AudioPath}?version={Version}");
        var bytes = await response.Content.ReadAsByteArrayAsync();

        Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
        Assert.AreEqual("audio/mpeg", response.Content.Headers.ContentType?.MediaType);
        Assert.AreEqual(10L, response.Content.Headers.ContentLength);
        Assert.AreEqual("\"test-etag\"", response.Headers.ETag?.Tag);
        Assert.IsTrue(response.Headers.CacheControl?.Private);
        Assert.IsFalse(response.Headers.CacheControl?.Public);
        CollectionAssert.AreEqual(application.DvarTorahAudio.Bytes, bytes);
        Assert.AreEqual((0L, (long?)10L), application.DvarTorahAudio.ReadCalls.Single());
        Assert.IsTrue(application.DvarTorahAudio.WasStreamDisposed);
    }

    [TestMethod]
    [DataRow("bytes=0-3", 0L, 4L)]
    [DataRow("bytes=3-", 3L, 7L)]
    [DataRow("bytes=-4", 6L, 4L)]
    [DataRow("bytes=0-100", 0L, 10L)]
    [DataRow("bytes=5-5", 5L, 1L)]
    [DataRow("bytes=-100", 0L, 10L)]
    [TestCategory("Integration")]
    public async Task GetAudio_ByteRange_RequestsOnlySelectedStorageBytes(string range, long offset, long length)
    {
        await using var application = CreateApplication();
        using var client = await application.CreateAuthenticatedClientAsync();
        using var request = new HttpRequestMessage(HttpMethod.Get, AudioPath);
        request.Headers.TryAddWithoutValidation("Range", range);

        using var response = await client.SendAsync(request);
        var bytes = await response.Content.ReadAsByteArrayAsync();

        Assert.AreEqual(HttpStatusCode.PartialContent, response.StatusCode);
        Assert.AreEqual(FormattableString.Invariant($"bytes {offset}-{offset + length - 1}/10"), response.Content.Headers.ContentRange?.ToString());
        Assert.AreEqual(length, response.Content.Headers.ContentLength);
        Assert.AreEqual((offset, (long?)length), application.DvarTorahAudio.ReadCalls.Single());
        CollectionAssert.AreEqual(application.DvarTorahAudio.Bytes.Skip((int)offset).Take((int)length).ToArray(), bytes);
    }

    [TestMethod]
    [DataRow("bytes=10-")]
    [DataRow("bytes=-0")]
    [DataRow("bytes=5-2")]
    [DataRow("bytes=0-2,5-7")]
    [DataRow("items=0-3")]
    [DataRow("invalid")]
    [DataRow("bytes=9223372036854775808-")]
    [TestCategory("Integration")]
    public async Task GetAudio_InvalidRange_Returns416WithoutDownloading(string range)
    {
        await using var application = CreateApplication();
        using var client = await application.CreateAuthenticatedClientAsync();
        using var request = new HttpRequestMessage(HttpMethod.Get, AudioPath);
        request.Headers.TryAddWithoutValidation("Range", range);

        using var response = await client.SendAsync(request);

        Assert.AreEqual(HttpStatusCode.RequestedRangeNotSatisfiable, response.StatusCode);
        Assert.AreEqual("bytes */10", response.Content.Headers.ContentRange?.ToString());
        Assert.IsEmpty(application.DvarTorahAudio.ReadCalls);
    }

    [TestMethod]
    [TestCategory("Integration")]
    public async Task GetAudio_HeadWithRange_ReturnsFullMetadataWithoutOpeningAudio()
    {
        await using var application = CreateApplication();
        using var client = await application.CreateAuthenticatedClientAsync();
        using var request = new HttpRequestMessage(HttpMethod.Head, AudioPath);
        request.Headers.TryAddWithoutValidation("Range", "bytes=0-1");

        using var response = await client.SendAsync(request);

        Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
        Assert.AreEqual(10L, response.Content.Headers.ContentLength);
        Assert.IsNull(response.Content.Headers.ContentRange);
        Assert.IsEmpty(await response.Content.ReadAsByteArrayAsync());
        Assert.IsEmpty(application.DvarTorahAudio.ReadCalls);
    }

    [TestMethod]
    [DataRow("\"test-etag\"", HttpStatusCode.PartialContent)]
    [DataRow("\"old-etag\"", HttpStatusCode.OK)]
    [DataRow("W/\"test-etag\"", HttpStatusCode.OK)]
    [DataRow("not-a-date", HttpStatusCode.OK)]
    [DataRow("Mon, 24 Aug 2026 18:00:00 GMT", HttpStatusCode.PartialContent)]
    [DataRow("Sun, 23 Aug 2026 18:00:00 GMT", HttpStatusCode.OK)]
    [TestCategory("Integration")]
    public async Task GetAudio_IfRange_UsesRangeOnlyForCurrentRepresentation(string ifRange, HttpStatusCode expected)
    {
        await using var application = CreateApplication();
        using var client = await application.CreateAuthenticatedClientAsync();
        using var request = new HttpRequestMessage(HttpMethod.Get, AudioPath);
        request.Headers.TryAddWithoutValidation("Range", "bytes=0-1");
        request.Headers.TryAddWithoutValidation("If-Range", ifRange);

        using var response = await client.SendAsync(request);

        Assert.AreEqual(expected, response.StatusCode);
        Assert.AreEqual(expected == HttpStatusCode.PartialContent ? 2L : 10L, response.Content.Headers.ContentLength);
    }

    [TestMethod]
    [DataRow("If-None-Match", "\"test-etag\"")]
    [DataRow("If-None-Match", "W/\"test-etag\"")]
    [DataRow("If-None-Match", "*")]
    [DataRow("If-Modified-Since", "Mon, 24 Aug 2026 18:00:00 GMT")]
    [TestCategory("Integration")]
    public async Task GetAudio_UnchangedRepresentation_Returns304WithoutDownloading(string header, string value)
    {
        await using var application = CreateApplication();
        using var client = await application.CreateAuthenticatedClientAsync();
        using var request = new HttpRequestMessage(HttpMethod.Get, AudioPath);
        request.Headers.TryAddWithoutValidation(header, value);

        using var response = await client.SendAsync(request);

        Assert.AreEqual(HttpStatusCode.NotModified, response.StatusCode);
        Assert.IsEmpty(application.DvarTorahAudio.ReadCalls);
        Assert.IsTrue(response.Headers.CacheControl?.Private);
    }

    [TestMethod]
    [TestCategory("Integration")]
    public async Task GetAudio_DownloadRequested_AttachesSafeFilename()
    {
        await using var application = CreateApplication();
        using var client = await application.CreateAuthenticatedClientAsync();

        using var response = await client.GetAsync($"{AudioPath}?download=true");

        Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
        Assert.AreEqual("attachment", response.Content.Headers.ContentDisposition?.DispositionType);
        StringAssert.Contains(response.Content.Headers.ContentDisposition?.FileName, "askarabbi-dvar-torah-diaspora-2026-08-29.mp3");
    }

    [TestMethod]
    [DataRow("")]
    [DataRow("/timings")]
    [TestCategory("Integration")]
    public async Task Get_StaleVersion_ReturnsConflictWithoutStorageRead(string suffix)
    {
        await using var application = CreateApplication();
        using var client = await application.CreateAuthenticatedClientAsync();

        using var response = await client.GetAsync($"{AudioPath}{suffix}?version=old");

        Assert.AreEqual(HttpStatusCode.Conflict, response.StatusCode);
        Assert.AreEqual(0, application.DvarTorahAudio.InfoCalls);
        Assert.AreEqual(0, application.DvarTorahAudio.TimingCalls);
    }

    [TestMethod]
    [DataRow("")]
    [DataRow("/timings")]
    [TestCategory("Integration")]
    public async Task Get_ArticleHasNoRecording_ReturnsNotFound(string suffix)
    {
        await using var application = CreateApplication();
        Assert.IsNotNull(application.WeeklyDvarTorah.CurrentArticle);
        application.WeeklyDvarTorah.CurrentArticle = application.WeeklyDvarTorah.CurrentArticle with { Audio = null };
        using var client = await application.CreateAuthenticatedClientAsync();

        using var response = await client.GetAsync(AudioPath + suffix);

        Assert.AreEqual(HttpStatusCode.NotFound, response.StatusCode);
        Assert.AreEqual(0, application.DvarTorahAudio.InfoCalls);
        Assert.AreEqual(0, application.DvarTorahAudio.TimingCalls);
    }

    [TestMethod]
    [DataRow("unknown")]
    [DataRow("diaspora:2026-08-28")]
    [DataRow("israel:2026-08-29")]
    [DataRow("diaspora:2026-08-22")]
    [TestCategory("Integration")]
    public async Task GetAudio_InvalidOrUnpublishedWeek_ReturnsNotFound(string weekKey)
    {
        await using var application = CreateApplication();
        using var client = await application.CreateAuthenticatedClientAsync();

        using var response = await client.GetAsync($"/api/dvar-torah/archive/{Uri.EscapeDataString(weekKey)}/audio");

        Assert.AreEqual(HttpStatusCode.NotFound, response.StatusCode);
        Assert.AreEqual(0, application.DvarTorahAudio.InfoCalls);
    }

    [TestMethod]
    [TestCategory("Integration")]
    public async Task GetAudio_MissingBlob_ReturnsNotFoundWithoutOpeningStream()
    {
        await using var application = CreateApplication();
        application.DvarTorahAudio.Info = null;
        using var client = await application.CreateAuthenticatedClientAsync();

        using var response = await client.GetAsync(AudioPath);

        Assert.AreEqual(HttpStatusCode.NotFound, response.StatusCode);
        Assert.IsEmpty(application.DvarTorahAudio.ReadCalls);
    }

    [TestMethod]
    [DataRow("")]
    [DataRow("/timings")]
    [TestCategory("Integration")]
    public async Task Get_StorageUnavailable_ReturnsSafe503AndKeepsArticleAvailable(string suffix)
    {
        await using var application = CreateApplication();
        application.DvarTorahAudio.Failure = new RequestFailedException(403, "Sensitive provider diagnostic");
        using var client = await application.CreateAuthenticatedClientAsync();

        using var response = await client.GetAsync(AudioPath + suffix);
        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>();
        using var articleResponse = await client.GetAsync("/api/dvar-torah");

        Assert.AreEqual(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        Assert.AreEqual("Recording temporarily unavailable", problem?.Title);
        Assert.IsFalse((await response.Content.ReadAsStringAsync()).Contains("Sensitive provider", StringComparison.Ordinal));
        Assert.IsTrue(response.Headers.CacheControl?.NoStore);
        Assert.AreEqual(HttpStatusCode.OK, articleResponse.StatusCode);
    }

    [TestMethod]
    [TestCategory("Integration")]
    public async Task GetTimings_ReadyRecording_ReturnsExactTextOffsetsWithoutDownloadingAudio()
    {
        await using var application = CreateApplication();
        using var client = await application.CreateAuthenticatedClientAsync();

        using var response = await client.GetAsync($"{AudioPath}/timings?version={Version}");
        var timings = await response.Content.ReadFromJsonAsync<DvarTorahAudioTimings>();

        Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
        Assert.IsNotNull(timings);
        Assert.AreEqual(Version, timings.Version);
        Assert.AreEqual("First paragraph.", timings.Body);
        Assert.AreEqual("First", timings.Words[0].Text);
        Assert.AreEqual(0, timings.Words[0].TextOffset);
        Assert.AreEqual(1, application.DvarTorahAudio.TimingCalls);
        Assert.IsEmpty(application.DvarTorahAudio.ReadCalls);
    }

    [TestMethod]
    [TestCategory("Integration")]
    public async Task GetTimings_MissingManifest_ReturnsNotFound()
    {
        await using var application = CreateApplication();
        application.DvarTorahAudio.Timings = null;
        using var client = await application.CreateAuthenticatedClientAsync();

        using var response = await client.GetAsync(AudioPath + "/timings");

        Assert.AreEqual(HttpStatusCode.NotFound, response.StatusCode);
    }

    [TestMethod]
    [TestCategory("Integration")]
    public async Task GetArticle_RecordingReady_ExposesOnlyAuthenticatedVersionedApiPaths()
    {
        await using var application = CreateApplication();
        using var client = await application.CreateAuthenticatedClientAsync();

        using var response = await client.GetAsync("/api/dvar-torah");
        var publication = await response.Content.ReadFromJsonAsync<WeeklyDvarTorahResponse>();
        var json = await response.Content.ReadAsStringAsync();

        Assert.IsNotNull(publication?.DvarTorah?.Audio);
        Assert.AreEqual($"{AudioPath}?version={Version}", publication.DvarTorah.Audio.AudioUrl);
        Assert.AreEqual($"{AudioPath}/timings?version={Version}", publication.DvarTorah.Audio.TimingsUrl);
        Assert.IsFalse(json.Contains("blob.core.windows.net", StringComparison.Ordinal));
        Assert.AreEqual(0, application.DvarTorahAudio.InfoCalls);
    }

    [TestMethod]
    [TestCategory("Integration")]
    public async Task GetArticle_AudioDisabled_OmitsReadyRecordingDescriptor()
    {
        await using var application = CreateApplication();
        application.IsAudioEnabled = false;
        using var client = await application.CreateAuthenticatedClientAsync();

        var publication = await client.GetFromJsonAsync<WeeklyDvarTorahResponse>("/api/dvar-torah");

        Assert.IsNotNull(publication?.DvarTorah);
        Assert.IsNull(publication.DvarTorah.Audio);
    }

    private static TestApplicationFactory CreateApplication()
    {
        var application = new TestApplicationFactory();
        var prefix = $"diaspora/2026-08-29/{Version}/{new string('b', 64)}";
        var audio = new WeeklyDvarTorahAudioMetadata
        {
            Version = Version,
            Voice = "en-US-AndrewMultilingualNeural",
            DurationMs = 4_000,
            BlobName = $"{prefix}/narration.mp3",
            BlobUri = $"https://audio.blob.core.windows.net/dvar-torah-audio/{prefix}/narration.mp3",
            TimingsBlobName = $"{prefix}/timings.json",
            AudioLength = 10,
            CreatedAtUtc = PublishedAtUtc,
        };
        application.WeeklyDvarTorah.CurrentArticle = new WeeklyDvarTorahArticle(new WeeklyDvarTorahWeek(DateOnly.ParseExact(WeekKey[9..], "yyyy-MM-dd", CultureInfo.InvariantCulture), "16 Elul", "Ki Tavo", null, false), "Teaching", "First paragraph.", "test-v1", PublishedAtUtc, PublishedAtUtc) { Audio = audio };
        application.DvarTorahAudio.Timings = new DvarTorahAudioTimings
        {
            Version = Version,
            Voice = audio.Voice,
            Title = "Teaching",
            Body = "First paragraph.",
            DurationMs = audio.DurationMs,
            Words = [new DvarTorahAudioWord("body", "First", 0, 5, 200, 300)],
        };
        return application;
    }
}
