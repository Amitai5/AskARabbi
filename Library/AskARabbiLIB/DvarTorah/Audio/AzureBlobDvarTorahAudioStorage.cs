using Azure;
using Azure.Core;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using System.Security.Cryptography;
using System.Text.Json;

namespace AskARabbiLIB.DvarTorah.Audio;

/// <summary>Uses managed-identity Azure Blob access for private, Hot-tier recordings and bounded timing manifests.</summary>
public sealed class AzureBlobDvarTorahAudioStorage : IDvarTorahAudioStorage
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly BlobContainerClient container;

    /// <summary>Initializes the configured private container using an injected Azure credential.</summary>
    /// <param name="options">Storage configuration without secrets.</param>
    /// <param name="credential">Explicit managed identity in production.</param>
    public AzureBlobDvarTorahAudioStorage(DvarTorahAudioOptions options, TokenCredential credential)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(credential);
        options.ValidateStorage();
        var clientOptions = new BlobClientOptions();
        clientOptions.Retry.MaxRetries = 3;
        clientOptions.Retry.NetworkTimeout = TimeSpan.FromSeconds(30);
        container = new BlobServiceClient(new Uri(options.StorageServiceUri), credential, clientOptions).GetBlobContainerClient(options.ContainerName);
    }

    internal AzureBlobDvarTorahAudioStorage(BlobContainerClient container) => this.container = container;

    /// <inheritdoc/>
    public async Task<DvarTorahAudioBlobInfo?> GetInfoAsync(WeeklyDvarTorahAudioMetadata audio, CancellationToken cancellationToken = default)
    {
        var blob = GetValidatedBlob(audio);
        try
        {
            var response = await blob.GetPropertiesAsync(cancellationToken: cancellationToken).ConfigureAwait(false);
            if (response.Value.ContentLength != audio.AudioLength)
            {
                throw new InvalidDataException("Stored audio length does not match its published metadata.");
            }
            return new(response.Value.ContentLength, response.Value.ETag.ToString(), response.Value.LastModified);
        }
        catch (RequestFailedException exception) when (exception.Status == 404)
        {
            return null;
        }
    }

    /// <inheritdoc/>
    public async Task<Stream> OpenReadAsync(WeeklyDvarTorahAudioMetadata audio, long offset, long? length, CancellationToken cancellationToken = default)
    {
        var blob = GetValidatedBlob(audio);
        if (offset < 0 || offset >= audio.AudioLength || length is <= 0 || length > audio.AudioLength - offset)
        {
            throw new ArgumentOutOfRangeException(nameof(offset), "The requested audio byte range is outside the recording.");
        }
        var response = await blob.DownloadStreamingAsync(new BlobDownloadOptions { Range = new HttpRange(offset, length) }, cancellationToken).ConfigureAwait(false);
        return response.Value.Content;
    }

    /// <inheritdoc/>
    public async Task<DvarTorahAudioTimings?> GetTimingsAsync(WeeklyDvarTorahAudioMetadata audio, CancellationToken cancellationToken = default)
    {
        GetValidatedBlob(audio);
        var timings = await ReadTimingsAsync(container.GetBlobClient(audio.TimingsBlobName), cancellationToken).ConfigureAwait(false);
        if (timings is not null && (timings.Version != audio.Version || timings.Voice != audio.Voice || timings.DurationMs != audio.DurationMs))
        {
            throw new InvalidDataException("The timing manifest does not match the published recording.");
        }
        return timings;
    }

    /// <inheritdoc/>
    public async Task<WeeklyDvarTorahAudioMetadata?> FindStoredAsync(string weekKey, string version, CancellationToken cancellationToken = default)
    {
        var prefix = DvarTorahAudioValidation.GetPrefix(weekKey, version);
        try
        {
            var marker = container.GetBlobClient($"{prefix}/complete.json");
            var properties = (await marker.GetPropertiesAsync(cancellationToken: cancellationToken).ConfigureAwait(false)).Value;
            if (properties.ContentLength is <= 0 or > 16_384)
            {
                throw new InvalidDataException("The recording completion marker is invalid.");
            }
            var download = await marker.DownloadStreamingAsync(new BlobDownloadOptions { Conditions = new BlobRequestConditions { IfMatch = properties.ETag }, Range = new HttpRange(0, properties.ContentLength) }, cancellationToken).ConfigureAwait(false);
            await using var stream = download.Value.Content;
            var metadata = await JsonSerializer.DeserializeAsync<WeeklyDvarTorahAudioMetadata>(stream, JsonOptions, cancellationToken).ConfigureAwait(false)
                ?? throw new InvalidDataException("The recording completion marker is empty.");
            if (metadata.Version != version || !metadata.BlobName.StartsWith(prefix + "/", StringComparison.Ordinal))
            {
                throw new InvalidDataException("The completed recording does not match its version and week.");
            }
            if (await GetInfoAsync(metadata, cancellationToken).ConfigureAwait(false) is null || await GetTimingsAsync(metadata, cancellationToken).ConfigureAwait(false) is null)
            {
                throw new InvalidDataException("A completed recording is missing one of its immutable files.");
            }
            return metadata;
        }
        catch (RequestFailedException exception) when (exception.Status == 404)
        {
            return null;
        }
        catch (JsonException exception)
        {
            throw new InvalidDataException("The recording completion marker is malformed.", exception);
        }
    }

    /// <inheritdoc/>
    public async Task<WeeklyDvarTorahAudioMetadata> UploadAsync(string weekKey, DvarTorahNarration narration, DateTimeOffset createdAtUtc, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(narration);
        DvarTorahAudioValidation.ValidateTimings(narration.Timings);
        if (narration.Mp3.Length is < 4 or > DvarTorahAudioValidation.MaximumMp3Bytes)
        {
            throw new InvalidDataException("The encoded MP3 is empty or exceeds the recording size limit.");
        }
        var versionPrefix = DvarTorahAudioValidation.GetPrefix(weekKey, narration.Timings.Version);
        var audioHash = Convert.ToHexStringLower(SHA256.HashData(narration.Mp3.Span));
        var prefix = $"{versionPrefix}/{audioHash}";
        var audioBlob = container.GetBlobClient($"{prefix}/narration.mp3");
        var timingBytes = JsonSerializer.SerializeToUtf8Bytes(narration.Timings, JsonOptions);
        if (timingBytes.Length > DvarTorahAudioValidation.MaximumManifestBytes)
        {
            throw new InvalidDataException("The narration timing manifest exceeds the size limit.");
        }

        // Immutable content-addressed pairs prevent a stale lease owner from overwriting a newer completed recording.
        using var mp3 = new BinaryData(narration.Mp3).ToStream();
        await UploadImmutableAsync(audioBlob, mp3, "audio/mpeg", cancellationToken).ConfigureAwait(false);
        using var timingStream = new MemoryStream(timingBytes, writable: false);
        await UploadImmutableAsync(container.GetBlobClient($"{prefix}/timings.json"), timingStream, "application/json", cancellationToken).ConfigureAwait(false);
        var metadata = CreateMetadata(prefix, audioBlob.Uri, narration.Mp3.Length, createdAtUtc, narration.Timings);
        using var marker = new MemoryStream(JsonSerializer.SerializeToUtf8Bytes(metadata, JsonOptions), writable: false);
        await UploadImmutableAsync(container.GetBlobClient($"{versionPrefix}/complete.json"), marker, "application/json", cancellationToken).ConfigureAwait(false);
        return await FindStoredAsync(weekKey, narration.Timings.Version, cancellationToken).ConfigureAwait(false)
            ?? throw new InvalidDataException("The completed recording could not be recovered after upload.");
    }

    internal BlobClient GetValidatedBlob(WeeklyDvarTorahAudioMetadata audio)
    {
        ArgumentNullException.ThrowIfNull(audio);
        DvarTorahAudioValidation.ValidateVersion(audio.Version);
        var parts = audio.BlobName?.Split('/');
        if (parts is not { Length: 5 } || parts[2] != audio.Version || parts[4] != "narration.mp3" || audio.AudioLength <= 0 || audio.AudioLength > DvarTorahAudioValidation.MaximumMp3Bytes || !double.IsFinite(audio.DurationMs) || audio.DurationMs <= 0)
        {
            throw new InvalidDataException("The recording metadata is invalid.");
        }
        DvarTorahAudioValidation.ValidateVersion(parts[3]);
        var prefix = $"{DvarTorahAudioValidation.GetPrefix($"{parts[0]}:{parts[1]}", audio.Version)}/{parts[3]}";
        var blob = container.GetBlobClient($"{prefix}/narration.mp3");
        if (!string.Equals(audio.BlobName, blob.Name, StringComparison.Ordinal) || audio.TimingsBlobName != $"{prefix}/timings.json" || !Uri.TryCreate(audio.BlobUri, UriKind.Absolute, out var uri) || uri != blob.Uri)
        {
            throw new InvalidDataException("The recording must belong to the configured private Blob container.");
        }
        return blob;
    }

    private static async Task UploadImmutableAsync(BlobClient blob, Stream content, string contentType, CancellationToken cancellationToken)
    {
        try
        {
            await blob.UploadAsync(content, new BlobUploadOptions { AccessTier = AccessTier.Hot, Conditions = new BlobRequestConditions { IfNoneMatch = ETag.All }, HttpHeaders = new BlobHttpHeaders { ContentType = contentType, CacheControl = "private, max-age=86400" } }, cancellationToken).ConfigureAwait(false);
        }
        catch (RequestFailedException exception) when (exception.Status == 412 || exception.Status == 409 && exception.ErrorCode == "BlobAlreadyExists")
        {
            // An immutable file already exists from this or another safe attempt. The completion marker chooses the pair.
        }
    }

    private static async Task<DvarTorahAudioTimings?> ReadTimingsAsync(BlobClient blob, CancellationToken cancellationToken)
    {
        try
        {
            var properties = (await blob.GetPropertiesAsync(cancellationToken: cancellationToken).ConfigureAwait(false)).Value;
            if (properties.ContentLength <= 0 || properties.ContentLength > DvarTorahAudioValidation.MaximumManifestBytes)
            {
                throw new InvalidDataException("The narration timing manifest exceeds the size limit.");
            }
            var download = await blob.DownloadStreamingAsync(new BlobDownloadOptions { Conditions = new BlobRequestConditions { IfMatch = properties.ETag }, Range = new HttpRange(0, properties.ContentLength) }, cancellationToken).ConfigureAwait(false);
            await using var stream = download.Value.Content;
            var timings = await JsonSerializer.DeserializeAsync<DvarTorahAudioTimings>(stream, JsonOptions, cancellationToken).ConfigureAwait(false)
                ?? throw new InvalidDataException("The narration timing manifest is empty.");
            DvarTorahAudioValidation.ValidateTimings(timings);
            return timings;
        }
        catch (RequestFailedException exception) when (exception.Status == 404)
        {
            return null;
        }
        catch (JsonException exception)
        {
            throw new InvalidDataException("The narration timing manifest is malformed.", exception);
        }
    }

    private static WeeklyDvarTorahAudioMetadata CreateMetadata(string prefix, Uri blobUri, long length, DateTimeOffset createdAtUtc, DvarTorahAudioTimings timings) => new()
    {
        Version = timings.Version, Voice = timings.Voice, DurationMs = timings.DurationMs, BlobName = $"{prefix}/narration.mp3",
        BlobUri = blobUri.AbsoluteUri, TimingsBlobName = $"{prefix}/timings.json", AudioLength = length, CreatedAtUtc = createdAtUtc.ToUniversalTime(),
    };

}
