using System.Text.RegularExpressions;

namespace AskARabbi.Api.Voice;

/// <summary>Configures optional, server-side Azure Speech for conversation turns.</summary>
public sealed class VoiceOptions
{
    public const string SectionName = "VoiceChat";
    public const int MaximumAudioBytes = 16_000 * 2 * 30;
    public const int MaximumTextLength = 6000;
    public bool Enabled { get; init; }
    public string SpeechRegion { get; init; } = "eastus2";
    public string SpeechResourceId { get; init; } = string.Empty;
    public string? SpeechServiceUri { get; init; }
    public string Voice { get; init; } = "en-US-AndrewMultilingualNeural";

    /// <summary>Validates enabled Speech settings without reading credentials.</summary>
    public void Validate()
    {
        if (!Enabled)
        {
            return;
        }
        if (!Regex.IsMatch(SpeechRegion, "^[a-z0-9]{2,40}$", RegexOptions.CultureInvariant) ||
            !Regex.IsMatch(SpeechResourceId, "^/subscriptions/[a-fA-F0-9-]{36}/resourceGroups/[^/]+/providers/Microsoft\\.CognitiveServices/accounts/[^/]+$", RegexOptions.CultureInvariant) ||
            !Regex.IsMatch(Voice, "^[a-zA-Z0-9-]{3,120}$", RegexOptions.CultureInvariant))
        {
            throw new InvalidOperationException("VoiceChat must identify an Azure Speech resource, region, and voice.");
        }
        if (SpeechServiceUri is not null &&
            (!Uri.TryCreate(SpeechServiceUri, UriKind.Absolute, out var uri) || uri.Scheme != Uri.UriSchemeHttps ||
             !Regex.IsMatch(uri.Host, "^[a-z0-9](?:[a-z0-9-]*[a-z0-9])?\\.cognitiveservices\\.azure\\.com$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant) ||
             uri.AbsolutePath != "/" || uri.Query.Length != 0 || uri.Fragment.Length != 0 || uri.UserInfo.Length != 0 || !uri.IsDefaultPort))
        {
            throw new InvalidOperationException("VoiceChat:SpeechServiceUri must be an HTTPS custom Azure Speech service root.");
        }
    }
}
