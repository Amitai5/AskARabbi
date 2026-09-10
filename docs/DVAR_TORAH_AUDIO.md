# Private Dvar Torah narration

The weekly generator publishes the text first, then invokes a separate narration coordinator. The API never synthesizes speech during a listening request. One recording is reused by every listener of that article version.

## Generation and recovery

1. Load the published article. An audio-only backfill skips research and text generation entirely.
2. Hash the canonical title/body, voice, and narration-format version. Unchanged content reuses its existing recording.
3. Acquire an independent MongoDB audio lease. Concurrent invocations cannot publish competing audio, and a failed attempt never changes the article's published status.
4. Reuse completed private assets when recovering a previously interrupted Mongo update. Otherwise, synthesize bounded sections with Azure Speech Neural and the approved `en-US-AndrewMultilingualNeural` voice.
5. Capture word-boundary times and map words to exact UTF-16 positions in the displayed title/body. Citation markers stay visible but are not narrated. English and Hebrew are handled by the multilingual narrator.
6. Assemble PCM sections and encode one seekable MP3 with server-side FFmpeg. Publish the MP3, timing manifest, and completion marker to private Hot Blob Storage.
7. Conditionally attach recording metadata to the existing `WeeklyAIDvarTorahs` document. The stored URI is private and stable, not a public URL or expiring SAS.

Audio failure leaves the text readable. The job reports failure for operational retry; successful text generation is not repeated. A recording is invalidated when its text, voice, or narration format changes. No user profile or private conversation is sent for speech synthesis.

The `speech-pcm24-mp3-96-v3-flowing-quotations` narration format retains the silent references introduced in v2: alphabetic source IDs (including `[TB]` and `[TC]`), numeric markers, legacy application-rendered quotation-reference labels, and marker-only source appendices. The quotation itself is still spoken. Newly generated articles place exact quotations inside their explanatory paragraphs, following the consistent welcome described in the [writing guide](DVAR_TORAH_WRITING.md).

Requests prefer paragraph or sentence boundaries instead of cutting ordinary sentences at an arbitrary word. Exceptionally long sentences fall back to whitespace, with a surrogate-safe hard limit for unbroken text. SSML collapses repeated whitespace, including masked reference gaps, and maps each emitted character back to its original UTF-16 display position. Word-boundary fallback searches only spoken text so a silent reference cannot steal a word highlight.

The approved male Andrew multilingual voice and English/Hebrew language spans remain unchanged. Voice-scoped `Leading-exact=0ms` and `Tailing-exact=250ms` prevent independently synthesized chunks from stacking their natural edge silence. Sentence and comma prosody remain with the voice; punctuation-silence overrides can conflict with word-boundary events. See Microsoft's [SSML silence documentation](https://learn.microsoft.com/en-us/azure/ai-services/speech-service/speech-synthesis-markup-structure). A listening check on newly synthesized English/Hebrew narration is still needed to assess subjective delivery.

Archived MP3s are not swept or rewritten at deployment. An explicit audio-only backfill on the updated generator can upgrade a selected recording; the normal coordinator also regenerates mismatched narration if a later job execution revisits that same published week. Neither path rewrites an article or moves its old quotation paragraphs; inline quotations apply to newly generated text. The new format produces a new immutable MP3/timings pair, and publication updates the audio metadata only when both are ready.

## Browser/API contract

Published article responses add an optional `audio` object containing `version`, `voice`, `durationMs`, `audioUrl`, and `timingsUrl`. Existing records without narration remain valid and return no ready recording.

- `GET` / `HEAD /api/dvar-torah/archive/{weekKey}/audio` streams the MP3 through the authenticated API. Byte ranges support seeking without buffering the full file in API memory.
- `GET /api/dvar-torah/archive/{weekKey}/audio/timings` returns the bounded timing manifest.
- The optional `version` query pins playback and timings to the same version. A stale version returns `409` rather than mixing old timings with new audio.
- `?download=true` returns an attachment through the same authenticated endpoint.
- Missing recordings return `404`; storage outages return a safe retryable response. Responses are privately cached and never expose storage credentials.

The timing manifest contains canonical title/body text, a schema version, duration, voice, and ordered word events: `section`, `text`, `textOffset`, `textLength`, `audioOffsetMs`, and `durationMs`. Positions use UTF-16 code units, matching JavaScript string offsets. The frontend validates the version and exact displayed text before highlighting. It fetches audio/timings on demand, supports pause, seeking, and speed controls, and stops playback when leaving the article. No frontend speech model or new frontend package is required.

The browser player is docked below the scrollable teaching on both mobile and desktop. **Follow text** scrolls only when the highlighted word approaches the edge; manual scrolling pauses following, source-reader inspection temporarily suspends it, and reduced-motion settings disable animation. Neither seeking nor following triggers new synthesis or a new timing request.

The top of each current or opened archive article shows **About N min read**, derived directly from `audio.durationMs` at 1× speed and rounded up to the next whole minute. The tooltip gives the baseline audio length. This is an audio-based estimate, not a words-per-minute guess or remaining-playback time; it stays unchanged at other playback speeds. Missing, nonfinite, or nonpositive durations omit the estimate. This uses existing article metadata without fetching the MP3 or timings, adding state, or changing an API contract.

## Azure resources and isolation

| Resource | Name / configuration |
| --- | --- |
| Resource group | `AARProduction` |
| Speech | `askarabbi-speech-prod`, East US 2, F0 |
| Storage | `askarabbiaudioprod`, Central US, Standard LRS, Hot |
| Container | `dvar-torah-audio` |
| Private endpoint | `askarabbiaudioprod-blob-pe` |
| Private DNS | `privatelink.blob.core.windows.net` linked to the application VNet |
| VNet | `askarabbi-production-vnet`, `10.82.0.0/16` |
| Container Apps subnet | `container-apps-production`, `10.82.4.0/23`, delegated to `Microsoft.App/environments` |
| Private endpoint subnet | `10.82.2.0/27` |
| Consumption environment | `askarabbi-containerapps-production` |
| API | `askarabbi-api-production` |
| Weekly job | `askarabbi-dvar-torah-production` |

The storage account has public network access disabled, anonymous Blob access disabled, shared-key access disabled, and HTTPS/TLS 1.2 required. Storage DNS resolves to its private endpoint from the VNet. The API has **Storage Blob Data Reader**; the generator has **Storage Blob Data Contributor**, both scoped to the audio container. The generator also has **Cognitive Services Speech User** on the Speech resource. Production uses managed identities, not Speech keys or storage connection strings.

Privileged Azure administrators can still manage these resources and role assignments. “Private” means no public storage data-plane route and only explicitly authorized runtime identities, not isolation from subscription administrators.

The consolidated environment reuses the existing Blob private endpoint and DNS. No new gateway, private endpoint, or DNS zone was added during consolidation; ordinary runtime, storage, network, logging, and Speech usage charges still apply. The API remains public and authenticated. Do not disable TLS checks, turn public storage back on, or add a public SAS to make local debugging easier.

## Provisioning and cutover

Consolidation and DNS/TLS cutover completed on September 10, 2026; both earlier environments and their API/job resources are retired. `infrastructure/production-containerapps.json` and [PRODUCTION_NETWORK.md](PRODUCTION_NETWORK.md) describe the consolidated environment and approved reconstruction procedure. Do not deploy the historical `infrastructure/dvar-torah-audio.json` template: it describes the old environment and would replace the shared VNet subnet configuration. The retired `Stage-PrivateAudioRuntime.ps1` and `Activate-PrivateAudioSchedule.ps1` helpers now stop before any Azure operation.

Routine releases use the [production deployment workflow](PRODUCTION_DEPLOYMENT.md), which updates the new API and job to verified immutable images without changing DNS, data, secrets, or timer ownership. The new generator is the only owner of Sunday `5 8 * * 0` UTC, with a 3600-second timeout, retry limit 2, parallelism 1, and completion count 1. Preserve API scale-to-zero and managed Data Protection. Do not recreate an old environment for a routine release.

## Configuration and backfill

Non-secret generator settings use `Enabled`, `StorageServiceUri`, `ContainerName`, `SpeechRegion`, `SpeechResourceId`, `SpeechServiceUri`, `Voice`, `FfmpegPath`, and `LeaseMinutes` under the `DvarTorahAudio__` prefix. Subnet-restricted production requires `DvarTorahAudio__SpeechServiceUri=https://askarabbi-speech-prod.cognitiveservices.azure.com/` on the unchanged F0 Speech account. The narrator uses that custom host's synthesis WebSocket route and waits for completed word metadata before assembling highlights. The API only needs enabled/storage settings. See the job README and API example configuration for exact defaults. Credentials remain in managed identity or the existing runtime secret store.

For a one-off backfill set `DvarTorahAudio__BackfillWeekKey` **on that execution only**. For the existing September 5, 2026 Nitzavim publication the key is `diaspora:2026-09-05`. Do not leave this override on the scheduled job, or it will keep targeting the old article. Download the finished recording through the authenticated API, not by enabling public Blob access.

```powershell
./scripts/Start-DvarTorahAudioBackfill.ps1 -WeekKey diaspora:2026-09-05
```

The helper defaults to `askarabbi-dvar-torah-production` and validates the selected subscription, Saturday date, consolidated environment, enabled narration, and custom Speech endpoint. It preserves the complete job execution template and secret references and adds the selector only to the one-off start request. It does not modify the job definition or retrieve secret values. Use `-WhatIf` to inspect the target without starting synthesis.

## Dependencies and verification

The server adds Microsoft's Speech SDK for exact synthesis boundaries and Azure Storage Blobs SDK for managed-identity uploads/downloads. Handwritten Speech/WebSocket or Blob signing code would increase maintenance and security risk. The generator image uses a Speech-supported Ubuntu .NET 10 runtime with native audio prerequisites and FFmpeg for one valid seekable MP3; those runtime tools do not run on users' devices.

Tests cover leases/retries, text/version alignment, malformed manifests, trusted blob paths, authentication, byte ranges, missing audio, backfill isolation, playback errors, seeking, and mobile rendering. Live validation must additionally verify Speech authorization, VNet DNS, Blob access, and actual MP3 duration/playback. Unit mocks alone do not establish cloud readiness.

### Verified staging run — September 4, 2026 UTC

- `dotnet test Library/AskARabbiLIB.Tests/AskARabbiLIB.Tests.csproj -c Release --no-restore --collect:"XPlat Code Coverage" --results-directory artifacts/audio-library-coverage-v3`: 772 passed; 80.11% branch coverage, 84.50% line coverage.
- `dotnet test Backend/AskARabbiBackend.slnx -c Release --no-restore`: API 102 and job 23 passed.
- `dotnet build Backend/AskARabbiBackend.slnx -c Release --no-restore`: no warnings or errors.
- `pnpm --dir Frontend verify`: 96 frontend tests, lint, TypeScript, and Vite production build passed.
- Desktop/mobile browser checks used an actual approved sample MP3 with mocked API routes: playback, range requests, Hebrew highlighting, seeking, speed, and navigation cleanup passed. This is UI validation, not a claim that the public domain has been cut over.
- Backfill execution `askarabbi-weekly-dvar-torah-vnet-desdo22` succeeded for `diaspora:2026-09-05`. It reused the published Nitzavim text and generated 403,012.5 ms of narration (4,837,293 MP3 bytes), then persisted audio metadata in Mongo. No backfill selector remained on the job definition.
- Replacement API health returned `200`; an unauthenticated audio request returned `401`. Storage remained Hot with public network, anonymous Blob, and shared-key access disabled.

At this historical checkpoint the original public API and Sunday timer were unchanged. The later completed DNS/TLS cutover, authenticated playback checks, scheduler/workflow switch, and retired-resource cleanup are recorded in [PRODUCTION_NETWORK.md](PRODUCTION_NETWORK.md).

References: [Container Apps VNet configuration](https://learn.microsoft.com/en-us/azure/container-apps/custom-virtual-networks), [Storage private endpoints](https://learn.microsoft.com/en-us/azure/storage/common/storage-private-endpoints), [Speech Entra authentication](https://learn.microsoft.com/en-us/azure/ai-services/speech-service/how-to-configure-azure-ad-auth), [managed certificates](https://learn.microsoft.com/en-us/azure/container-apps/custom-domains-managed-certificates).
