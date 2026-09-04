# Weekly Dvar Torah Container Apps Job

This .NET 10 executable is the isolated weekly write path for the `WeeklyAIDvarTorahs` MongoDB collection. Its Docker image runs in the VNet-integrated `askarabbi-weekly-dvar-torah-vnet` Azure Container Apps Job, whose five-field cron expression is `5 8 * * 0` (Sunday at 08:05 UTC). Each execution performs at most one text-generation attempt, then generates or recovers that publication's narration and exits; it does not run an internal timer or HTTP server.

The job calculates the upcoming Shabbat with the same pinned calendar service as the API, acquires a recoverable MongoDB lease, researches current events, retrieves passages from the approved Sefaria Torah corpus, drafts a structured teaching, runs deterministic grounding checks plus an independent safety/inclusion review, and atomically publishes once. Platform retries either return `AlreadyPublished`, observe `GenerationInProgress`, or recover an expired lease.

## Private narration after publication

After the text is durably published, a separate `WeeklyDvarTorahAudioCoordinator` generates its recording. It also runs after `AlreadyPublished`, so an audio retry never regenerates the article. The default narrator is the approved male `en-US-AndrewMultilingualNeural` Azure Speech voice, with explicit English/Hebrew SSML spans. The generator synthesizes bounded chunks, maps Speech word-boundary events back to the exact displayed UTF-16 title/body offsets, and joins PCM before one server-side FFmpeg MP3 encode. The resulting file contains duration and seek metadata, including on mobile; no model or speech engine downloads to the browser.

An independent Mongo audio lease and a content/voice version prevent concurrent synthesis of the same article. The MP3 and timing manifest use immutable, content-addressed Blob paths. A completion marker allows a retry to attach an already-uploaded recording after a Mongo publication failure without synthesizing it again. Mongo stores the private Blob URI and recording metadata only after both files are ready. An audio error never changes the article's `Published` status or body; the job exits `1` to request a retry and logs a safe audio failure. Cancellation exits `2`.

Blob Storage uses the Hot tier, disabled public network access, private endpoints, and private DNS in the Container Apps VNet. The job's system-assigned identity needs `Storage Blob Data Contributor` on the private container and `Cognitive Services Speech User` on the Speech resource. The API identity receives only `Storage Blob Data Reader`. The API authenticates listeners and streams bytes; it never exposes Blob/SAS URLs or synthesizes audio per playback.

For a single existing publication, start a manual execution with `DvarTorahAudio__BackfillWeekKey=diaspora:2026-09-05` (or another exact published Shabbat key) and `DvarTorahAudio__Enabled=true`. This mode loads only that article and bypasses text generation entirely, even when `DvarTorah__GenerationEnabled=false`; it does not require Azure OpenAI or corpus settings. An unknown or malformed key fails without creating replacement text. Set the override on the one execution, not on the recurring job; otherwise later scheduled runs would continue targeting that same article. Clear it before returning to normal weekly execution.

## Content and source contract

The [writing guide](../../docs/DVAR_TORAH_WRITING.md) defines a beginner-friendly essay with a scene-setting beginning, one evidence-backed argument, and a conclusion returning to the opening idea. The application adds the same welcome to every new article. Independent review must approve its context, structure, and ending as well as grounding and safety. Existing publications are not rewritten automatically.

- Current events come only from curated public-service, government, or institutional RSS/Atom endpoints that require no API key or paid publisher subscription: PBS News, NPR, MIT News, NIST, NASA, and Federal Reserve releases. Commercial subscription publishers are excluded. Individual feed failures are logged and tolerated when enough independent publishers remain.
- Only bounded feed metadata is retained: publisher, headline, short summary, public URL, publication time, and retrieval time. The job does not scrape or republish article bodies.
- Torah passages come from the same fingerprint-verified managed Sefaria corpus used by grounded conversations. Retrieved passages are deterministically restricted to the regular parashah or exact festival reading for that Hebrew date and Israel/Diaspora cycle. An unknown festival range fails closed without publishing.
- Each article features exactly three impactful passages in the body. The model selects only their evidence IDs; application code inserts the exact bounded wording and canonical references from public-domain or CC0 Torah evidence, then rejects any missing or altered quotation. News evidence is never quoted.
- At least 80% of both substantive source weight and sourced teaching claims must be Torah. The article must cite at least eight distinct Torah passages and at least two independent current-events publishers by default.
- A separate model pass blocks unsupported claims, irresponsible Torah interpretation, political persuasion, violence advocacy or glorification, graphic violence, hate or dehumanization, racism, sexism, targeting or alienation of protected/minority groups, exploitation of suffering, and claims that tragedy is divine punishment.
- One repair is allowed. A second grounding, neutrality, or safety failure leaves the week unpublished and records a safe failure code.
- Published records include tags, the central moral teaching, deterministic Torah-grounding percentage, model/review versions, the news research window, and complete bounded Torah/news source provenance. MongoDB indexes the tag array for future archive search.

## Safe pre-generation state

`DvarTorah__GenerationEnabled` defaults to `false`. Without an explicit audio backfill key, the scheduled container writes one structured `WeeklyDvarTorahGenerationDisabled` log and exits successfully without reading MongoDB configuration or constructing a client. `DvarTorahAudio__Enabled` independently defaults to `false`; text publication remains available while narration is disabled.

The generator is implemented, but activation remains fail-closed. Before setting `DvarTorah__GenerationEnabled=true`, configure the existing MongoDB, Azure model, and managed Torah corpus values; grant the job identity access to those resources; run a non-production research/publication smoke test; and complete the activation checklist in [`docs/PRODUCTION_READINESS.md`](../../docs/PRODUCTION_READINESS.md).

## Runtime configuration

| Environment variable | Required | Default |
| --- | --- | --- |
| `DvarTorah__GenerationEnabled` | No | `false` |
| `MongoDB__ConnectionString` | Only when generation is enabled | None; configure as a Container Apps secret reference |
| `MongoDB__DatabaseName` | No | `askarabbi` |
| `MongoDB__DvarTorahCollectionName` | No | `WeeklyAIDvarTorahs` |
| `DvarTorah__InIsrael` | No | `false` (Diaspora cycle) |
| `DvarTorah__GenerationLeaseMinutes` | No | `30` |
| `DvarTorah__ResearchWindowDays` | No | `7` |
| `DvarTorah__ResearchTimeoutMinutes` | No | `25` (maximum `30`) |
| `DvarTorah__MaximumNewsCandidates` | No | `80` |
| `DvarTorah__MinimumNewsPublishers` | No | `2` |
| `DvarTorah__MaximumNewsSources` | No | `4` |
| `DvarTorah__MinimumTorahEvidenceItems` | No | `8` |
| `DvarTorah__MaximumTorahEvidenceItems` | No | `14` |
| `DvarTorah__MinimumTorahGroundingPercent` | No | `80` (cannot be configured lower) |
| `DvarTorah__MinimumBodyCharacters` | No | `2500` |
| `DvarTorah__MaximumBodyCharacters` | No | `15000` |
| `DvarTorah__GeneratorVersion` | No | `weekly-dvar-torah-v3` |
| `AI__ProjectEndpoint` | When generation is enabled | None |
| `AI__ModelName` | When generation is enabled | None |
| `AI__VectorStoreId` | When generation is enabled | None |
| `AI__CorpusFingerprint` | When generation is enabled | None |
| `AI__TenantId` | No | Managed-identity tenant resolution |
| `AI__TimeoutSeconds` | No | `300` |
| `AI__MaximumOutputTokens` | No | `24000` |
| `AI__ValidationMaximumOutputTokens` | No | `12000` |
| `AI__ReasoningEffort` | No | `High` |
| `AI__ValidationReasoningEffort` | No | Same as `AI__ReasoningEffort` (`High` by default) |
| `AI__MaximumRetryCount` | No | `1` |
| `DvarTorahAudio__Enabled` | No | `false` |
| `DvarTorahAudio__StorageServiceUri` | When narration is enabled | HTTPS Blob service URI, without keys, SAS, or container path |
| `DvarTorahAudio__ContainerName` | No | `dvar-torah-audio` |
| `DvarTorahAudio__SpeechRegion` | No | `eastus2` |
| `DvarTorahAudio__SpeechResourceId` | When narration is enabled | Full Azure resource ID of the Speech account |
| `DvarTorahAudio__Voice` | No | `en-US-AndrewMultilingualNeural` |
| `DvarTorahAudio__FfmpegPath` | No | `ffmpeg`, installed in the job image |
| `DvarTorahAudio__LeaseMinutes` | No | `30` (range `5`–`120`) |
| `DvarTorahAudio__BackfillWeekKey` | Manual audio-only runs | Unset; exact `diaspora:yyyy-MM-dd` or `israel:yyyy-MM-dd` published Shabbat |
| `DOTNET_ENVIRONMENT` | Local authenticated development only | Set `Development` to use the developer Azure credential chain |

The MongoDB connection string must remain in the Container Apps Job secret store or an Azure Key Vault reference. Do not pass it as a build argument, commit it to configuration, or print it to logs. Azure model, vector-store, Speech, and Blob access use `ManagedIdentityCredential(ManagedIdentityId.SystemAssigned)` outside explicitly selected Development; no AI, Speech, or Storage account key is required. Local credentials do not bypass private storage networking. Configure the job's replica timeout to at least 3,600 seconds for the default text-plus-audio budgets.

The job runtime is .NET 10 on Ubuntu 24.04 (`10.0-noble`), a [supported Speech SDK platform](https://learn.microsoft.com/en-us/azure/ai-services/speech-service/quickstarts/setup-platform). Only this image installs ALSA (`libasound2t64`) and FFmpeg. The API and frontend do not run these native components. FFmpeg encodes one valid, seekable MP3 instead of concatenating separately encoded MP3 files; it runs without shell interpolation and under the non-root app user.

## Build and local verification

Run from the repository root:

```powershell
dotnet run --project Backend/AskARabbi.DvarTorahJob
docker build --file Backend/AskARabbi.DvarTorahJob/Dockerfile --tag askarabbi-dvar-torah-job:local .
docker run --rm askarabbi-dvar-torah-job:local
```

Both local runs use the safe disabled default and should exit with code `0`. The production workflow builds this Dockerfile, pushes `askarabbi-dvar-torah-job:<verified-commit>` to ACR, resolves its immutable digest, updates the existing Container Apps Job, and verifies the job image, schedule trigger, cron expression, and provisioning state.
