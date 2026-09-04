# AskRabbi backend

`Backend` contains the .NET 10 ASP.NET Core foundation for the production AskRabbi API plus the isolated weekly Dvar Torah Azure Container Apps Job. It provides WorkOS AuthKit authentication, owner-scoped Azure Cosmos DB for MongoDB persistence, saved-conversation APIs, a current-or-latest weekly publication API, personalization, monthly usage enforcement, managed-corpus file-search retrieval, grounded Azure OpenAI answers, deterministic Hebrew-calendar tools, and process health.

`POST /api/conversations` creates a saved conversation together with its first user message; opening or abandoning an empty browser draft never writes to Cosmos DB. That first turn and `POST /api/conversations/{conversationId}/messages` check the current allowance, retrieve only approved Sefaria evidence through a forced Azure OpenAI Responses `file_search` call, expose three bounded local calendar functions when relevant, generate and audit a strict structured draft, persist only validated assistant text plus trusted quotation/context/provenance snapshots, and increment usage only after success. The functions convert a supplied or privately loaded birth date to a Hebrew date, find the weekly parashah or festival-displaced reading for a week or Hebrew birthday anniversary, and return today's Gregorian and Hebrew dates. The first successful structured response also supplies a concise AI-generated title, which the backend applies once and never regenerates for later turns. Retrieval ignores model prose, resolves provenance through the bundled checksum-validated manifest, and reapplies source filters locally. Missing evidence, failed tool calculation, stale corpus metadata, provider failure, or failed quotation/citation validation returns a stable fail-closed status without persisting an assistant answer.

When a source selection is omitted, new conversations use every approved source. Users can narrow that set to the core Torah, Tanakh, Mishnah, and Talmud collections or any other non-empty combination. Existing conversations retain their saved source choices.

Warm answer requests use one bounded managed-corpus search, up to 20 candidates, at most 10 evidence segments, medium answer-model reasoning, low audit-model reasoning, and separate 2,400-token answer and 1,600-token audit budgets. Successful retrievals are cached in process for 10 minutes by normalized query and source filters. The independent grounding audit, exact-quotation checks, citation validation, and fail-closed behavior remain mandatory. Usage and personalization reads run together; successful title/usage writes run together; known conversation context avoids redundant Cosmos reads. Responses expose `Server-Timing` entries for the complete turn, retrieval, and model work.

## Projects

- `AskARabbi.Api` is the production HTTP host and composition root.
- `AskARabbi.Api.Tests` contains hermetic MSTest integration tests with fake identity, persistence, and time boundaries.
- `AskARabbi.DvarTorahJob` is a one-shot .NET 10 executable and Docker image for the grounded weekly write path and its post-publication Azure Speech narration.
- `AskARabbi.DvarTorahJob.Tests` covers disabled execution, publication-before-narration ordering, audio-only backfill, failure recovery, and cancellation without real provider calls.
- `AskARabbiBackend.slnx` owns all four projects.
- Both production hosts reference `AskARabbiLIB` for calendar, Dvar Torah orchestration, account, conversation, personalization, usage, and MongoDB contracts and implementations.

The API and library pin `WorkOS.net` 6.2.0, `MongoDB.Driver` 3.11.0, and `Zmanim` 1.5.0. The shared library also isolates the official Azure Speech and Blob SDKs behind narration/storage boundaries. The weekly job alone installs native Speech dependencies and FFmpeg for one seekable MP3 encode; neither a browser model nor per-listener synthesis is needed. WorkOS and MongoDB avoid custom authentication and wire-protocol clients. Zmanim supplies the weekly parashah schedule while .NET supplies numeric Hebrew-calendar conversion. These integrations must remain covered by dependency updates and security scanning. See [`THIRD_PARTY_NOTICES.md`](../THIRD_PARTY_NOTICES.md) for their notices.

## Configuration and secrets

Every backend `appsettings*.json` file is secret-free. The ignored local `appsettings.json` and tracked `appsettings.example.json` contain only non-sensitive URLs, collection names, CORS, logging, and usage defaults. Store local WorkOS credentials and the complete Cosmos Mongo connection string in .NET User Secrets; the connection string contains both the Azure endpoint and credential and must never enter JSON, frontend configuration, source control, build artifacts, or logs.

Production uses the tracked, secret-free `AskARabbi.Api/appsettings.Production.json`: the API is `https://api.askarabbi.ai`, the frontend and sole credentialed CORS origin are `https://askarabbi.ai`, and the WorkOS callback is `https://api.askarabbi.ai/api/user/callback`. WorkOS and Cosmos credentials remain secret-backed environment variables. Azure OpenAI endpoint, deployment, vector-store ID, corpus fingerprint, and optional tenant ID are non-secret runtime environment variables; production authentication uses the Container App's managed identity. The API Docker image includes only `document-manifest.json` for trusted citation provenance—not raw texts, normalized Markdown, or the SQLite index. After verification passes on a push to `production`, the backend deployment workflow builds both backend Dockerfiles, pushes commit-tagged images to ACR, and updates the API and scheduled job by immutable digest. Follow the [production deployment plan](../docs/PRODUCTION_DEPLOYMENT.md), [managed corpus guide](../docs/MANAGED_VECTOR_STORE.md), and [production readiness checklist](../docs/PRODUCTION_READINESS.md).

.NET User Secrets are the required local credential store. The project already has a `UserSecretsId`, so run these commands from the repository root:

```powershell
dotnet user-secrets --project Backend/AskARabbi.Api set "WorkOS:ApiKey" "sk_test_..."
dotnet user-secrets --project Backend/AskARabbi.Api set "WorkOS:ClientId" "client_..."
dotnet user-secrets --project Backend/AskARabbi.Api set "MongoDB:ConnectionString" "<Azure Cosmos DB for MongoDB connection string>"
```

The local callback, frontend, database, collection, and usage settings remain in secret-free JSON. Environment variables are also supported and override both JSON and User Secrets.

Equivalent deployment variables use double underscores:

```text
WorkOS__ApiKey
WorkOS__ClientId
WorkOS__RedirectUri
WorkOS__FrontendUri
MongoDB__ConnectionString
MongoDB__DatabaseName
MongoDB__DvarTorahCollectionName
DvarTorah__InIsrael
DvarTorah__GenerationLeaseMinutes
DvarTorah__GenerationEnabled
Usage__MonthlyAnswerLimit
Cors__AllowedOrigins__0
AI__ProjectEndpoint
AI__ModelName
AI__VectorStoreId
AI__CorpusFingerprint
AI__TenantId
```

The non-secret performance settings can also be overridden with `AI__MaximumOutputTokens`, `AI__ValidationMaximumOutputTokens`, `AI__ReasoningEffort`, `AI__MaximumCandidates`, `AI__MaximumEvidenceSegments`, `AI__MaximumEvidenceCharacters`, `AI__MaximumEnrichmentHits`, `AI__RecentConversationTurns`, `AI__RetrievalCacheSeconds`, and `AI__RetrievalCacheMaximumEntries`. The answer model defaults to medium reasoning with an 8,000-token combined reasoning-and-structured-output ceiling so a valid concise answer is not cut off by hidden reasoning; the smaller independent grounding audit remains low reasoning. Change either only after measuring answer quality and latency together.

Optional MongoDB collection-name keys are `MongoDB:UsersCollectionName`, `MongoDB:ConversationsCollectionName`, `MongoDB:ConversationMessagesCollectionName`, `MongoDB:ConversationSettingsCollectionName`, `MongoDB:UsageCollectionName`, and `MongoDB:DvarTorahCollectionName`. The weekly publication default is exactly `WeeklyAIDvarTorahs`; the remaining defaults are suitable for a new database. `DvarTorah:InIsrael` selects the application-wide reading cycle and defaults to Diaspora; do not change it after publishing without treating the other cycle as a separate content stream. `DvarTorah:GenerationEnabled` is read only by the scheduled job and defaults to `false`.

In the WorkOS dashboard, configure `http://localhost:5090/api/user/callback` as an exact redirect URI, `http://localhost:5173/` as an allowed sign-out/application URI, and `http://localhost:5173/reset-password` as the password-reset URL. Enable email/password and Google in the same WorkOS environment whose API key and client ID are stored in User Secrets.

`Cors:AllowedOrigins` is an exact origin allow-list; wildcards, paths, queries, and fragments are rejected. Development defaults to `http://localhost:5173` when no origin is configured. Production has no default origin and must set its deployed frontend origin explicitly.

The API deliberately remains runnable without WorkOS, MongoDB, or AI configuration so `GET /health` can support local and deployment smoke tests. With all AI settings omitted, chat fails closed with `ai_unavailable` and no provider call; a partial AI configuration fails startup validation. In the normal `http` profile, authentication or persistence endpoints fail explicitly with `503` rather than falling back to an identity or datastore.

An explicit `local-demo` launch profile is available for frontend integration testing without credentials. It is guarded by `LocalDevelopment:UseDemoServices`, is rejected outside the `Development` environment, uses a deterministic local identity, and keeps account/conversation data only in process memory. It never replaces the production WorkOS/MongoDB registrations.

## HTTP surface

All conversation and conversation-settings routes require the encrypted AskRabbi application cookie. Every datastore operation is scoped by the immutable local user ID; a conversation ID alone never grants access.

| Method and route | Current behavior |
| --- | --- |
| `GET /health` | Reports process health; it does not yet probe WorkOS or Cosmos DB. |
| `GET /api/user/login` | Starts WorkOS AuthKit with short-lived state and S256 PKCE cookies; optional `email`, `provider`, and `screen` query hints select email, Google/Apple/Microsoft, or sign-up. |
| `GET /api/user/callback` | Validates state and PKCE, exchanges the code, upserts the local account, and creates the application cookie. |
| `GET /api/user/session` | Returns the minimum safe account projection. |
| `POST /api/user/forgot-password` | Requests a WorkOS reset email and always returns a non-enumerating `202` for a valid request shape. |
| `POST /api/user/reset-password` | Confirms the WorkOS reset, clears the current AskRabbi cookie, and returns `204`. |
| `POST /api/user/logout` | Clears the local cookie and returns the WorkOS logout destination. |
| `GET /api/conversations` | Returns recent titles and source selections for navigation without loading message bodies. |
| `POST /api/conversations` | Creates a saved conversation from its first user message, processes the first grounded response, and applies its one-time AI-generated title. Add `?compact=true` to return only navigation metadata and the current turn's messages. |
| `GET /api/conversations/{id}` | Loads metadata and ordered messages, including trusted assistant-source snapshots, for one owned conversation. |
| `POST /api/conversations/{id}/messages` | Stores one user message by client idempotency ID and returns canonical context plus a grounded turn status; only validated answers and their trusted sources are persisted and counted. Add `?compact=true` for the bounded current-turn response used by the frontend. |
| `PUT /api/conversations/{id}/title` | Renames one owned conversation. |
| `PUT /api/conversations/{id}/sources` | Replaces its approved source selectors. |
| `DELETE /api/conversations/{id}` | Removes its metadata and message records. |
| `GET /api/dvar-torah` | Returns the upcoming Shabbat metadata and this week's published Dvar Torah, or the most recent earlier publication while the current week is pending. |
| `GET /api/dvar-torah/archive` | Searches and pages prior publication metadata. |
| `GET /api/dvar-torah/archive/{weekKey}` | Returns a prior full publication. |
| `GET`, `HEAD /api/dvar-torah/archive/{weekKey}/audio` | Authenticated MP3 stream for any published current/past week. Supports a single byte range (`206`), invalid/unsatisfiable range rejection (`416`), conditional reads, and `HEAD` without downloading audio. `download=true` supplies an attachment filename. |
| `GET /api/dvar-torah/archive/{weekKey}/audio/timings` | Authenticated timing manifest with exact title/body word offsets for highlighting. |
| `GET /api/conversation-settings/usage` | Returns usage and exact inclusive-start/exclusive-end UTC dates for the current calendar month. |
| `GET /api/conversation-settings/personalization` | Returns configured personalization or an explicit unconfigured envelope. |
| `PUT /api/conversation-settings/personalization` | Validates, normalizes, and replaces personalization. |
| `GET /api/conversation-settings/preferences` | Returns account-backed source-context and product-email defaults. |
| `PUT /api/conversation-settings/preferences` | Replaces those defaults in the user-owned Cosmos settings document. |

## Persistence shape

Azure Cosmos DB for MongoDB is accessed through the official MongoDB .NET driver. Account records, conversation metadata, messages, personalization/preferences, monthly counters, and weekly Dvar Torah publications use separate collections. Weekly publications use deterministic `diaspora|israel:yyyy-MM-dd` IDs and persist generation state, a bounded recovery lease, safe failure codes, and immutable published text. The API reads only complete `Published` documents and falls back no later than the requested Shabbat. Assistant message documents embed only the bounded, validated source citations used for that answer: exact quotations, presented context, canonical Sefaria URL, edition attribution URL, language, license, and excerpt state. Older message documents without the additive `sources` field deserialize with an empty source list. Separating messages from conversation metadata prevents every message append from rewriting an ever-growing conversation document and keeps sidebar queries lightweight. Personalization and general preferences share one document but are updated with field-level Mongo operations so saving either one cannot erase the other. Conversation preferences carry a defaults version: legacy records without that version resolve source context to closed, while a subsequent explicit user save records the current version and preserves the user's choice. Required indexes are created when configured persistence starts.

## Weekly Dvar Torah Container Apps Job

[`AskARabbi.DvarTorahJob`](AskARabbi.DvarTorahJob) is the separate one-shot writer image. The VNet-integrated `askarabbi-weekly-dvar-torah-vnet` Azure Container Apps Job is scheduled with `5 8 * * 0`, which Azure evaluates as Sunday at 08:05 UTC. Each execution reads the Cosmos Mongo connection string from a job-level secret reference, calculates the upcoming Shabbat with the same pinned calendar service as the API, acquires a recoverable Mongo lease, generates grounded text through `IWeeklyDvarTorahGenerator`, and atomically publishes once. Platform retries either return `AlreadyPublished`, observe `GenerationInProgress`, or recover an expired lease.

`DvarTorah__GenerationEnabled` defaults to `false`; the explicit production configuration enables the grounded publication pipeline. After publication, a separate audio coordinator optionally generates an English/Hebrew recording with Azure Speech, stores the MP3 and word timings in Hot-tier private Blob Storage, and attaches recording metadata to the Mongo article. Audio has its own recoverable lease and immutable content/voice version: retries reuse completed uploads, and any audio failure leaves the published text unchanged. An explicit single-week backfill mode does not invoke the text generator. Follow the job [README](AskARabbi.DvarTorahJob/README.md) for configuration and recovery.

### Authenticated narration contract

Publication responses add nullable `audio: { version, voice, durationMs, audioUrl, timingsUrl }`. Older records without audio remain readable and return `audio: null`. The URLs point only to authenticated API routes; private Blob URIs are retained server-side, never returned as SAS or public links. Both audio routes accept `?version=...` and return `409` if the recording changed, preventing an old manifest from highlighting new audio. Versioned responses use private browser caching; shared/CDN caching is prohibited. The stream uses bounded Blob ranges instead of buffering the MP3 in API memory. The API never synthesizes recordings during a read.

The timing response has `schemaVersion: 1`, `version`, `voice`, exact normalized `title` and `body`, `durationMs`, `textOffsetUnit: "UTF-16 code units"`, and `words: [{ section, text, textOffset, textLength, audioOffsetMs, durationMs }]`. The frontend only highlights when the manifest matches the displayed article and active version, and starts the normal authenticated MP3 stream on user interaction. Missing audio returns `404`; storage/manifest failures return a safe `503` while the article remains readable.

API narration configuration is secret-free: `DvarTorahAudio__Enabled=true`, `DvarTorahAudio__StorageServiceUri=https://<account>.blob.core.windows.net/`, and `DvarTorahAudio__ContainerName=dvar-torah-audio`. When disabled, no storage client or credential is created. Production uses the API's system-assigned identity with `Storage Blob Data Reader` on the private container. The job separately uses `Storage Blob Data Contributor` and `Cognitive Services Speech User`, plus its existing database/model access. Storage public network access stays disabled; both cloud hosts use the VNet's private endpoint/DNS, and local downloads go through the authenticated API rather than opening storage to the internet.

The implementation adapts only the relevant invariant `DateOnly` and `TimeOnly` BSON-serialization idea discovered in ClearVowAI. AskRabbi already had focused Azure OpenAI, Key Vault, retrieval, and grounding services, so the older Foundry Agent, SQL, Redis, reflection-tool, and unrelated service code was not duplicated.

## Run and verify

```powershell
dotnet restore Backend/AskARabbiBackend.slnx
dotnet build Backend/AskARabbiBackend.slnx --configuration Release --no-restore
dotnet test Backend/AskARabbiBackend.slnx --configuration Release --no-build --no-restore
dotnet run --project Backend/AskARabbi.Api
dotnet run --project Backend/AskARabbi.DvarTorahJob
```

The development profile listens on `http://localhost:5090`:

```powershell
Invoke-WebRequest http://localhost:5090/health
```

Run the complete browser/API workflow without WorkOS or MongoDB credentials:

```powershell
dotnet run --project Backend/AskARabbi.Api --launch-profile local-demo
```

## Remaining production work

- Deploy this grounded-chat integration and complete authenticated live grounded-answer smoke tests against the bound production corpus.
- Add a persistent usage reservation/finalization record for concurrent retries across multiple replicas; deterministic message IDs already make sequential request retries idempotent.
- Replace the self-contained application ticket with a reviewed shared server-side session/revocation design before public launch. A rotating WorkOS refresh token is protected inside the encrypted `HttpOnly` ticket and is never exposed to JavaScript; near access-token expiry, the API refreshes WorkOS and rotates the ticket. A different device's already-issued ticket can nevertheless remain usable until its next refresh attempt.
- Keep Azure Container Apps managed Data Protection enabled and include session continuity in deployment smoke testing.
- Add the final CSRF policy, rate limits, dependency readiness checks, WorkOS webhooks, account deletion, retention jobs, and live-provider smoke tests. Restrictive credentialed CORS is already enforced from an exact origin allow-list.

See the [authentication design](../docs/AUTHENTICATION.md) and [technical design](../docs/TECHNICAL.md) for the surrounding boundaries.
