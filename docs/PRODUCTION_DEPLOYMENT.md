# Production deployment plan

## Fixed public topology

AskRabbi uses one public web origin and one API origin:

| Surface | Production URL | Purpose |
| --- | --- | --- |
| Frontend | `https://askarabbi.ai` | React application, sign-in screen, conversations, settings, and password-reset UI |
| Backend | `https://api.askarabbi.ai` | ASP.NET Core API, WorkOS callback, application cookie, and Cosmos DB access |
| WorkOS callback | `https://api.askarabbi.ai/api/user/callback` | Exact OAuth redirect URI registered with WorkOS |
| Password reset | `https://askarabbi.ai/reset-password` | SPA route that consumes WorkOS's `token` query parameter |

The tracked backend `appsettings.Production.json` contains these non-secret URLs, exact production CORS origin, collection names, and API host name. The Vite production build defaults to `https://api.askarabbi.ai`. Secrets are supplied only to the backend at runtime.

## Production runtime target

Production was consolidated into one VNet-integrated Azure Container Apps environment on September 10, 2026. DNS cutover and old-runtime deletion are complete. [PRODUCTION_NETWORK.md](PRODUCTION_NETWORK.md) records the deployed topology, verification, and remaining release bookkeeping; the ordered runbook below is retained for reconstruction, not for recreating the retired environments.

| Resource | Required value |
| --- | --- |
| Resource group | `AARProduction` |
| Container registry | `askarabbiacrprod.azurecr.io` |
| Registry SKU | Basic; admin credentials disabled |
| Container Apps environment | `askarabbi-containerapps-production` |
| VNet / delegated subnet | `askarabbi-production-vnet` / `container-apps-production` (`10.82.4.0/23`) |
| Container App | `askarabbi-api-production` |
| Provider hostname | `askarabbi-api-production.thankfulgrass-b0d2066a.centralus.azurecontainerapps.io`; read the live resource before future DNS changes |
| Runtime port | `8080` |
| API image repository | `askarabbi-api` |
| Scheduled Container Apps Job | `askarabbi-dvar-torah-production` |
| Job image repository | `askarabbi-dvar-torah-job` |
| Job schedule | `5 8 * * 0` (Sunday 08:05 UTC) |
| Job execution policy | 3600-second timeout; two retries; one parallel replica; one successful completion |
| API scaling | Minimum zero replicas; retain the existing maximum and scaling rules |
| Cosmos MongoDB database | `askarabbi` |

The API uses its system-assigned managed identity to pull from ACR; the scheduled job must be provisioned with its own system identity and `AcrPull` assignment. Runtime secrets remain on the individual Container Apps resources and are not embedded in either image or supplied by the deployment workflow. Managed ASP.NET Core Data Protection is enabled for authentication-cookie continuity across API replicas and revisions.

### Consolidated networking

The API and generator share the new environment and subnet. MongoDB and Azure OpenAI use service-endpoint virtual-network rules restricted to that subnet; their public endpoint names remain in use, but clients outside the allowed subnet are denied. Blob narration keeps its existing private endpoint and disabled public network access. Speech retains the **same F0 account, voice, and managed identity authentication**. The Cognitive Services service endpoint also routes Speech, so that account must allow the app subnet and the generator must set `DvarTorahAudio__SpeechServiceUri=https://askarabbi-speech-prod.cognitiveservices.azure.com/`. Azure accepted this free subnet rule on F0; no Speech upgrade or private endpoint is part of the migration.

No NAT Gateway, VPN Gateway, Application Gateway, Azure Firewall, additional private endpoints, or additional private DNS zones are part of this design. Container Apps retains public HTTPS ingress for the API while its outbound dependency access uses the VNet. See [PRODUCTION_NETWORK.md](PRODUCTION_NETWORK.md) for the cutover, network checks, and scoped cleanup order. Older private-audio staging scripts and the historical runtime names in [DVAR_TORAH_AUDIO.md](DVAR_TORAH_AUDIO.md) are not the deployment target; do not run them to recreate retired environments.

## Automatic backend deployment

`.github/workflows/deploy.yml` is the sole backend production deployment workflow. It:

1. Waits for the `Verify` workflow to complete successfully for a direct push to `production`.
2. Checks out the exact verified commit SHA.
3. Authenticates to Azure through GitHub OpenID Connect; no Azure client secret is used.
4. Builds the API and weekly-job Dockerfiles for Linux AMD64 and pushes commit-SHA tags to their separate ACR repositories.
5. Updates `askarabbi-api-production` and `askarabbi-dvar-torah-production` to their immutable registry digests instead of mutable tags. It does not provision environments, copy secrets, perform DNS cutover, or retire resources.
6. Preserves and verifies Azure-managed .NET Data Protection and API scale-to-zero, waits for a healthy API revision, calls its public `/health` endpoint, and verifies the job's environment, narration setting, image, Schedule trigger, UTC cron expression, and execution policy.

Pull requests, failed verification runs, staging branches, and all branches other than `production` cannot enter this deployment job. Deployments are serialized so two production revisions are not updated concurrently. The frontend's separate production deployment remains unchanged.

### Connect GitHub to Azure with OIDC

Complete this one-time setup before the first workflow run:

1. In Microsoft Entra ID, create an app registration named `AskARabbi GitHub Production` and ensure its service principal exists.
2. Under **Certificates & secrets → Federated credentials**, add the **GitHub Actions deploying Azure resources** scenario with:
   - Organization: `Amitai5`
   - Repository: `AskARabbi`
   - Entity type: **Environment**
   - GitHub environment: `production`
   - Audience: `api://AzureADTokenExchange`
3. Do not create an Entra client secret. The federated credential is the trust boundary.
4. On ACR `askarabbiacrprod`, assign that service principal the `AcrPush` role at the registry scope. The registry currently uses legacy registry permissions; if it is later migrated to ABAC-enabled repository permissions, replace this with `Container Registry Repository Writer`.
5. Assign the same service principal `Container Apps Contributor` on Container App `askarabbi-api-production` and `Container Apps Jobs Contributor` on Container Apps Job `askarabbi-dvar-torah-production`, each at its individual resource scope. These are distinct built-in roles: the app role does not grant `Microsoft.App/jobs/read` or `write`. Existing assignments on retired resources do not grant access to replacements. The workflow checks access to both targets before updating either image.
6. In GitHub, open **Settings → Environments → production**. Restrict the environment to the `production` branch and add these environment secrets:
   - `AZURE_CLIENT_ID`: the app registration's Application (client) ID
   - `AZURE_TENANT_ID`: the Microsoft Entra Directory (tenant) ID
   - `AZURE_SUBSCRIPTION_ID`: the Azure subscription ID
7. Merge or push this workflow to `production`. After `Verify` passes, approve the `production` environment if it has required reviewers, then watch **Actions → Deploy Backend**.

The deployment identity needs no Cosmos DB, WorkOS, Key Vault, or subscription-wide role. Retain `AcrPush`, the separate resource-scoped app/job contributor roles above, and the existing `Reader` assignment on the consolidated environment. Runtime credentials are deliberately managed separately from deployment credentials.

## Backend configuration

Set these runtime environment variables on the API host:

| Variable | Required | Value |
| --- | --- | --- |
| `ASPNETCORE_ENVIRONMENT` | Yes | `Production` |
| `WorkOS__ApiKey` | Yes | Production WorkOS API key; secret |
| `WorkOS__ClientId` | Yes | Client ID from the same WorkOS production application |
| `MongoDB__ConnectionString` | Yes | Complete Azure Cosmos DB for MongoDB connection string; secret |
| `MongoDB__DatabaseName` | Optional | `askarabbi` is already the production default |
| `MongoDB__DvarTorahCollectionName` | Optional | `WeeklyAIDvarTorahs` is already the production default |
| `DvarTorah__InIsrael` | Optional | `false` selects the Diaspora weekly reading cycle |
| `Usage__MonthlyTokenLimit` | Optional | `5000000` input + output tokens per account per UTC calendar month; see [token accounting](TOKEN_USAGE.md). Update or remove any previous override when deploying a changed default. |
| `AI__ProjectEndpoint` | Yes | Azure OpenAI resource endpoint; non-secret |
| `AI__ModelName` | Yes | `askarabbi-gpt-5-mini`; non-secret deployment name |
| `AI__VectorStoreId` | Yes | Verified full-corpus managed vector-store ID; non-secret |
| `AI__CorpusFingerprint` | Yes | Lowercase SHA-256 printed by the corpus publisher; non-secret |
| `AI__TenantId` | Optional | Tenant used by `DefaultAzureCredential`; non-secret |
| `AI__MaximumOutputTokens` | Optional | `8000` is the production default and includes hidden reasoning plus structured answer tokens |
| `AI__ServiceTier` | Optional | `Priority` requests lower-latency priority processing for conversational file-search, answer, and validation calls; Azure can fall back to standard processing when capacity is unavailable |

The following non-secret values are already tracked in `appsettings.Production.json`. Set environment overrides only if the topology changes:

```text
WorkOS__RedirectUri=https://api.askarabbi.ai/api/user/callback
WorkOS__FrontendUri=https://askarabbi.ai/
Cors__AllowedOrigins__0=https://askarabbi.ai
AllowedHosts=api.askarabbi.ai
```

ASP.NET Core converts double underscores in environment-variable names into nested configuration separators. Never place `WorkOS__ApiKey` or `MongoDB__ConnectionString` in frontend configuration, a Vite variable, a build argument, source control, or logs.

The Container App uses its system-assigned managed identity for Azure OpenAI Responses generation and forced vector-store `file_search`. Grant that identity **Cognitive Services OpenAI User** at only the `AARProduction-OpenAI` resource scope. No Azure OpenAI API key is required by production. Corpus publishing also needs **Cognitive Services OpenAI Contributor**, but that role does not bypass the network restriction: run publishing from the allowed subnet, not an ordinary public workstation. Publish and rotate the corpus with the reviewed commands in [MANAGED_VECTOR_STORE.md](MANAGED_VECTOR_STORE.md); update `AI__VectorStoreId` and `AI__CorpusFingerprint` together.

### Azure Container Apps configuration

In the Azure portal, open `askarabbi-api-production` and use **Settings → Secrets** for secret values. Reference those secrets from **Containers → Environment variables** instead of entering the secret values directly as plain environment variables. The deployment workflow updates only the image and preserves this runtime configuration.

When cloning or replacing the API configuration, use a management API version that preserves `properties.configuration.runtime.dotnet.autoConfigureDataProtection` (`2025-02-02-preview` is the version verified by this workflow). Require it to be `true` before and after deploying. Keys from a different Container App are not assumed to migrate with its image; include fresh sign-in and cross-revision session checks in the cutover verification.

The live deployment temporarily allows both `api.askarabbi.ai` and the Azure provider hostname in `AllowedHosts` so the provider URL can be smoke-tested before DNS is connected. Do not replace the allow-list with `*`. Remove the provider hostname after the custom domain is stable unless Azure health operations still require it.

For stronger secret handling, the Container App can reference Azure Key Vault secrets through its managed identity. Grant it access only to the individual secrets it needs. The application still receives the resolved values under the same environment-variable names, so no code change is required.

## WorkOS production setup

Use the WorkOS **Production** environment, not the staging credentials. In **Applications → AskRabbi**:

1. Copy its production API key once and store it as `WorkOS__ApiKey` on the API host.
2. Copy the matching client ID into `WorkOS__ClientId`.
3. Add the exact redirect URI `https://api.askarabbi.ai/api/user/callback`.
4. Set the sign-in URL to `https://askarabbi.ai/`.
5. Set the default application/homepage and allowed sign-out URI to `https://askarabbi.ai/`.
6. Set the password-reset URL to `https://askarabbi.ai/reset-password` so the generated link arrives as `/reset-password?token=...`.
7. Enable Email + Password and Google OAuth. Complete any Google provider credentials requested by WorkOS for production.
8. Under **Sessions**, set **Maximum session length** to **30 days** and **Inactivity timeout** to **7 days**. Keep the access-token duration short; do not increase it to 30 days. Verify these settings in the WorkOS **Production** application before activating longer browser sign-in. The app cannot extend an expired or revoked WorkOS session.

### Persistent browser sign-in

The API defaults to `Session:MaximumLifetimeDays=30` and `Session:InactivityTimeoutDays=7`, also declared in `Backend/AskARabbi.Api/appsettings.Production.json`. Azure overrides use `Session__MaximumLifetimeDays` and `Session__InactivityTimeoutDays`.

- `AskRabbi.Session` is an encrypted, `HttpOnly`, `Secure`, `SameSite=Strict` persistent cookie with a maximum lifetime of 30 days from sign-in. WorkOS token refresh never restarts that deadline.
- `AskRabbi.SessionActivity` is a separate encrypted, session-bound cookie, refreshed on authenticated API requests and expiring after seven days without activity (or at the 30-day deadline, whichever is earlier). Keeping activity separate prevents slow responses from rewriting credentials that another request has already refreshed. No tokens are placed in local storage, and no extra database writes or provider calls are needed to record activity.
- The API enforces both cutoffs even when an expired cookie is manually replayed. Logout, account deletion, and rejected WorkOS refreshes clear both cookies. Background/offline browser activity without an API request does not extend sign-in.
- Both cookies use the Azure-managed Data Protection keys described above, so restarting or scaling the same Container App to zero does not reset their deadlines or require keeping a replica running.
- Rollout requires one fresh sign-in for existing sessions: older cookies do not contain a trustworthy original sign-in timestamp or the session-bound activity cookie. Clearing browser data, private browsing, or a stricter WorkOS session policy can still end sign-in earlier.

WorkOS production redirect URIs must use HTTPS and must match the URI sent by the API exactly. Keep staging and production API keys/client IDs separate.

## Azure Cosmos DB for MongoDB setup

1. Create or select the Azure Cosmos DB for MongoDB account and database.
2. In the Azure portal, open **Connection strings** or **Quick start** and copy the complete MongoDB connection string.
3. Store that full value as `MongoDB__ConnectionString`; it includes the host, port, account credential, TLS parameters, and any required `appName` value.
4. Keep `MongoDB__DatabaseName=askarabbi`, or override it consistently before the first production write.
5. Enable `Microsoft.AzureCosmosDB` on `container-apps-production`, then enable the Cosmos virtual-network filter with only that subnet allowed and no public IP exceptions. Keep `publicNetworkAccess=Enabled` for the service-endpoint path; disabling it would also block this approved design. The frontend must never have direct Cosmos access.
6. Start the API once and verify that its startup index initializer can access the account and create the required indexes.

The current collections are `users`, `conversations`, `conversationMessages`, `conversationSettings`, `usage`, and `WeeklyAIDvarTorahs`. Collection names are case-sensitive. Changing a collection name after data exists requires a migration plan. The API startup initializer creates the current-or-latest weekly-publication index; the collection itself is created on first write if it does not already exist.

## Weekly Dvar Torah Container Apps Job deployment

The weekly write path is intentionally isolated from `askarabbi-api-production` in [`Backend/AskARabbi.DvarTorahJob`](../Backend/AskARabbi.DvarTorahJob). The project publishes a one-shot .NET 10 Docker image with no ingress or internal timer. `askarabbi-dvar-torah-production` runs one replica on the five-field cron `5 8 * * 0`, which Container Apps evaluates in UTC. Its Sunday 08:05 UTC start occurs after Shabbat across the continental United States; application code then selects the upcoming Shabbat through the shared Hebrew-calendar service. MongoDB enforces a deterministic reading-cycle/week key and recoverable generation lease, so platform retries cannot publish a week twice.

The schedule can be provisioned now without authorizing content generation. `DvarTorah__GenerationEnabled` defaults to `false`; while false, every scheduled execution logs a safe disabled event and exits successfully before reading `MongoDB__ConnectionString` or constructing a database client. This is an application gate, not a disabled cron trigger, so the infrastructure and execution path remain observable before the content phase.

### One-time consolidation and job provisioning

Use the approved migration runbook in [PRODUCTION_NETWORK.md](PRODUCTION_NETWORK.md), not the retired private-audio bootstrap scripts. Create the new job in `askarabbi-containerapps-production` with a Manual trigger while its networking and identities are tested. Copy the current private-audio job's immutable production image and runtime configuration, including narration, voice, Blob location, generation settings, and secret references. This infrastructure migration must not rebuild unrelated working-tree changes or regenerate published articles.

Grant the new job identity `AcrPull` on the existing registry, `Cognitive Services OpenAI User` on the existing OpenAI account, `Cognitive Services Speech User` on the unchanged Speech resource, and `Storage Blob Data Contributor` only on the existing audio container. Grant the GitHub OIDC deployment identity resource-scoped `Container Apps Jobs Contributor` on the new job.

The job's runtime secret store is separate from the API's. Transfer existing secret values securely in memory or preserve their Key Vault references; never emit them into a deployment summary, saved template, shell history, or logs. Preserve `MongoDB__ConnectionString` as a runtime secret reference. Subsequent deployment workflow runs update only the image and preserve the job's schedule, environment variables, identity, and secrets.

Before activating the weekly timer:

1. Verify all required dependency operations from the new subnet, including MongoDB access, OpenAI Responses/file search, Speech synthesis, and private Blob read/write.
2. Verify the API can still return the existing weekly article and its audio; do not delete or regenerate publications to test infrastructure.
3. Check for active job executions. Disable every old scheduled trigger before enabling the new Schedule trigger; never leave two timer owners.
4. Set the new timer to `5 8 * * 0`, timeout `3600`, retry limit `2`, parallelism `1`, and completion count `1`. Preserve the current production generation gate; do not reset it to the bootstrap default.
5. Update failure alerts and Log Analytics filters to the new job name, verify the workflow's target permissions, and retire the old runtime promptly after successful cutover.

Container Apps keeps the most recent execution history and environment logs. Alert on failed or timed-out executions and on the absence of an expected weekly completion event; application logs contain status/week identifiers but no generated body or secret.

The 60-minute replica timeout allows time for article generation and narration and exceeds the current 30-minute MongoDB generation lease. If a container is forcibly terminated at the platform timeout, its lease has already expired and a platform retry can recover it instead of treating the abandoned attempt as still active. Keep the platform timeout greater than `DvarTorah__GenerationLeaseMinutes` whenever either value changes.

## Frontend build and hosting

Run from `Frontend`:

```powershell
pnpm install --frozen-lockfile
pnpm verify
pnpm build
```

Deploy the generated `Frontend/dist` directory. A standard production build automatically targets `https://api.askarabbi.ai`. The optional build variable below is useful for an intentional alternate environment, but it is not a secret:

```text
VITE_API_BASE_URL=https://api.askarabbi.ai
```

Vite embeds every `VITE_*` value into browser-readable JavaScript. Only public URLs and other non-sensitive build configuration may use that prefix.

Local `.env` variants are ignored by Git. The production API URL is compiled into the normal production default, so no frontend environment file is required for this topology.

Configure the static host to rewrite unknown application routes to `/index.html` while still serving real assets normally. Without this fallback, opening a WorkOS password-reset link directly at `/reset-password?token=...` will return a hosting 404 before React can process it.

## Backend image publish

Normal production releases are automatic. A push or merge to `production` starts `Verify`; only a successful push verification can start `Deploy Backend`. The workflow builds both images from the root Docker context so they can reference `AskARabbiLIB`. The API image includes the trusted document manifest, shared conversational prompts, and [canonical-source archive](../Backend/AskARabbi.Api/Data/README.md); its Docker build generates the read-only canonical-search index. The full raw/normalized corpus, developer segment index, frontend, credentials, and developer build output are excluded.

Conversational writer, repair, auditor, strict schemas, and the matching library must deploy together. The [claim-kind and recovery change](ANSWER_RELIABILITY.md) needs no new secret, configuration key, dependency, database migration, or frontend response contract. Keep existing corpus settings and assets: uncited background is not an outage bypass. Weekly publication keeps its separate validation rules.

Each image is tagged with the verified Git commit SHA, and the API and job are updated to their respective digests. Re-running a deployment does not depend on a mutable `latest` tag. Runtime secrets and environment variables are not passed as Docker build arguments.

For a manual diagnostic build without pushing or deploying, run from the repository root while Docker is available:

```powershell
docker build --file Backend/AskARabbi.Api/Dockerfile --tag askarabbi-api:local .
docker build --file Backend/AskARabbi.DvarTorahJob/Dockerfile --tag askarabbi-dvar-torah-job:local .
```

The production configuration requires HTTPS for WorkOS URLs, always marks authentication cookies `Secure`, enables HSTS, and accepts credentialed browser requests only from `https://askarabbi.ai`.

## Production smoke checks

Perform these checks after DNS and TLS are active:

1. `GET https://api.askarabbi.ai/health` returns HTTP `200`.
2. `https://askarabbi.ai` loads without mixed-content or CORS errors and sends API requests only to `https://api.askarabbi.ai`.
3. Email sign-in, Google sign-in, sign-up, session refresh, and logout complete through the WorkOS production environment.
4. A password-reset email opens `https://askarabbi.ai/reset-password?token=...`, accepts a new password, and requires signing in again.
5. Saving Personalization creates or updates the user's `conversationSettings` record in Cosmos.
6. Saving Settings persists both conversation defaults without erasing Personalization.
7. Creating, renaming, loading, and deleting a conversation affects only the authenticated user's records.
8. Requests with an unapproved `Origin` do not receive a CORS allow-origin response.
9. Restart the API and verify that existing application sessions behave according to the deployed Data Protection key storage.
10. Verify the existing weekly article and authenticated audio stream from the custom API origin, then check the new runtime's dependency logs. Complete the network-denial checks in [PRODUCTION_NETWORK.md](PRODUCTION_NETWORK.md); a public `/health` response alone does not prove the database or OpenAI firewall is correct.

### Conversation reliability checks

Use an authenticated test account against the deployed revision:

- Ask “Who was Rabbi Akiva?” and verify a useful introductory answer can appear without Torah citations; inspect diagnostics to confirm independent review.
- Ask the reported vampire identity and alive/dead questions. Fiction should remain clearly fictional; any real religious ruling must be supported rather than attached to unrelated passages.
- Ask a known source-backed question and inspect exact quotations, edition, provenance, and enabled-source filtering.
- In a controlled test environment, force `ValidationFailed` and `InsufficientEvidence`. Verify `answered` with one localized recovery message, `sources: []`, persistence after reload, idempotent replay of the same message ID, and no title from rejected content.
- Confirm the original validation outcome and actual provider tokens remain in diagnostics; the fixed reply should add no model request.
- Use controlled dependency/usage failures to confirm provider, retrieval, cancellation, and quota errors are not presented as successful recovery. Do not disrupt production dependencies to simulate failures.

Local implementation tests and live prototype probes are documented in [answer reliability](ANSWER_RELIABILITY.md); they do not replace these deployed API checks.

## Remaining launch controls

Azure-managed ASP.NET Core Data Protection is already enabled and verified across Container App revisions. Before broad public access, replace or complete the planned shared server-side session/revocation design and finish rate limits, CSRF review, WorkOS webhook validation, dependency readiness checks, backups and retention, account deletion, telemetry redaction, and live-provider smoke automation.

The backend combines managed semantic retrieval, local canonical-source access, reviewed claim types, and API-owned recovery. Keep the managed corpus ID and fingerprint bound together and validate authenticated chat behavior for the deployed revision. Track the remaining launch controls in [PRODUCTION_READINESS.md](PRODUCTION_READINESS.md).
