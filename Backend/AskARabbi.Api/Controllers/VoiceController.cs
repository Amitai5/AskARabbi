using AskARabbi.Api.Authentication;
using AskARabbi.Api.Voice;
using AskARabbiLIB.Conversations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace AskARabbi.Api.Controllers;

/// <summary>Provides bounded, authenticated speech without changing the grounded chat contract.</summary>
[ApiController]
[Authorize]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
[Route("api/voice")]
[EnableRateLimiting("conversation-voice")]
public sealed class VoiceController(VoiceOptions options, IVoiceService speech, ConversationService conversations, ICurrentUser currentUser) : ControllerBase
{
    private static readonly HashSet<string> Languages = ["en-US", "he-IL", "fr-FR", "de-DE", "it-IT", "fa-IR", "pl-PL", "ru-RU", "es-ES"];

    /// <summary>Checks configuration before the browser requests microphone permission.</summary>
    /// <returns>Whether optional conversation speech is enabled.</returns>
    [HttpGet]
    public IActionResult GetAvailability() => Ok(new { enabled = options.Enabled });

    /// <summary>Transcribes one recording into an unsaved, editable question.</summary>
    /// <param name="language">Explicit recognition locale.</param>
    /// <param name="cancellationToken">Request cancellation.</param>
    /// <returns>A transcript, or a visible no-speech failure.</returns>
    [HttpPost("transcriptions")]
    [RequestSizeLimit(VoiceOptions.MaximumAudioBytes)]
    public async Task<IActionResult> Transcribe([FromQuery] string language, CancellationToken cancellationToken)
    {
        EnsureEnabled();
        if (!Languages.Contains(language))
        {
            return BadRequest(new { detail = "Choose a supported spoken language." });
        }
        if (!string.Equals(Request.ContentType, "application/octet-stream", StringComparison.OrdinalIgnoreCase))
        {
            return StatusCode(StatusCodes.Status415UnsupportedMediaType);
        }
        // Bound reads independently of Content-Length and the hosting server's request-size feature.
        using var audio = new MemoryStream();
        var buffer = new byte[8192];
        int read;
        while ((read = await Request.Body.ReadAsync(buffer, cancellationToken).ConfigureAwait(false)) > 0)
        {
            if (audio.Length + read > VoiceOptions.MaximumAudioBytes)
            {
                return StatusCode(StatusCodes.Status413PayloadTooLarge);
            }
            await audio.WriteAsync(buffer.AsMemory(0, read), cancellationToken).ConfigureAwait(false);
        }
        if (audio.Length < 3200 || audio.Length % 2 != 0)
        {
            return BadRequest(new { detail = "Record a question between a tenth of a second and 30 seconds." });
        }
        var text = await speech.TranscribeAsync(audio.ToArray(), language, cancellationToken).ConfigureAwait(false);
        if (string.IsNullOrWhiteSpace(text))
        {
            return UnprocessableEntity(new { detail = "No speech was recognized. Try again or type your question." });
        }
        if (text.Length > 4000)
        {
            return UnprocessableEntity(new { detail = "That question is too long. Try a shorter recording or type your question." });
        }
        return Ok(new { text });
    }

    /// <summary>Reads only an owned, saved assistant answer; browser-supplied text is never synthesized.</summary>
    /// <param name="conversationId">Owned conversation ID.</param>
    /// <param name="messageId">Validated assistant message ID.</param>
    /// <param name="cancellationToken">Request cancellation.</param>
    /// <returns>Transient MP3 audio without a public storage URL.</returns>
    [HttpPost("conversations/{conversationId:guid}/messages/{messageId:guid}/audio")]
    public async Task<IActionResult> Synthesize(Guid conversationId, Guid messageId, CancellationToken cancellationToken)
    {
        EnsureEnabled();
        var conversation = await conversations.GetAsync(currentUser.UserId, conversationId, cancellationToken).ConfigureAwait(false);
        var message = conversation?.Messages.FirstOrDefault(value => value.Id == messageId && value.Role == ConversationMessageRole.Assistant);
        if (message is null)
        {
            return NotFound();
        }
        if (string.IsNullOrWhiteSpace(message.Content) || message.Content.Length > VoiceOptions.MaximumTextLength)
        {
            return UnprocessableEntity(new { detail = "This answer cannot be read aloud. Its text and sources are still available." });
        }
        var mp3 = await speech.SynthesizeAsync(message.Content, cancellationToken).ConfigureAwait(false);
        return File(mp3, "audio/mpeg");
    }

    private void EnsureEnabled()
    {
        if (!options.Enabled)
        {
            throw new VoiceUnavailableException();
        }
    }
}
