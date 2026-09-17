# Voice questions and answer playback

Chat supports deliberate **Record question → Stop recording → review → Send** turns. Each recording ends after at most 30 seconds. This is a bounded push-to-talk interaction, not an open microphone or a continuous conversation session. Keyboard activation uses the same native buttons. Cancel, navigation, hiding the app, and going offline release microphone tracks and discard pending transcription results.

The transcript is appended to the current draft without sending it automatically. Existing text is preserved; a draft exceeding 4,000 characters is rejected without truncation. Source selection and the existing grounded-answer pipeline remain authoritative. After a voice draft is sent and a validated answer becomes visible, the app prepares its audio. Browser autoplay policies may require pressing Play. Every saved answer also has **Listen to answer**, with native pause, replay, and seek controls. Text and inline citation controls stay visible during preparation, playback, and audio failures. Starting another voice action stops earlier audio.

## API and Azure configuration

Conversation voice is optional and defaults off. Configure the API's existing Azure Speech resource through these secret-free settings:

| Setting | Value |
| --- | --- |
| `VoiceChat__Enabled` | `true` to enable; `false` disables both speech operations |
| `VoiceChat__SpeechRegion` | Existing Speech region, default `eastus2` |
| `VoiceChat__SpeechResourceId` | Full ARM ID of the existing Speech resource |
| `VoiceChat__SpeechServiceUri` | HTTPS custom resource root for the existing subnet-restricted resource; omit for regional access |
| `VoiceChat__Voice` | Default `en-US-AndrewMultilingualNeural` |

The API uses its **own system-assigned managed identity** in production; grant it **Cognitive Services Speech User** on the existing resource through the approved deployment process. The weekly job's assignment does not grant the API access. Retain existing subnet restrictions and custom endpoint routing. Development uses `DefaultAzureCredential`. No Speech key, access token, or resource credential is returned to the browser or placed in `VITE_*`.

The API uses the documented [short-audio recognition](https://learn.microsoft.com/en-us/azure/ai-services/speech-service/rest-speech-to-text-short) and [speech synthesis](https://learn.microsoft.com/en-us/azure/ai-services/speech-service/rest-text-to-speech) REST endpoints with managed-identity Bearer authentication. This bounded interaction needs no native audio runtime, browser SDK, additional package, or image change. Redirects are disabled to keep credentials on the configured Azure host. The weekly job and its SDK-based stored narration remain independent.

New additive endpoints (all require the existing application cookie):

- `GET /api/voice`: `{ enabled: boolean }`; checked before requesting microphone permission.
- `POST /api/voice/transcriptions?language=en-US`: raw mono 16 kHz, signed 16-bit little-endian PCM with `application/octet-stream`, 3,200–960,000 bytes; returns `{ text: string }`. The browser records in its native format and decodes/downmixes/resamples locally before upload.
- `POST /api/voice/conversations/{conversationId}/messages/{messageId}/audio`: returns transient `audio/mpeg` synthesized from the owned saved assistant message. Client-supplied text is ignored. Missing, foreign, or user-authored messages return 404.

Recognition follows the selected conversation language for English, Hebrew, French, German, Italian, Persian, Polish, Russian, and Spanish. Yiddish input remains typed, with an explicit explanation rather than silently recognizing another language. Multilingual output pronunciation depends on the configured Azure voice.

Both speech operations return a visible 503 when disabled or unavailable. Transcription with no speech or an oversized result returns 422. Synthesis rejects answers over 6,000 characters with 422 rather than silently truncating them. Provider deadlines are 60 seconds for recognition and 90 seconds for synthesis. A per-account, per-API-process limiter permits ten voice requests per minute with no queue; a deployment with multiple replicas multiplies that allowance. Audio is synthesized on demand and retained only in browser memory for replay while the answer is mounted. It is not added to offline storage. Reopening an answer generates new audio and incurs another Speech request.

## Privacy and compatibility

Microphone audio and unsent transcripts are not persisted to account storage. A sent transcript is ordinary saved chat history. The canonical answer text goes to Azure Speech for playback; audio is processed in memory and returned with no-store caching. Browser object URLs are revoked on navigation. These application practices do not promise zero provider retention. The public privacy policy documents the new processing without requiring sign-in or JavaScript.

Use HTTPS (or localhost), microphone permission, MediaRecorder, and Web Audio. Unsupported browsers, permission denial, expired authentication, no speech, rate limits, and provider errors leave typed chat usable. The frontend requires no downloaded speech SDK or model. No saved schema, conversation contract, quotation validation, citation metadata, source licensing, or weekly narration contract changes.

## Review and verification

Automated tests use fake identity, persistence, microphone, media, and Speech boundaries. They do not establish live Azure recognition accuracy, real-device recording/decoding compatibility, managed-identity access, or pronunciation. Before enabling, smoke-test the approved deployment with English and Hebrew questions, a pause inside a question, denied permission, cancellation during the permission prompt, a mobile browser, blocked autoplay, and an open citation during playback. Check narrow-screen reflow and native player keyboard controls.

The implementation reuses the existing grounded turn and saved answer to preserve correctness. Performance costs are bounded recording conversion in the browser and one on-demand synthesis request; no continuous service connection or audio persistence is introduced. Dedicated voice boundary types keep provider code outside controllers and leave the shared library unchanged.
