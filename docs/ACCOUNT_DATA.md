# Account data deletion

## User experience

Settings has a **Your data** section. Both destructive actions require a separate confirmation dialog and an exact typed phrase. Cancellation never sends a request. The dialog prevents duplicate submission, traps focus, and retains errors so a failed request can be retried.

- **Delete all chats:** removes the owner's complete conversation history, including messages and conversations outside the sidebar's page limit. Account, preferences, and usage allowance are preserved.
- **Delete account:** disables the account immediately after durable acceptance, removes its WorkOS identity and application data, and signs the requester out. Other devices lose authenticated access on their next API request.

Other open tabs receive an ephemeral `BroadcastChannel` notification when supported. No conversation content or profile is stored in browser storage. The backend remains authoritative when a browser prohibits cross-tab messaging. Late list/answer responses cannot repopulate the cleared conversation view.

## API contract

Both endpoints require the authenticated owner's cookie. They never accept a target user ID. The exact custom header forces a CORS preflight, and credentialed CORS must retain its exact trusted-origin allow-list.

| Endpoint | Required `X-Confirm-Deletion` | Success |
| --- | --- | --- |
| `DELETE /api/user/data/chats` | `DELETE ALL CHATS` | `204 No Content` |
| `DELETE /api/user/data/account` | `DELETE ACCOUNT` | `200 {"status":"deleted"}` or `202 {"status":"pending"}` |

Missing/incorrect confirmation returns `400`; unauthenticated requests return `401`. A concurrent answer or account write returns `409` and asks the user to retry after that work completes. `202` means erasure was accepted durably, the account is disabled, and cleanup needs a retry; it does not claim physical erasure has completed.

## Data scope and retention

AskRabbi stores saved questions, answers, source references, and conversation metadata in its MongoDB-backed account history. Disabling Azure OpenAI response storage does not stop this application persistence. Settings and the sign-in screen distinguish saved history from Azure AI processing; see [chat storage and provider retention](CHAT_PRIVACY.md).

Account deletion removes the owner's conversation headers, messages (including orphaned messages), settings/personalization, usage records, and account record, plus the corresponding WorkOS user in the configured environment. Settings are owner-scoped by their Mongo `_id`, while conversations, messages, and usage use `userId`.

Chat, source-lookup, validation, repair, and background-generation requests use `store=false` with Azure OpenAI. Microsoft may still retain prompts and answers for abuse monitoring, including authorized human review. AskRabbi's deletion endpoints do not delete those provider-controlled records; see [Microsoft's data privacy details](https://learn.microsoft.com/en-us/azure/foundry/responsible-ai/openai/data-privacy#preventing-abuse).

Shared weekly Dvar Torah publications, their audio blobs, and the approved source corpus are not personal account data and are retained. Deleting an AskRabbi identity does not delete the person's Google or other upstream provider account. Service backups and operational/security logs follow separate retention policies; this API does not promise immediate physical purge of provider backups or logs.

## Concurrency and recovery

Two optional fields are added to the existing user document: `dataOperations` (bounded shared/exclusive operation leases) and `deletionRequestedAtUtc` (a durable pending-erasure marker). Existing documents remain readable without migration. Startup adds the pending-deletion index through the existing index manager. No production dependency is added.

Authenticated mutating API requests acquire a shared owner-scoped lease. Bulk chat deletion takes an exclusive lease; account deletion atomically marks an idle account as pending and rejects later writes. Normal mutations have a five-minute cancellation budget; leases have a thirty-minute crash-recovery expiry, intentionally longer than the request budget. Lease release is attempted independently of client cancellation. These updates never upsert a user. Future background writers of personal data must participate in the same operation boundary rather than writing around it.

Account cleanup deletes the WorkOS identity first, treats provider `404` as an already-completed step, then removes owned Mongo records. The pending user is removed last, so provider or database failures retain the recovery request. Cleanup is idempotent. The HTTP request allows thirty seconds for completion independently of browser disconnection. A scoped background worker retries pending requests at API startup and once a minute while the API is running; failures are logged without tokens or credentials.

Cookie validation checks the local account on every authenticated request and rejects missing/pending users. Token refresh no longer upserts accounts. The login callback also verifies the WorkOS identity after its upsert, preventing an already-exchanged callback from resurrecting an account whose identity was deleted concurrently.

## Deployment checklist

1. Deploy the backend first and drain old API revisions and in-flight requests before exposing deletion in the frontend. Old code does not honor the new pending marker or operation leases.
2. Production deliberately retains scale-to-zero, as approved on September 5, 2026. The recovery worker cannot run while the API is stopped; accepted pending erasures resume on the next startup. This means retries do not have a guaranteed wall-clock deadline during idle periods. A continuously running replica or scheduled cleanup/wake mechanism requires separate approval if timely unattended cleanup becomes necessary. No Azure scaling or spending changes are included in this deployment.
3. Verify the production WorkOS server credential can delete users in the intended environment. Use a disposable account for the live end-to-end test; never use an existing personal account to smoke-test erasure.
4. Monitor pending-erasure failures, define backup/log retention, and ensure backup restoration reconciles accepted erasure requests before serving restored personal data.
5. Verify both confirmation flows, cross-account isolation, sign-out on another device, and provider/database retry behavior. Automated coverage uses isolated fakes, not live WorkOS/Cosmos deletion.

The added database lookup and small lease updates trade modest per-request persistence overhead for revocation and erasure correctness. They add no GPT calls. The sidebar menu uses a viewport-aware React portal and native confirmation dialogs rather than adding another UI library.
