# Weekly Dvar Torah Container Apps Job

This .NET 10 executable is the isolated weekly write path for the `WeeklyAIDvarTorahs` MongoDB collection. Its Docker image is built for the `askarabbi-weekly-dvar-torah` Azure Container Apps Job, whose five-field cron expression is `5 8 * * 0` (Sunday at 08:05 UTC). Each execution performs at most one generation attempt and exits; it does not run an internal timer or HTTP server.

The job calculates the upcoming Shabbat with the same pinned calendar service as the API, acquires a recoverable MongoDB lease, researches current events, retrieves passages from the approved Sefaria Torah corpus, drafts a structured teaching, runs deterministic grounding checks plus an independent safety/inclusion review, and atomically publishes once. Platform retries either return `AlreadyPublished`, observe `GenerationInProgress`, or recover an expired lease.

## Content and source contract

- Current events come only from curated public-service, government, or institutional RSS/Atom endpoints that require no API key or paid publisher subscription: PBS News, NPR, MIT News, NIST, NASA, and Federal Reserve releases. Commercial subscription publishers are excluded. Individual feed failures are logged and tolerated when enough independent publishers remain.
- Only bounded feed metadata is retained: publisher, headline, short summary, public URL, publication time, and retrieval time. The job does not scrape or republish article bodies.
- Torah passages come from the same fingerprint-verified managed Sefaria corpus used by grounded conversations. Retrieved passages are deterministically restricted to the regular parashah or exact festival reading for that Hebrew date and Israel/Diaspora cycle. An unknown festival range fails closed without publishing.
- Each article features exactly three impactful passages in the body. The model selects only their evidence IDs; application code inserts the exact bounded wording and canonical references from public-domain or CC0 Torah evidence, then rejects any missing or altered quotation. News evidence is never quoted.
- At least 80% of both substantive source weight and sourced teaching claims must be Torah. The article must cite at least eight distinct Torah passages and at least two independent current-events publishers by default.
- A separate model pass blocks unsupported claims, irresponsible Torah interpretation, political persuasion, violence advocacy or glorification, graphic violence, hate or dehumanization, racism, sexism, targeting or alienation of protected/minority groups, exploitation of suffering, and claims that tragedy is divine punishment.
- One repair is allowed. A second grounding, neutrality, or safety failure leaves the week unpublished and records a safe failure code.
- Published records include tags, the central moral teaching, deterministic Torah-grounding percentage, model/review versions, the news research window, and complete bounded Torah/news source provenance. MongoDB indexes the tag array for future archive search.

## Safe pre-generation state

`DvarTorah__GenerationEnabled` defaults to `false`. In that state the scheduled container writes one structured `WeeklyDvarTorahGenerationDisabled` log and exits successfully without reading MongoDB configuration or constructing a client. This lets the image, schedule, identity, and deployment path exist before the content contract is approved.

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
| `DvarTorah__GeneratorVersion` | No | `weekly-dvar-torah-v2` |
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

The MongoDB connection string must remain in the Container Apps Job secret store or an Azure Key Vault reference. Do not pass it as a build argument, commit it to configuration, or print it to logs. Azure model and vector-store access should use the job's system-assigned managed identity; no AI API key is required.

## Build and local verification

Run from the repository root:

```powershell
dotnet run --project Backend/AskARabbi.DvarTorahJob
docker build --file Backend/AskARabbi.DvarTorahJob/Dockerfile --tag askarabbi-dvar-torah-job:local .
docker run --rm askarabbi-dvar-torah-job:local
```

Both local runs use the safe disabled default and should exit with code `0`. The production workflow builds this Dockerfile, pushes `askarabbi-dvar-torah-job:<verified-commit>` to ACR, resolves its immutable digest, updates the existing Container Apps Job, and verifies the job image, schedule trigger, cron expression, and provisioning state.
