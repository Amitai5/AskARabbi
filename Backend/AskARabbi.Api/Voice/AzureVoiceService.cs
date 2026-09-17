using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Xml.Linq;
using Azure.Core;
using Azure.Identity;

namespace AskARabbi.Api.Voice;

/// <summary>Uses bounded Azure Speech REST calls and managed identity without native audio dependencies.</summary>
public sealed class AzureVoiceService(VoiceOptions options, TokenCredential credential, HttpClient client) : IVoiceService
{
    /// <inheritdoc/>
    public async Task<string> TranscribeAsync(byte[] pcm, string language, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(pcm);
        ArgumentException.ThrowIfNullOrWhiteSpace(language);
        if (pcm.Length < 3200 || pcm.Length > VoiceOptions.MaximumAudioBytes || pcm.Length % 2 != 0)
        {
            throw new ArgumentException("A bounded 16-bit PCM recording is required.", nameof(pcm));
        }
        using var content = new ByteArrayContent(CreateWave(pcm));
        content.Headers.ContentType = MediaTypeHeaderValue.Parse("audio/wav; codecs=\"audio/pcm\"; samplerate=16000");
        var endpoint = options.SpeechServiceUri is null
            ? $"https://{options.SpeechRegion}.stt.speech.microsoft.com/speech/recognition/conversation/cognitiveservices/v1"
            : $"{options.SpeechServiceUri.TrimEnd('/')}/stt/speech/recognition/conversation/cognitiveservices/v1";
        var bytes = await SendAsync($"{endpoint}?language={Uri.EscapeDataString(language)}&format=simple", content, false, cancellationToken).ConfigureAwait(false);
        try
        {
            using var document = JsonDocument.Parse(bytes);
            var root = document.RootElement;
            var status = root.GetProperty("RecognitionStatus").GetString();
            return status switch
            {
                "Success" => root.GetProperty("DisplayText").GetString() ?? string.Empty,
                "NoMatch" or "InitialSilenceTimeout" or "BabbleTimeout" => string.Empty,
                _ => throw new VoiceUnavailableException(),
            };
        }
        catch (Exception exception) when (exception is JsonException or KeyNotFoundException or InvalidOperationException)
        {
            throw new VoiceUnavailableException();
        }
    }

    /// <inheritdoc/>
    public async Task<byte[]> SynthesizeAsync(string text, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(text);
        if (text.Length > VoiceOptions.MaximumTextLength)
        {
            throw new ArgumentException("The answer exceeds the voice limit.", nameof(text));
        }
        XNamespace speech = "http://www.w3.org/2001/10/synthesis";
        // XML text nodes escape saved quotations and prevent text from injecting SSML.
        var ssml = new XElement(speech + "speak", new XAttribute("version", "1.0"), new XAttribute(XNamespace.Xml + "lang", "en-US"),
            new XElement(speech + "voice", new XAttribute("name", options.Voice), text)).ToString(SaveOptions.DisableFormatting);
        using var content = new StringContent(ssml, Encoding.UTF8, "application/ssml+xml");
        var endpoint = options.SpeechServiceUri is null
            ? $"https://{options.SpeechRegion}.tts.speech.microsoft.com/cognitiveservices/v1"
            : $"{options.SpeechServiceUri.TrimEnd('/')}/tts/cognitiveservices/v1";
        return await SendAsync(endpoint, content, true, cancellationToken).ConfigureAwait(false);
    }

    private async Task<byte[]> SendAsync(string endpoint, HttpContent content, bool synthesis, CancellationToken cancellationToken)
    {
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        deadline.CancelAfter(TimeSpan.FromSeconds(synthesis ? 90 : 60));
        try
        {
            var token = await credential.GetTokenAsync(new TokenRequestContext(["https://cognitiveservices.azure.com/.default"]), deadline.Token).ConfigureAwait(false);
            using var request = new HttpRequestMessage(HttpMethod.Post, endpoint) { Content = content };
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", $"aad#{options.SpeechResourceId}#{token.Token}");
            if (synthesis)
            {
                request.Headers.Add("X-Microsoft-OutputFormat", "audio-24khz-48kbitrate-mono-mp3");
                request.Headers.UserAgent.ParseAdd("AskARabbi-Voice/1.0");
            }
            using var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, deadline.Token).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
            {
                // Do not log provider bodies, headers, utterances, or access tokens.
                throw new VoiceUnavailableException();
            }
            var maximumBytes = synthesis ? 4_000_000 : 64_000;
            using var output = new MemoryStream();
            await using var input = await response.Content.ReadAsStreamAsync(deadline.Token).ConfigureAwait(false);
            var buffer = new byte[8192];
            int read;
            while ((read = await input.ReadAsync(buffer, deadline.Token).ConfigureAwait(false)) > 0)
            {
                if (output.Length + read > maximumBytes)
                {
                    throw new VoiceUnavailableException();
                }
                await output.WriteAsync(buffer.AsMemory(0, read), deadline.Token).ConfigureAwait(false);
            }
            if (output.Length == 0)
            {
                throw new VoiceUnavailableException();
            }
            return output.ToArray();
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            throw new VoiceUnavailableException();
        }
        catch (Exception exception) when (exception is AuthenticationFailedException or HttpRequestException or IOException)
        {
            throw new VoiceUnavailableException();
        }
    }

    private static byte[] CreateWave(byte[] pcm)
    {
        using var stream = new MemoryStream(44 + pcm.Length);
        using var writer = new BinaryWriter(stream, Encoding.ASCII, leaveOpen: true);
        writer.Write("RIFF"u8);
        writer.Write(36 + pcm.Length);
        writer.Write("WAVEfmt "u8);
        writer.Write(16);
        writer.Write((short)1);
        writer.Write((short)1);
        writer.Write(16_000);
        writer.Write(32_000);
        writer.Write((short)2);
        writer.Write((short)16);
        writer.Write("data"u8);
        writer.Write(pcm.Length);
        writer.Write(pcm);
        return stream.ToArray();
    }
}
