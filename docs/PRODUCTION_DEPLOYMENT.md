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

| Resource | Current value |
| --- | --- |
| Resource group | `AARProduction` |
| Container registry | `askarabbiacrprod.azurecr.io` |
| Registry SKU | Basic; admin credentials disabled |
| Container App | `askarabbi-api` |
| Provider hostname | `askarabbi-api.nicebeach-dd0ab493.centralus.azurecontainerapps.io` |
| Runtime port | `8080` |
| Image repository | `askarabbi-api` |
| Cosmos MongoDB database | `askarabbi` |

The Container App uses its system-assigned managed identity to pull from ACR. Runtime secrets remain in Container Apps and are not embedded in the image or supplied by the deployment workflow. Managed ASP.NET Core Data Protection is enabled for authentication-cookie continuity across replicas and revisions.

## Automatic backend deployment

`.github/workflows/deploy.yml` is the sole backend production deployment workflow. It:

1. Waits for the `Verify` workflow to complete successfully for a direct push to `production`.
2. Checks out the exact verified commit SHA.
3. Authenticates to Azure through GitHub OpenID Connect; no Azure client secret is used.
4. Builds `Backend/AskARabbi.Api/Dockerfile` for Linux AMD64 and pushes a commit-SHA tag to ACR.
5. Updates `askarabbi-api` to the immutable registry digest instead of a mutable tag.
6. Waits for the resulting revision to become healthy and calls its public `/health` endpoint.

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
5. On Container App `askarabbi-api`, assign the same service principal `Container Apps Contributor` at the individual Container App scope.
6. In GitHub, open **Settings → Environments → production**. Restrict the environment to the `production` branch and add these environment secrets:
   - `AZURE_CLIENT_ID`: the app registration's Application (client) ID
   - `AZURE_TENANT_ID`: the Microsoft Entra Directory (tenant) ID
   - `AZURE_SUBSCRIPTION_ID`: the Azure subscription ID
7. Merge or push this workflow to `production`. After `Verify` passes, approve the `production` environment if it has required reviewers, then watch **Actions → Deploy Backend**.

The deployment identity needs no Cosmos DB, WorkOS, Key Vault, or subscription-wide role. `AcrPush` and the Container App-scoped contributor assignment are sufficient for this workflow. Runtime credentials are deliberately managed separately from deployment credentials.

## Backend configuration

Set these runtime environment variables on the API host:

| Variable | Required | Value |
| --- | --- | --- |
| `ASPNETCORE_ENVIRONMENT` | Yes | `Production` |
| `WorkOS__ApiKey` | Yes | Production WorkOS API key; secret |
| `WorkOS__ClientId` | Yes | Client ID from the same WorkOS production application |
| `MongoDB__ConnectionString` | Yes | Complete Azure Cosmos DB for MongoDB connection string; secret |
| `MongoDB__DatabaseName` | Optional | `askarabbi` is already the production default |
| `Usage__MonthlyAnswerLimit` | Optional | `50` is the current default |

The following non-secret values are already tracked in `appsettings.Production.json`. Set environment overrides only if the topology changes:

```text
WorkOS__RedirectUri=https://api.askarabbi.ai/api/user/callback
WorkOS__FrontendUri=https://askarabbi.ai/
Cors__AllowedOrigins__0=https://askarabbi.ai
AllowedHosts=api.askarabbi.ai
```

ASP.NET Core converts double underscores in environment-variable names into nested configuration separators. Never place `WorkOS__ApiKey` or `MongoDB__ConnectionString` in frontend configuration, a Vite variable, a build argument, source control, or logs.

### Azure Container Apps configuration

In the Azure portal, open `askarabbi-api` and use **Settings → Secrets** for secret values. Reference those secrets from **Containers → Environment variables** instead of entering the secret values directly as plain environment variables. The deployment workflow updates only the image and preserves this runtime configuration.

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

The current collections are `users`, `conversations`, `conversationMessages`, `conversationSettings`, and `usage`. Changing a collection name after data exists requires a migration plan.

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

## API publish

Normal production releases are automatic. A push or merge to `production` starts `Verify`; only a successful push verification can start `Deploy Backend`. The workflow builds from the root Docker context so the API can reference `AskARabbiLIB`, while `.dockerignore` prevents the raw corpus, normalized corpus, frontend, local configuration, development settings, and build output from entering the image context.

Each image is tagged with the verified Git commit SHA and the Container App is updated to the resulting digest. Re-running a deployment does not depend on a mutable `latest` tag. Runtime secrets and environment variables are not passed as Docker build arguments.

For a manual diagnostic build without pushing or deploying, run from the repository root while Docker is available:

```powershell
docker build --file Backend/AskARabbi.Api/Dockerfile --tag askarabbi-api:local .
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

The backend still stores user turns without invoking the grounded answer pipeline. Production chat also needs a model deployment and a production retriever because the container image deliberately excludes local corpus data. Track all remaining launch work in [PRODUCTION_READINESS.md](PRODUCTION_READINESS.md).
