using System.Text.RegularExpressions;

namespace AskARabbiLIB.DvarTorah.Audio;

/// <summary>Configures private narration storage and server-side speech generation without credentials.</summary>
public sealed class DvarTorahAudioOptions
{
    public const string SectionName = "DvarTorahAudio";
    public bool Enabled { get; init; }
    public string StorageServiceUri { get; init; } = string.Empty;
    public string ContainerName { get; init; } = "dvar-torah-audio";
    public string SpeechRegion { get; init; } = "eastus2";
    public string SpeechResourceId { get; init; } = string.Empty;
    public string Voice { get; init; } = "en-US-AndrewMultilingualNeural";
    public string FfmpegPath { get; init; } = "ffmpeg";
    public TimeSpan LeaseDuration { get; init; } = TimeSpan.FromMinutes(30);

    /// <summary>Validates the configured Azure Blob service endpoint and private container.</summary>
    public void ValidateStorage()
    {
        if (!Uri.TryCreate(StorageServiceUri, UriKind.Absolute, out var uri) || uri.Scheme != Uri.UriSchemeHttps || !uri.Host.EndsWith(".blob.core.windows.net", StringComparison.OrdinalIgnoreCase) || uri.AbsolutePath != "/" || !string.IsNullOrEmpty(uri.Query) || !string.IsNullOrEmpty(uri.Fragment) || !string.IsNullOrEmpty(uri.UserInfo) || !uri.IsDefaultPort)
        {
            throw new InvalidOperationException($"{SectionName}:StorageServiceUri must be an HTTPS Azure Blob service URI without credentials or paths.");
        }
        if (!Regex.IsMatch(ContainerName, "^[a-z0-9](?:[a-z0-9]|-(?!-)){1,61}[a-z0-9]$", RegexOptions.CultureInvariant))
        {
            throw new InvalidOperationException($"{SectionName}:ContainerName must be a valid private Blob container name.");
        }
    }

    /// <summary>Validates all settings required by the generator.</summary>
    public void ValidateGeneration()
    {
        ValidateStorage();
        if (!Regex.IsMatch(SpeechRegion, "^[a-z0-9]{2,40}$", RegexOptions.CultureInvariant) || !Regex.IsMatch(SpeechResourceId, "^/subscriptions/[a-fA-F0-9-]{36}/resourceGroups/[^/]+/providers/Microsoft\\.CognitiveServices/accounts/[^/]+$", RegexOptions.CultureInvariant))
        {
            throw new InvalidOperationException($"{SectionName}:SpeechRegion and SpeechResourceId must identify the managed-identity Speech resource.");
        }
        if (!Regex.IsMatch(Voice, "^[a-zA-Z0-9-]{3,120}$", RegexOptions.CultureInvariant) || string.IsNullOrWhiteSpace(FfmpegPath))
        {
            throw new InvalidOperationException($"{SectionName}:Voice and FfmpegPath are required.");
        }
        if (LeaseDuration < TimeSpan.FromMinutes(5) || LeaseDuration > TimeSpan.FromHours(2))
        {
            throw new InvalidOperationException($"{SectionName}:LeaseDuration must be between five minutes and two hours.");
        }
    }
}
