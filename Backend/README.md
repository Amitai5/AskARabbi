# AskRabbi backend

`Backend` contains the .NET 10 ASP.NET Core foundation for the production AskRabbi API. It provides WorkOS AuthKit authentication, owner-scoped Azure Cosmos DB for MongoDB persistence, saved-conversation APIs, personalization, monthly usage enforcement, managed-corpus file-search retrieval, grounded Azure OpenAI answers, and process health.

`POST /api/conversations` creates a saved conversation together with its first user message; opening or abandoning an empty browser draft never writes to Cosmos DB. That first turn and `POST /api/conversations/{conversationId}/messages` check the current allowance, retrieve only approved Sefaria evidence through a forced Azure OpenAI Responses `file_search` call, generate and audit a strict structured draft, persist only validated assistant text plus trusted quotation/context/provenance snapshots, and increment usage only after success. The first successful structured response also supplies a concise AI-generated title, which the backend applies once and never regenerates for later turns. Retrieval ignores model prose, resolves provenance through the bundled checksum-validated manifest, and reapplies source filters locally. Missing evidence, stale corpus metadata, provider failure, or failed quotation/citation validation returns a stable fail-closed status without persisting an assistant answer.

When a source selection is omitted, new conversations use the core Torah, Tanakh, Mishnah, and Talmud collections. Supplemental approved works must be selected explicitly. Existing conversations retain their saved source choices.

Warm answer requests use one bounded managed-corpus search, up to 20 candidates, at most 10 evidence segments, low model reasoning, and separate 2,400-token answer and 800-token audit budgets. Successful retrievals are cached in process for 10 minutes by normalized query and source filters. The independent grounding audit, exact-quotation checks, citation validation, and fail-closed behavior remain mandatory. Usage and personalization reads run together; successful title/usage writes run together; known conversation context avoids redundant Cosmos reads. Responses expose `Server-Timing` entries for the complete turn, retrieval, and model work.

## Projects

- `AskARabbi.Api` is the production HTTP host and composition root.
- `AskARabbi.Api.Tests` contains hermetic MSTest integration tests with fake identity, persistence, and time boundaries.
- `AskARabbiBackend.slnx` owns both projects.
- `AskARabbi.Api` references `AskARabbiLIB` for account, conversation, personalization, usage, and MongoDB contracts and implementations.

Two production dependencies are pinned for this milestone: `WorkOS.net` 6.2.0 in the API and `MongoDB.Driver` 3.11.0 in the reusable library. The official SDKs avoid maintaining custom authentication and Mongo wire-protocol clients; the tradeoff is additional binary/dependency surface that must remain covered by routine dependency updates and security scanning. WorkOS stays behind `IUserAuthenticationService`, and MongoDB stays behind store interfaces, so either provider can be replaced without changing controllers or domain services.

## Configuration and secrets

Every backend `appsettings*.json` file is secret-free. The ignored local `appsettings.json` and tracked `appsettings.example.json` contain only non-sensitive URLs, collection names, CORS, logging, and usage defaults. Store local WorkOS credentials and the complete Cosmos Mongo connection string in .NET User Secrets; the connection string contains both the Azure endpoint and credential and must never enter JSON, frontend configuration, source control, build artifacts, or logs.

Production uses the tracked, secret-free `AskARabbi.Api/appsettings.Production.json`: the API is `https://api.askarabbi.ai`, the frontend and sole credentialed CORS origin are `https://askarabbi.ai`, and the WorkOS callback is `https://api.askarabbi.ai/api/user/callback`. WorkOS and Cosmos credentials remain secret-backed environment variables. Azure OpenAI endpoint, deployment, vector-store ID, corpus fingerprint, and optional tenant ID are non-secret runtime environment variables; production authentication uses the Container App's managed identity. The Docker image includes only `document-manifest.json` for trusted citation provenance—not raw texts, normalized Markdown, or the SQLite index. After verification passes on a push to `production`, the backend deployment workflow builds the Dockerfile, pushes the commit-tagged image to ACR, and updates Azure Container Apps by immutable digest. Follow the [production deployment plan](../docs/PRODUCTION_DEPLOYMENT.md), [managed corpus guide](../docs/MANAGED_VECTOR_STORE.md), and [production readiness checklist](../docs/PRODUCTION_READINESS.md).

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
Usage__MonthlyAnswerLimit
Cors__AllowedOrigins__0
AI__ProjectEndpoint
AI__ModelName
AI__VectorStoreId
AI__CorpusFingerprint
AI__TenantId
```

The non-secret performance settings can also be overridden with `AI__MaximumOutputTokens`, `AI__ValidationMaximumOutputTokens`, `AI__ReasoningEffort`, `AI__MaximumCandidates`, `AI__MaximumEvidenceSegments`, `AI__MaximumEvidenceCharacters`, `AI__MaximumEnrichmentHits`, `AI__RecentConversationTurns`, `AI__RetrievalCacheSeconds`, and `AI__RetrievalCacheMaximumEntries`. The tracked production defaults favor conversational answers and one-pass retrieval; raise them only after measuring answer quality and latency together.

Optional MongoDB collection-name keys are `MongoDB:UsersCollectionName`, `MongoDB:ConversationsCollectionName`, `MongoDB:ConversationMessagesCollectionName`, `MongoDB:ConversationSettingsCollectionName`, and `MongoDB:UsageCollectionName`. Their defaults are suitable for a new database.

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
| `GET /api/conversation-settings/usage` | Returns usage and exact inclusive-start/exclusive-end UTC dates for the current calendar month. |
| `GET /api/conversation-settings/personalization` | Returns configured personalization or an explicit unconfigured envelope. |
| `PUT /api/conversation-settings/personalization` | Validates, normalizes, and replaces personalization. |
| `GET /api/conversation-settings/preferences` | Returns account-backed source-context and product-email defaults. |
| `PUT /api/conversation-settings/preferences` | Replaces those defaults in the user-owned Cosmos settings document. |

## Persistence shape

Azure Cosmos DB for MongoDB is accessed through the official MongoDB .NET driver. Account records, conversation metadata, messages, personalization/preferences, and monthly counters use separate collections. Assistant message documents embed only the bounded, validated source citations used for that answer: exact quotations, presented context, canonical Sefaria URL, edition attribution URL, language, license, and excerpt state. Older message documents without the additive `sources` field deserialize with an empty source list. Separating messages from conversation metadata prevents every message append from rewriting an ever-growing conversation document and keeps sidebar queries lightweight. Personalization and general preferences share one document but are updated with field-level Mongo operations so saving either one cannot erase the other. Conversation preferences carry a defaults version: legacy records without that version resolve source context to closed, while a subsequent explicit user save records the current version and preserves the user's choice. Required indexes are created when configured persistence starts.

The implementation adapts only the relevant invariant `DateOnly` and `TimeOnly` BSON-serialization idea discovered in ClearVowAI. AskRabbi already had focused Azure OpenAI, Key Vault, retrieval, and grounding services, so the older Foundry Agent, SQL, Redis, reflection-tool, and unrelated service code was not duplicated.

## Run and verify

```powershell
dotnet restore Backend/AskARabbiBackend.slnx
dotnet build Backend/AskARabbiBackend.slnx --configuration Release --no-restore
dotnet test Backend/AskARabbiBackend.slnx --configuration Release --no-build --no-restore
dotnet run --project Backend/AskARabbi.Api
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
