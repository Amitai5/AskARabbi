# Production readiness checklist

This checklist reflects the deployed Azure resources and the current repository behavior as of August 27, 2026.

## Completed foundation

- [x] Azure resource group `AARProduction` exists.
- [x] Azure Container Registry `askarabbiacrprod.azurecr.io` runs on the Basic SKU with its admin account disabled.
- [x] Azure Container App `askarabbi-api` runs the .NET 10 API on port 8080.
- [x] The Container App pulls from ACR through its system-assigned managed identity and an ACR-scoped `AcrPull` assignment.
- [x] Azure Cosmos DB for MongoDB contains the application database and collections.
- [x] The MongoDB connection string is stored as a Container App secret and application indexes initialize successfully.
- [x] ASP.NET Core Data Protection is shared by the Container Apps platform so authentication cookies can survive scaling and revisions.
- [x] The provider health endpoint returns `Healthy`.
- [x] Backend, library, prototype, and frontend verification run on every branch.
- [x] A production-only backend deployment workflow builds the API image, pushes it to ACR, deploys it by digest, and checks the new revision and `/health` endpoint.

## Blockers before a production user can sign in

- [ ] Commit and push the Dockerfile, `.dockerignore`, deployment workflow, backend implementation, and related documentation.
- [ ] Connect the GitHub `production` environment to Azure through an OIDC federated credential.
- [ ] Grant that deployment identity `AcrPush` on `askarabbiacrprod` and `Container Apps Contributor` on `askarabbi-api`.
- [ ] Add `AZURE_CLIENT_ID`, `AZURE_TENANT_ID`, and `AZURE_SUBSCRIPTION_ID` to the GitHub `production` environment.
- [ ] Run the first production workflow and confirm that it deploys the verified commit successfully.
- [ ] Bind `api.askarabbi.ai` to the Container App, create the required Cloudflare DNS records, and validate HTTPS.
- [ ] Configure the WorkOS production application with its API key, client ID, Google login, email/password login, callback URL, sign-out URL, and password-reset URL.
- [ ] Add `WorkOS__ApiKey` and `WorkOS__ClientId` as Container App secrets and secret-backed environment variables.
- [ ] Validate the existing frontend production deployment at `https://askarabbi.ai`, including the SPA fallback for `/reset-password`.

## Blockers before AskARabbi can answer questions

- [ ] Connect `IGroundedAnswerService` and the AI engine from `AskARabbiLIB` to the backend conversation-message endpoint.
- [ ] Provision or select the production Azure OpenAI/Foundry model deployment and configure the backend authentication boundary for it.
- [ ] Select and deploy the production source retriever. The current backend image intentionally excludes `Data`, so it cannot use the local SQLite index without an explicit index delivery or mount design. Azure AI Search remains the recommended production destination.
- [ ] Retrieve source evidence using the conversation's selected collections and the user's language preferences.
- [ ] Run deterministic citation and quotation validation before persisting or returning an assistant message.
- [ ] Persist the validated assistant response and its evidence snapshot idempotently.
- [ ] Enforce and increment monthly usage only after a validated answer is produced.
- [ ] Define cancellation, timeout, retry, and safe failure behavior across retrieval, model generation, validation, and persistence.
- [ ] Add backend tests for the complete user-message-to-grounded-answer workflow without making live network calls.

## Production smoke testing

- [ ] Confirm `GET https://api.askarabbi.ai/health` returns HTTP 200.
- [ ] Test email sign-in, Google sign-in, sign-up, session refresh, logout, and password reset through the WorkOS production environment.
- [ ] Verify credentialed CORS succeeds only from `https://askarabbi.ai`.
- [ ] Create, rename, load, and delete conversations and verify ownership isolation in Cosmos DB.
- [ ] Save personalization and conversation settings and verify one update cannot erase the other.
- [ ] Ask representative questions and verify every displayed citation resolves to the exact approved source text.
- [ ] Restart and scale the Container App and confirm existing authentication sessions remain valid.
- [ ] Confirm failed retrieval or failed citation validation never falls back to unsupported model knowledge.

## Security and operational hardening

- [ ] Add a readiness endpoint that checks required dependencies without exposing secrets; keep `/health` as process liveness.
- [ ] Add per-user and per-IP rate limits for authentication, password recovery, and AI generation endpoints.
- [ ] Complete the CSRF review for every cookie-authenticated state-changing endpoint.
- [ ] Validate signed WorkOS webhooks and use them for account/session lifecycle events where needed.
- [ ] Replace or complete the shared server-side session and revocation design before broad public access.
- [ ] Add structured telemetry, latency and error metrics, alerts, and explicit personal-data and secret redaction.
- [ ] Configure Cosmos DB backup, retention, deletion, and account-erasure procedures.
- [ ] Restrict Cosmos DB network access after the final Container Apps networking design is selected.
- [ ] Configure Azure cost budgets and alerts for Container Apps, ACR, Cosmos DB, retrieval, and model usage.
- [ ] Define an ACR image-retention policy and a tested rollback procedure.
- [ ] Add automated dependency and container-image vulnerability scanning.
- [ ] Decide whether scale-to-zero is acceptable for production chat latency or whether `minReplicas` should become 1.

## Deferred product work

- [ ] Billing and upgrades above the initial monthly usage allowance.
- [ ] Saved/private conversation modes, retention controls, and user-facing data export.
- [ ] Administrative support tooling and abuse review.
- [ ] Full integration evaluations against a curated set of Jewish-learning questions and expected evidence.
