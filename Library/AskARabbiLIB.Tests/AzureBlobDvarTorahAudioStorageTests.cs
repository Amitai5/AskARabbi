using AskARabbiLIB.DvarTorah.Audio;
using Azure.Core.Pipeline;
using Azure.Storage.Blobs;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace AskARabbiLIB.Tests;

[TestClass]
public sealed class AzureBlobDvarTorahAudioStorageTests
{
    [TestMethod]
    [TestCategory("Unit")]
    public async Task UploadAsync_CompleteRecording_UsesHotPrivateImmutablePairAndRecoveryMarker()
    {
        using var server = new BlobServer();
        var storage = CreateStorage(server);
        var narration = new DvarTorahNarration(new byte[] { 0xff, 0xfb, 0, 0 }, DvarTorahAudioTestData.Timings());

        var result = await storage.UploadAsync("diaspora:2026-09-05", narration, DvarTorahAudioTestData.Now);
        var recovered = await storage.FindStoredAsync("diaspora:2026-09-05", result.Version);

        Assert.AreEqual(result, recovered);
        Assert.AreEqual(4, result.AudioLength);
        Assert.IsFalse(result.BlobUri.Contains('?', StringComparison.Ordinal));
        Assert.HasCount(3, server.Blobs);
        var uploads = server.Requests.Where(request => request.Method == HttpMethod.Put).ToArray();
        Assert.HasCount(3, uploads);
        Assert.IsTrue(uploads.All(request => request.Tier == "Hot" && request.IfNoneMatch == "*"));
        StringAssert.EndsWith(uploads[2].Path, "/complete.json");
    }

    [TestMethod]
    [TestCategory("Unit")]
    public async Task UploadAsync_DuplicateOrStaleWorker_DoesNotOverwritePublishedRecording()
    {
        using var server = new BlobServer();
        var storage = CreateStorage(server);
        var firstNarration = new DvarTorahNarration(new byte[] { 0xff, 0xfb, 0, 0 }, DvarTorahAudioTestData.Timings());
        var otherNarration = new DvarTorahNarration(new byte[] { 0xff, 0xfb, 1, 1 }, DvarTorahAudioTestData.Timings());
        var first = await storage.UploadAsync("diaspora:2026-09-05", firstNarration, DvarTorahAudioTestData.Now);

        var duplicate = await storage.UploadAsync("diaspora:2026-09-05", firstNarration, DvarTorahAudioTestData.Now.AddMinutes(1));
        var stale = await storage.UploadAsync("diaspora:2026-09-05", otherNarration, DvarTorahAudioTestData.Now.AddMinutes(2));

        Assert.AreEqual(first, duplicate);
        Assert.AreEqual(first, stale);
        CollectionAssert.AreEqual(firstNarration.Mp3.ToArray(), server.Blobs[new Uri(first.BlobUri).AbsolutePath]);
    }

    [TestMethod]
    [TestCategory("Unit")]
    public async Task GetInfoAsync_HeadOnly_ReturnsPropertiesWithoutAudioDownload()
    {
        using var server = new BlobServer();
        var storage = CreateStorage(server);
        var metadata = DvarTorahAudioTestData.Metadata();
        server.Blobs[new Uri(metadata.BlobUri).AbsolutePath] = [0xff, 0xfb, 0, 0];

        var result = await storage.GetInfoAsync(metadata);

        Assert.IsNotNull(result);
        Assert.AreEqual(4, result.Length);
        Assert.HasCount(1, server.Requests);
        Assert.AreEqual(HttpMethod.Head, server.Requests[0].Method);
    }

    [TestMethod]
    [TestCategory("Unit")]
    public async Task OpenReadAsync_ByteRange_ReadsOnlyRequestedPart()
    {
        using var server = new BlobServer();
        var storage = CreateStorage(server);
        var metadata = DvarTorahAudioTestData.Metadata();
        server.Blobs[new Uri(metadata.BlobUri).AbsolutePath] = [10, 20, 30, 40];

        await using var stream = await storage.OpenReadAsync(metadata, 1, 2);
        using var result = new MemoryStream();
        await stream.CopyToAsync(result);

        CollectionAssert.AreEqual(new byte[] { 20, 30 }, result.ToArray());
        Assert.HasCount(1, server.Requests);
        Assert.AreEqual("bytes=1-2", server.Requests[0].Range);
    }

    [TestMethod]
    [TestCategory("Unit")]
    public async Task Reader_UnknownBlob_ReturnsMissingWithoutThrowing()
    {
        using var server = new BlobServer();
        var storage = CreateStorage(server);

        Assert.IsNull(await storage.GetInfoAsync(DvarTorahAudioTestData.Metadata()));
        Assert.IsNull(await storage.GetTimingsAsync(DvarTorahAudioTestData.Metadata()));
        Assert.IsNull(await storage.FindStoredAsync("diaspora:2026-09-05", DvarTorahAudioTestData.Timings().Version));
    }

    [TestMethod]
    [DataRow("https://attacker.example/narration.mp3")]
    [DataRow("https://testaccount.blob.core.windows.net/other-container/narration.mp3")]
    [DataRow("http://testaccount.blob.core.windows.net/dvar-torah-audio/narration.mp3")]
    [TestCategory("Unit")]
    public async Task Reader_UntrustedMetadataUri_RejectsBeforeNetwork(string uri)
    {
        using var server = new BlobServer();
        var storage = CreateStorage(server);

        await Assert.ThrowsExactlyAsync<InvalidDataException>(() => storage.GetInfoAsync(DvarTorahAudioTestData.Metadata() with { BlobUri = uri }));

        Assert.HasCount(0, server.Requests);
    }

    [TestMethod]
    [TestCategory("Unit")]
    public async Task Reader_TraversalOrTokenOrInvalidRange_RejectsBeforeNetwork()
    {
        using var server = new BlobServer();
        var storage = CreateStorage(server);
        var metadata = DvarTorahAudioTestData.Metadata();

        await Assert.ThrowsExactlyAsync<InvalidDataException>(() => storage.GetInfoAsync(metadata with { BlobUri = metadata.BlobUri + "?sig=secret" }));
        await Assert.ThrowsExactlyAsync<InvalidDataException>(() => storage.GetInfoAsync(metadata with { TimingsBlobName = "../other/timings.json" }));
        await Assert.ThrowsExactlyAsync<InvalidDataException>(() => storage.GetInfoAsync(metadata with { BlobName = "../../outside.mp3" }));
        await Assert.ThrowsExactlyAsync<ArgumentOutOfRangeException>(() => storage.OpenReadAsync(metadata, -1, 1));
        await Assert.ThrowsExactlyAsync<ArgumentOutOfRangeException>(() => storage.OpenReadAsync(metadata, 4, 1));
        await Assert.ThrowsExactlyAsync<ArgumentOutOfRangeException>(() => storage.OpenReadAsync(metadata, 0, 5));

        Assert.HasCount(0, server.Requests);
    }

    [TestMethod]
    [TestCategory("Unit")]
    public async Task GetTimingsAsync_MalformedOrStaleManifest_RejectsHighlighting()
    {
        using var server = new BlobServer();
        var storage = CreateStorage(server);
        var metadata = DvarTorahAudioTestData.Metadata();
        var path = "/dvar-torah-audio/" + metadata.TimingsBlobName;
        server.Blobs[path] = Encoding.UTF8.GetBytes("{ broken");

        await Assert.ThrowsExactlyAsync<InvalidDataException>(() => storage.GetTimingsAsync(metadata));
        server.Blobs[path] = JsonSerializer.SerializeToUtf8Bytes(DvarTorahAudioTestData.Timings() with { Version = new string('a', 64) }, new JsonSerializerOptions(JsonSerializerDefaults.Web));
        await Assert.ThrowsExactlyAsync<InvalidDataException>(() => storage.GetTimingsAsync(metadata));
    }

    [TestMethod]
    [TestCategory("Unit")]
    public async Task GetInfoAsync_LengthMismatch_RejectsStaleMetadata()
    {
        using var server = new BlobServer();
        var storage = CreateStorage(server);
        var metadata = DvarTorahAudioTestData.Metadata();
        server.Blobs[new Uri(metadata.BlobUri).AbsolutePath] = [1];

        await Assert.ThrowsExactlyAsync<InvalidDataException>(() => storage.GetInfoAsync(metadata));
    }

    [TestMethod]
    [TestCategory("Unit")]
    public async Task Reader_MalformedMetadataFields_RejectsBeforeNetwork()
    {
        using var server = new BlobServer();
        var storage = CreateStorage(server);
        var valid = DvarTorahAudioTestData.Metadata();
        var invalid = new[]
        {
            valid with { BlobName = null! }, valid with { BlobName = valid.BlobName.Replace(valid.Version, new string('a', 64), StringComparison.Ordinal) },
            valid with { BlobName = valid.BlobName.Replace("narration.mp3", "secret.mp3", StringComparison.Ordinal) },
            valid with { AudioLength = 0 }, valid with { AudioLength = 64 * 1024 * 1024 + 1 },
            valid with { DurationMs = double.NaN }, valid with { DurationMs = 0 }, valid with { BlobUri = "not-an-uri" },
        };

        foreach (var metadata in invalid)
        {
            await Assert.ThrowsExactlyAsync<InvalidDataException>(() => storage.GetInfoAsync(metadata));
        }
        await Assert.ThrowsExactlyAsync<ArgumentOutOfRangeException>(() => storage.OpenReadAsync(valid, 0, 0));
        Assert.HasCount(0, server.Requests);
    }

    [TestMethod]
    [TestCategory("Unit")]
    public async Task FindStoredAsync_MalformedCompletionMarker_DoesNotReuseIt()
    {
        using var server = new BlobServer();
        var storage = CreateStorage(server);
        var version = DvarTorahAudioTestData.Timings().Version;
        var path = $"/dvar-torah-audio/diaspora/2026-09-05/{version}/complete.json";
        foreach (var marker in new[] { "", "null", "{ malformed", new string('x', 16_385) })
        {
            server.Blobs[path] = Encoding.UTF8.GetBytes(marker);
            await Assert.ThrowsExactlyAsync<InvalidDataException>(() => storage.FindStoredAsync("diaspora:2026-09-05", version));
        }
        foreach (var metadata in new[] { DvarTorahAudioTestData.Metadata() with { Version = new string('a', 64) }, DvarTorahAudioTestData.Metadata() with { BlobName = "different/week/recording.mp3" }, DvarTorahAudioTestData.Metadata() })
        {
            server.Blobs[path] = JsonSerializer.SerializeToUtf8Bytes(metadata, new JsonSerializerOptions(JsonSerializerDefaults.Web));
            await Assert.ThrowsExactlyAsync<InvalidDataException>(() => storage.FindStoredAsync("diaspora:2026-09-05", version));
        }
        // An MP3 without its paired timings is also an incomplete recording, never a valid recovery target.
        server.Blobs[new Uri(DvarTorahAudioTestData.Metadata().BlobUri).AbsolutePath] = [0xff, 0xfb, 0, 0];
        await Assert.ThrowsExactlyAsync<InvalidDataException>(() => storage.FindStoredAsync("diaspora:2026-09-05", version));
    }

    [TestMethod]
    [TestCategory("Unit")]
    public async Task GetTimingsAsync_EmptyNullOrOversizedData_FailsClosed()
    {
        using var server = new BlobServer();
        var storage = CreateStorage(server);
        var metadata = DvarTorahAudioTestData.Metadata();
        var path = "/dvar-torah-audio/" + metadata.TimingsBlobName;

        foreach (var bytes in new[] { Array.Empty<byte>(), Encoding.UTF8.GetBytes("null"), new byte[8 * 1024 * 1024 + 1] })
        {
            server.Blobs[path] = bytes;
            await Assert.ThrowsExactlyAsync<InvalidDataException>(() => storage.GetTimingsAsync(metadata));
        }
    }

    [TestMethod]
    [TestCategory("Unit")]
    public async Task UploadAsync_InvalidAudioOrOversizedManifest_FailsBeforeNetwork()
    {
        using var server = new BlobServer();
        var storage = CreateStorage(server);
        var valid = DvarTorahAudioTestData.Timings();

        await Assert.ThrowsExactlyAsync<InvalidDataException>(() => storage.UploadAsync("diaspora:2026-09-05", new DvarTorahNarration(new byte[3], valid), DvarTorahAudioTestData.Now));
        await Assert.ThrowsExactlyAsync<InvalidDataException>(() => storage.UploadAsync("diaspora:2026-09-05", new DvarTorahNarration(new byte[64 * 1024 * 1024 + 1], valid), DvarTorahAudioTestData.Now));
        await Assert.ThrowsExactlyAsync<InvalidDataException>(() => storage.UploadAsync("diaspora:2026-09-05", new DvarTorahNarration(new byte[4], valid with { Voice = new string('x', 8 * 1024 * 1024) }), DvarTorahAudioTestData.Now));

        Assert.HasCount(0, server.Requests);
    }

    private static AzureBlobDvarTorahAudioStorage CreateStorage(BlobServer server)
    {
        var options = new BlobClientOptions { Transport = new HttpClientTransport(new HttpClient(server)) };
        options.Retry.MaxRetries = 0;
        return new AzureBlobDvarTorahAudioStorage(new BlobContainerClient(new Uri("https://testaccount.blob.core.windows.net/dvar-torah-audio"), options));
    }

    private sealed class BlobServer : HttpMessageHandler
    {
        public Dictionary<string, byte[]> Blobs { get; } = [];
        public List<Request> Requests { get; } = [];
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var path = request.RequestUri?.AbsolutePath ?? throw new InvalidOperationException("No request path.");
            var range = Header(request, "x-ms-range") ?? Header(request, "Range");
            Requests.Add(new Request(request.Method, path, range, Header(request, "x-ms-access-tier"), Header(request, "If-None-Match")));
            if (request.Method == HttpMethod.Put)
            {
                if (Blobs.ContainsKey(path) && Header(request, "If-None-Match") == "*")
                {
                    var conflict = new HttpResponseMessage(HttpStatusCode.PreconditionFailed) { Content = new StringContent("<Error><Code>ConditionNotMet</Code></Error>") };
                    conflict.Headers.Add("x-ms-error-code", "ConditionNotMet");
                    return conflict;
                }
                Blobs[path] = await (request.Content ?? throw new InvalidOperationException("No upload content.")).ReadAsByteArrayAsync(cancellationToken);
                var created = new HttpResponseMessage(HttpStatusCode.Created);
                created.Headers.ETag = new EntityTagHeaderValue("\"version-1\"");
                created.Headers.Add("x-ms-request-id", "test-request");
                created.Headers.Add("x-ms-version", "2026-02-06");
                return created;
            }
            if (!Blobs.TryGetValue(path, out var bytes))
            {
                var missing = new HttpResponseMessage(HttpStatusCode.NotFound) { Content = new StringContent("<Error><Code>BlobNotFound</Code></Error>") };
                missing.Headers.Add("x-ms-error-code", "BlobNotFound");
                return missing;
            }
            var start = 0;
            var end = bytes.Length - 1;
            if (range is not null)
            {
                var parts = range.Replace("bytes=", string.Empty, StringComparison.Ordinal).Split('-');
                start = int.Parse(parts[0], System.Globalization.CultureInfo.InvariantCulture);
                if (parts[1].Length > 0)
                {
                    end = int.Parse(parts[1], System.Globalization.CultureInfo.InvariantCulture);
                }
            }
            var response = new HttpResponseMessage(range is null ? HttpStatusCode.OK : HttpStatusCode.PartialContent)
            {
                Content = new ByteArrayContent(request.Method == HttpMethod.Head ? [] : bytes[start..(end + 1)]),
            };
            response.Content.Headers.ContentLength = request.Method == HttpMethod.Head ? bytes.Length : end - start + 1;
            response.Content.Headers.LastModified = DvarTorahAudioTestData.Now;
            response.Content.Headers.ContentType = new MediaTypeHeaderValue(path.EndsWith(".json", StringComparison.Ordinal) ? "application/json" : "audio/mpeg");
            if (range is not null)
            {
                response.Content.Headers.ContentRange = new ContentRangeHeaderValue(start, end, bytes.Length);
            }
            response.Headers.ETag = new EntityTagHeaderValue("\"version-1\"");
            response.Headers.Add("x-ms-blob-type", "BlockBlob");
            response.Headers.Add("x-ms-creation-time", DvarTorahAudioTestData.Now.ToString("R"));
            return response;
        }
        private static string? Header(HttpRequestMessage request, string name) => request.Headers.TryGetValues(name, out var values) ? values.First() : null;
    }

    private sealed record Request(HttpMethod Method, string Path, string? Range, string? Tier, string? IfNoneMatch);
}
