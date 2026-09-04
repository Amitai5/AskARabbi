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

## Browser/API contract

Published article responses add an optional `audio` object containing `version`, `voice`, `durationMs`, `audioUrl`, and `timingsUrl`. Existing records without narration remain valid and return no ready recording.

- `GET` / `HEAD /api/dvar-torah/archive/{weekKey}/audio` streams the MP3 through the authenticated API. Byte ranges support seeking without buffering the full file in API memory.
- `GET /api/dvar-torah/archive/{weekKey}/audio/timings` returns the bounded timing manifest.
- The optional `version` query pins playback and timings to the same version. A stale version returns `409` rather than mixing old timings with new audio.
- `?download=true` returns an attachment through the same authenticated endpoint.
- Missing recordings return `404`; storage outages return a safe retryable response. Responses are privately cached and never expose storage credentials.

The timing manifest contains canonical title/body text, a schema version, duration, voice, and ordered word events: `section`, `text`, `textOffset`, `textLength`, `audioOffsetMs`, and `durationMs`. Positions use UTF-16 code units, matching JavaScript string offsets. The frontend validates the version and exact displayed text before highlighting. It fetches audio/timings on demand, supports pause, seeking, and speed controls, and stops playback when leaving the article. No frontend speech model or new frontend package is required.

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
| Container Apps subnet | `10.82.0.0/23`, delegated to `Microsoft.App/environments` |
| Private endpoint subnet | `10.82.2.0/27` |
| Replacement Consumption environment | `askarabbi-production-private-env` |
| Replacement API | `askarabbi-api-vnet` |
| Replacement weekly job | `askarabbi-weekly-dvar-torah-vnet` |

The storage account has public network access disabled, anonymous Blob access disabled, shared-key access disabled, and HTTPS/TLS 1.2 required. Storage DNS resolves to its private endpoint from the VNet. The API has **Storage Blob Data Reader**; the generator has **Storage Blob Data Contributor**, both scoped to the audio container. The generator also has **Cognitive Services Speech User** on the Speech resource. Production uses managed identities, not Speech keys or storage connection strings.

Privileged Azure administrators can still manage these resources and role assignments. “Private” means no public storage data-plane route and only explicitly authorized runtime identities, not isolation from subscription administrators.

Private networking adds a billed private endpoint, DNS, and the load balancer/public IP managed by Container Apps. Hot LRS storage and Speech usage are additional; monitor the subscription budget and Speech F0 allowance. The API itself remains public and authenticated. Do not disable TLS checks, turn public storage back on, or add a public SAS to make local debugging easier.

## Provisioning and cutover

The original environment cannot be retrofitted with a custom VNet. `infrastructure/dvar-torah-audio.json` therefore creates the private network and a replacement Consumption environment, without touching the original runtime. It references the existing Log Analytics workspace key inside ARM; no credential is saved in the template.

```powershell
az deployment group create --resource-group AARProduction --name askarabbi-private-audio-network --template-file infrastructure/dvar-torah-audio.json --mode Incremental
./scripts/Stage-PrivateAudioRuntime.ps1 -Phase Bootstrap
./scripts/Stage-PrivateAudioRuntime.ps1 -Phase Configure
```

The staging script copies existing API/job secrets directly in memory and keeps the replacement job **Manual**. It does not change DNS or the original Sunday schedule. Bootstrap refuses to overwrite existing replacements. Configure refuses to overwrite an API that already has a custom domain.

Before cutover:

1. Deploy verified images to the replacements. Check the provider `/health`, authenticated audio behavior, and a successful one-off narration backfill.
2. Update only the `api.askarabbi.ai` CNAME and Azure ownership TXT record in the approved DNS account. Keep the CNAME DNS-only and pointing directly to the new Container App for Azure-managed certificate issuance/renewal. Bind and validate the new TLS certificate; never bypass a certificate warning.
3. Validate WorkOS login, existing Mongo data, streaming, seeking, and highlighting through the public domain. A runtime migration can require users to sign in again; account data remains in the same Mongo database.
4. Disable the old job timer before enabling the new job's Sunday `5 8 * * 0` UTC timer. Preserve retry limit 2, parallelism 1, completion count 1, and use a 3600-second replica timeout for text plus narration.
5. Point the production deployment workflow at the replacement API/job. Keep the old environment available for rollback until the migration is confirmed. Do not delete it as part of an automated retry.

## Configuration and backfill

Non-secret generator settings use `DvarTorahAudio__Enabled`, `StorageServiceUri`, `ContainerName`, `SpeechRegion`, `SpeechResourceId`, `Voice`, `FfmpegPath`, and `LeaseMinutes` under the `DvarTorahAudio__` prefix. The API only needs enabled/storage settings. See the job README and API example configuration for exact defaults. Credentials remain in managed identity or the existing runtime secret store.

For a one-off backfill set `DvarTorahAudio__BackfillWeekKey` **on that execution only**. For the existing September 5, 2026 Nitzavim publication the key is `diaspora:2026-09-05`. Do not leave this override on the scheduled job, or it will keep targeting the old article. Download the finished recording through the authenticated API, not by enabling public Blob access.

```powershell
./scripts/Start-DvarTorahAudioBackfill.ps1 -WeekKey diaspora:2026-09-05
```

The helper validates the selected subscription and Saturday date, preserves the complete job execution template and secret references, and adds the selector only to the one-off start request. It does not modify the job definition or retrieve secret values. Use `-WhatIf` to inspect the target without starting synthesis.

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

At this checkpoint the original public API and Sunday timer are unchanged. DNS/TLS cutover, authenticated live playback/download, frontend publication, and final scheduler/workflow switch remain separate acceptance steps requiring the approved DNS change.

References: [Container Apps VNet configuration](https://learn.microsoft.com/en-us/azure/container-apps/custom-virtual-networks), [Storage private endpoints](https://learn.microsoft.com/en-us/azure/storage/common/storage-private-endpoints), [Speech Entra authentication](https://learn.microsoft.com/en-us/azure/ai-services/speech-service/how-to-configure-azure-ad-auth), [managed certificates](https://learn.microsoft.com/en-us/azure/container-apps/custom-domains-managed-certificates).
