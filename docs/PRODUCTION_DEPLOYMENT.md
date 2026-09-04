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

## Current Azure deployment

The backend now uses Azure Container Apps rather than App Service:

| Resource | Current or required value |
| --- | --- |
| Resource group | `AARProduction` |
| Container registry | `askarabbiacrprod.azurecr.io` |
| Registry SKU | Basic; admin credentials disabled |
| Container App | `askarabbi-api-vnet` |
| Provider hostname | `askarabbi-api-vnet.nicestone-7ffaddef.centralus.azurecontainerapps.io` |
| Runtime port | `8080` |
| API image repository | `askarabbi-api` |
| Scheduled Container Apps Job | `askarabbi-weekly-dvar-torah` |
| Job image repository | `askarabbi-dvar-torah-job` |
| Job schedule | `5 8 * * 0` (Sunday 08:05 UTC) |
| Cosmos MongoDB database | `askarabbi` |

The API uses its system-assigned managed identity to pull from ACR; the scheduled job must be provisioned with its own system identity and `AcrPull` assignment. Runtime secrets remain on the individual Container Apps resources and are not embedded in either image or supplied by the deployment workflow. Managed ASP.NET Core Data Protection is enabled for authentication-cookie continuity across API replicas and revisions.

### Private narration migration

Private Blob narration uses `askarabbi-production-private-env`, API `askarabbi-api-vnet`, and generator `askarabbi-weekly-dvar-torah-vnet`. The API's DNS cutover is approved; the original API remains available for rollback. The weekly schedule remains on the original job until its separate approval: the replacement is Manual and has successfully generated the existing Nitzavim recording. Follow [DVAR_TORAH_AUDIO.md](DVAR_TORAH_AUDIO.md) for scoped roles, backfill, DNS/TLS checks, and ordered timer migration. Storage public network access must remain disabled. After the schedule cutover, update the workflow job name to the replacement and its job timeout to 3600 seconds.

## Automatic backend deployment

`.github/workflows/deploy.yml` is the sole backend production deployment workflow. It:

1. Waits for the `Verify` workflow to complete successfully for a direct push to `production`.
2. Checks out the exact verified commit SHA.
3. Authenticates to Azure through GitHub OpenID Connect; no Azure client secret is used.
4. Builds the API and weekly-job Dockerfiles for Linux AMD64 and pushes commit-SHA tags to their separate ACR repositories.
5. Updates `askarabbi-api-vnet` and the current timer owner `askarabbi-weekly-dvar-torah` to their immutable registry digests instead of mutable tags.
6. Waits for the API revision to become healthy, calls its public `/health` endpoint, and verifies the job image, Schedule trigger, UTC cron expression, and provisioning state.

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
5. On Container App `askarabbi-api-vnet` and the selected Container Apps Job, assign the same service principal `Container Apps Contributor` at each individual resource scope. The replacement job also has this scoped assignment for its later cutover.
6. In GitHub, open **Settings → Environments → production**. Restrict the environment to the `production` branch and add these environment secrets:
   - `AZURE_CLIENT_ID`: the app registration's Application (client) ID
   - `AZURE_TENANT_ID`: the Microsoft Entra Directory (tenant) ID
   - `AZURE_SUBSCRIPTION_ID`: the Azure subscription ID
7. Merge or push this workflow to `production`. After `Verify` passes, approve the `production` environment if it has required reviewers, then watch **Actions → Deploy Backend**.

The deployment identity needs no Cosmos DB, WorkOS, Key Vault, or subscription-wide role. `AcrPush` and the two resource-scoped Container Apps contributor assignments are sufficient for this workflow. Runtime credentials are deliberately managed separately from deployment credentials.

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
| `Usage__MonthlyAnswerLimit` | Optional | `50` is the current default |
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

The Container App uses its system-assigned managed identity for Azure OpenAI Responses generation and forced vector-store `file_search`. Grant that identity **Cognitive Services OpenAI User** at only the `AARProduction-OpenAI` resource scope. No Azure OpenAI API key is required by production. A workstation publishing files/stores needs the resource-scoped **Cognitive Services OpenAI Contributor** role. Publish and rotate the corpus with the reviewed commands in [MANAGED_VECTOR_STORE.md](MANAGED_VECTOR_STORE.md); update `AI__VectorStoreId` and `AI__CorpusFingerprint` together.

### Azure Container Apps configuration

In the Azure portal, open `askarabbi-api-vnet` and use **Settings → Secrets** for secret values. Reference those secrets from **Containers → Environment variables** instead of entering the secret values directly as plain environment variables. The deployment workflow updates only the image and preserves this runtime configuration.

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
8. Review the session lifetime, access-token duration, and inactivity timeout before launch.

WorkOS production redirect URIs must use HTTPS and must match the URI sent by the API exactly. Keep staging and production API keys/client IDs separate.

## Azure Cosmos DB for MongoDB setup

1. Create or select the Azure Cosmos DB for MongoDB account and database.
2. In the Azure portal, open **Connection strings** or **Quick start** and copy the complete MongoDB connection string.
3. Store that full value as `MongoDB__ConnectionString`; it includes the host, port, account credential, TLS parameters, and any required `appName` value.
4. Keep `MongoDB__DatabaseName=askarabbi`, or override it consistently before the first production write.
5. Permit network access only from the backend's outbound network or use a private endpoint/VNet integration. The frontend must never have direct Cosmos access.
6. Start the API once and verify that its startup index initializer can access the account and create the required indexes.

The current collections are `users`, `conversations`, `conversationMessages`, `conversationSettings`, `usage`, and `WeeklyAIDvarTorahs`. Collection names are case-sensitive. Changing a collection name after data exists requires a migration plan. The API startup initializer creates the current-or-latest weekly-publication index; the collection itself is created on first write if it does not already exist.

## Weekly Dvar Torah Container Apps Job deployment

The weekly write path is intentionally isolated from `askarabbi-api` in [`Backend/AskARabbi.DvarTorahJob`](../Backend/AskARabbi.DvarTorahJob). The project publishes a one-shot .NET 10 Docker image with no ingress or internal timer. `askarabbi-weekly-dvar-torah` runs one replica on the five-field cron `5 8 * * 0`, which Container Apps evaluates in UTC. Its Sunday 08:05 UTC start occurs after Shabbat across the continental United States; application code then selects the upcoming Shabbat through the shared Hebrew-calendar service. MongoDB enforces a deterministic reading-cycle/week key and recoverable generation lease, so platform retries cannot publish a week twice.

The schedule can be provisioned now without authorizing content generation. `DvarTorah__GenerationEnabled` defaults to `false`; while false, every scheduled execution logs a safe disabled event and exits successfully before reading `MongoDB__ConnectionString` or constructing a database client. This is an application gate, not a disabled cron trigger, so the infrastructure and execution path remain observable before the content phase.

### One-time job provisioning

Provision the job in the same Container Apps environment as `askarabbi-api`. The first image below is Microsoft's public quickstart job, used only to establish the resource and its system identity before the production workflow applies the private AskRabbi image:

```bash
az containerapp job create \
  --name askarabbi-weekly-dvar-torah \
  --resource-group AARProduction \
  --environment <CONTAINER_APPS_ENVIRONMENT> \
  --trigger-type Schedule \
  --cron-expression "5 8 * * 0" \
  --replica-timeout 2100 \
  --replica-retry-limit 2 \
  --parallelism 1 \
  --replica-completion-count 1 \
  --cpu 0.5 \
  --memory 1.0Gi \
  --mi-system-assigned \
  --image mcr.microsoft.com/k8se/quickstart-jobs:latest \
  --env-vars DvarTorah__GenerationEnabled=false MongoDB__DatabaseName=askarabbi MongoDB__DvarTorahCollectionName=WeeklyAIDvarTorahs DvarTorah__InIsrael=false DvarTorah__GenerationLeaseMinutes=30
```

Assign the new job identity `AcrPull` at `askarabbiacrprod` scope and configure the job's ACR registry entry to use that system identity:

```bash
JOB_PRINCIPAL_ID="$(az containerapp job identity show --name askarabbi-weekly-dvar-torah --resource-group AARProduction --query principalId --output tsv)"
ACR_RESOURCE_ID="$(az acr show --name askarabbiacrprod --resource-group AARProduction --query id --output tsv)"

az role assignment create \
  --assignee-object-id "$JOB_PRINCIPAL_ID" \
  --assignee-principal-type ServicePrincipal \
  --role AcrPull \
  --scope "$ACR_RESOURCE_ID"

az containerapp job registry set \
  --name askarabbi-weekly-dvar-torah \
  --resource-group AARProduction \
  --server askarabbiacrprod.azurecr.io \
  --identity system
```

Grant the GitHub OIDC deployment identity resource-scoped `Container Apps Contributor` on the job. The next successful production workflow replaces the bootstrap image by immutable digest and verifies the configured image and schedule. Do not run the job with generation enabled merely to validate deployment.

The job's runtime secret store is separate from the API Container App's secret store. Before enabling generation, add the full Cosmos connection string to the job as `mongodb-connection-string`—prefer an Azure Key Vault reference through the job's managed identity—and expose it only as `MongoDB__ConnectionString=secretref:mongodb-connection-string`. The deployment workflow updates only the image and preserves the job's schedule, environment variables, identity, and secrets.

Before enabling generation:

1. Replace `UnconfiguredWeeklyDvarTorahGenerator` with the approved source, prompt, validation, and model implementation.
2. Run the library coordinator tests, backend/job tests, both Docker builds, and a manual Container Apps Job execution against a non-production collection.
3. Verify a retry returns `AlreadyPublished`, an active lease returns `GenerationInProgress`, and an expired lease can be recovered safely.
4. Confirm the API returns the new article from `GET /api/dvar-torah` and the frontend displays it only after opening the weekly-learning destination.
5. Configure Container Apps execution-failure alerts and Log Analytics queries, then set `DvarTorah__GenerationEnabled=true` on the job.

Container Apps keeps the most recent execution history and environment logs. Alert on failed or timed-out executions and on the absence of an expected weekly completion event; application logs contain status/week identifiers but no generated body or secret.

The 35-minute replica timeout intentionally exceeds the 30-minute MongoDB lease. If a container is forcibly terminated at the platform timeout, its lease has already expired and a platform retry can recover it instead of treating the abandoned attempt as still active. Keep the platform timeout greater than `DvarTorah__GenerationLeaseMinutes` whenever either value changes.

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

Normal production releases are automatic. A push or merge to `production` starts `Verify`; only a successful push verification can start `Deploy Backend`. The workflow builds both images from the root Docker context so they can reference `AskARabbiLIB`; only the API image includes the 2.6 MB trusted document manifest. `.dockerignore` prevents the raw corpus, normalized Markdown, SQLite index, frontend, local configuration, development settings, and build output from entering either image context.

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

## Remaining launch controls

Azure-managed ASP.NET Core Data Protection is already enabled and verified across Container App revisions. Before broad public access, replace or complete the planned shared server-side session/revocation design and finish rate limits, CSRF review, WorkOS webhook validation, dependency readiness checks, backups and retention, account deletion, telemetry redaction, and live-provider smoke automation.

The backend invokes the grounded answer pipeline and deliberately excludes local corpus data. The full managed corpus is published and its ID/fingerprint are bound together; production chat becomes usable after this integration is deployed, WorkOS is configured, and authenticated source/citation smoke tests pass. Track the remaining launch controls in [PRODUCTION_READINESS.md](PRODUCTION_READINESS.md).
