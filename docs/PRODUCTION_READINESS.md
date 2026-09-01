# Production readiness checklist

This checklist reflects the deployed Azure resources and the current repository behavior as of September 1, 2026.

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
- [x] A production-only backend deployment workflow builds the API and weekly-job images, pushes both to ACR, deploys both by digest, and verifies the API revision/health plus the scheduled-job image and cron configuration.

## Blockers before a production user can sign in

- [ ] Commit and push the Dockerfile, `.dockerignore`, deployment workflow, backend implementation, and related documentation.
- [ ] Connect the GitHub `production` environment to Azure through an OIDC federated credential.
- [x] Grant the deployment identity `AcrPush` on `askarabbiacrprod`, resource-scoped `Container Apps Contributor` on `askarabbi-api`, and resource-scoped `Container Apps Jobs Contributor` on `askarabbi-weekly-dvar-torah`.
- [ ] Add `AZURE_CLIENT_ID`, `AZURE_TENANT_ID`, and `AZURE_SUBSCRIPTION_ID` to the GitHub `production` environment.
- [ ] Run the first production workflow and confirm that it deploys the verified commit successfully.
- [ ] Bind `api.askarabbi.ai` to the Container App, create the required Cloudflare DNS records, and validate HTTPS.
- [ ] Configure the WorkOS production application with its API key, client ID, Google login, email/password login, callback URL, sign-out URL, and password-reset URL.
- [ ] Add `WorkOS__ApiKey` and `WorkOS__ClientId` as Container App secrets and secret-backed environment variables.
- [ ] Validate the existing frontend production deployment at `https://askarabbi.ai`, including the SPA fallback for `/reset-password`.

## Blockers before AskARabbi can answer questions

- [x] Connect `IGroundedAnswerService`, `AzureOpenAIEngine`, and `GroundedAnswerTextRenderer` to the backend conversation-message endpoint.
- [x] Deploy `askarabbi-gpt-5-mini` and configure Entra authentication through the Container App's managed identity.
- [x] Implement forced Azure OpenAI Responses file-search retrieval behind `ISourceRetriever`, including immutable corpus verification, manifest-backed provenance, local source/language filters, stable IDs, and explicit excerpts.
- [x] Grant the publishing operator resource-scoped **Cognitive Services OpenAI User** and **Cognitive Services OpenAI Contributor** roles.
- [x] Publish a three-document pilot and validate forced file-search retrieval, empty-attribute manifest provenance, exact text reconstruction, and schema-v2 logical/provider counts.
- [x] Publish and verify the full 1,441-document corpus and bind its returned store ID/fingerprint to the Container App as `AskARabbi Production Sefaria Corpus`.
- [x] Retrieve source evidence using each conversation's selected sources; treat conversation and quotation languages as presentation preferences without inventing unavailable source translations.
- [x] Run deterministic citation/quotation validation and an independent claim-support audit before persistence or return.
- [x] Persist only validated assistant responses with deterministic assistant-message IDs.
- [x] Increment monthly usage only after a validated answer is produced.
- [x] Propagate cancellation and return stable fail-closed outcomes across retrieval, generation, and validation.
- [x] Cover the backend message-to-grounded-answer HTTP workflow with fake providers and no live network calls.
- [ ] Add a persistent reservation/finalization record so simultaneous retries across multiple API replicas cannot duplicate provider work or usage increments.

## Production smoke testing

- [ ] Confirm `GET https://api.askarabbi.ai/health` returns HTTP 200.
- [ ] Test email sign-in, Google sign-in, sign-up, session refresh, logout, and password reset through the WorkOS production environment.
- [ ] Verify credentialed CORS succeeds only from `https://askarabbi.ai`.
- [ ] Create, rename, load, and delete conversations and verify ownership isolation in Cosmos DB.
- [ ] Save personalization and conversation settings and verify one update cannot erase the other.
- [ ] Ask representative questions and verify every displayed citation resolves to the exact approved source text.
- [ ] Verify source selections alter managed retrieval and an unavailable preferred quotation language never causes an invented translation.
- [ ] Restart and scale the Container App and confirm existing authentication sessions remain valid.
- [ ] Confirm failed retrieval or failed citation validation never falls back to unsupported model knowledge.

## Weekly Dvar Torah activation

- [x] Add the shared Hebrew-week contract, current-or-latest service, MongoDB publication collection, deterministic week key, recovery lease, and authenticated read controller.
- [x] Add the lazy-loaded frontend sidebar destination with loading, pending, fallback, error, desktop, and mobile-compatible states.
- [x] Add the separate .NET 10 one-shot host, Docker image, disabled generation gate, and Azure deployment workflow for a Sunday Container Apps Job schedule.
- [x] Approve and implement the Torah-first content structure, no-subscription RSS/Atom source policy, prompts, deterministic 80% grounding policy, independent neutrality/inclusion/safety review, searchable metadata, and generator versioning.
- [x] Add deterministic tests for RSS parsing/failure isolation, parashah-range filtering, 80% grounding, exact quotations, repair, violence, racism, protected-group targeting, and fail-closed publication behavior.
- [x] Provision `askarabbi-weekly-dvar-torah` in the existing Container Apps environment with cron `5 8 * * 0`, one replica, a 35-minute timeout, two retries, ACR managed-identity pull, and `DvarTorah__GenerationEnabled=false`.
- [x] Configure its MongoDB connection through a verified job-level secret, grant its managed identity resource-scoped access to the existing Azure model/vector store, and confirm the collection is exactly `WeeklyAIDvarTorahs`.
- [x] Give the job identity `AcrPull` and run the production deployment workflow once to verify both immutable image digests are applied.
- [x] Run a disabled manual execution and confirm it succeeds with the structured `WeeklyDvarTorahGenerationDisabled` event and no publication attempt.
- [ ] Validate current, fallback, retry, active-lease, expired-lease, and publication behavior against a non-production MongoDB collection.
- [ ] Configure Container Apps execution-failure alerts, Log Analytics queries, and an Azure cost budget, then set `DvarTorah__GenerationEnabled=true`.

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
