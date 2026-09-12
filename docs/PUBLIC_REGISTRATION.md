# Public registration and account capacity

## Configuration

The backend defaults to **100 current AskRabbi accounts**, including accounts that already existed before this change. Configure `Registration:AccountLimit` in the API configuration (`Backend/AskARabbi.Api/appsettings.Production.json`), or use the Azure Container App environment variable:

```text
Registration__AccountLimit=100
```

Set it to **`0` for unlimited registration**. Negative values fail startup. A lower limit never removes accounts or blocks an already-admitted user from signing in; it closes new registrations until capacity is available. Restart/redeploy every API replica when changing the limit so all replicas use the same setting.

This is an application-account limit, not a chat-usage limit. It does not grant public access to existing users' private data, change authentication requirements, or remove user-scoped authorization.

## Admission and user experience

- `GET /api/user/registration` is anonymous and returns only `{ "isOpen": true/false }`, with `Cache-Control: no-store`. It exposes no account counts, emails, or user identifiers.
- The login page shows **Registration is currently full** when closed, explains that new accounts are not being accepted, and links to `support@askarabbi.ai`. Google/email sign-in and password recovery remain available to existing users.
- `GET /api/user/login?screen=sign-up` checks availability before redirecting to AuthKit.
- **Every callback**, including Google and email flows that started on the sign-in screen, checks admission before persisting an AskRabbi account or issuing its cookie. A full registration redirects only to the configured frontend with `?registration=closed`.
- Frontend availability is advisory: if another person takes the last place, the callback still rejects the excess account. No browser setting can override the cap.
- WorkOS may already have created a provider identity before the callback rejects application admission. That identity alone has no AskRabbi account, session, chat access, or saved data. We do not automatically delete provider identities on denial. If provider-side identity creation itself must also be capped, configure a separately authenticated [WorkOS registration Action](https://workos.com/docs/authkit/actions); the application cap remains required regardless.

## Persistence and concurrency

The admission singleton uses the reserved non-GUID `_id: "account-registration"` in the existing `MongoDB:ConversationSettingsCollectionName` collection (`conversationSettings` by default). Account settings always use the account's GUID as `_id`, so their reads, updates, and erasure cannot collide with this record. It stores admitted provider IDs, unfinished operation reservations, and an increasing revision. The first initialization imports the existing `users.providerUserId` values. No emails, credentials, or tokens are copied into this record.

`MongoDB:RegistrationCollectionName` is an optional override for an already-provisioned ledger location. Leave it unset to reuse the settings collection. Do not change this location after the ledger is initialized without draining callbacks and migrating the complete record. An installation that already admitted users into the earlier `accountRegistration` collection must explicitly retain that name until a coordinated migration; never silently bootstrap a second ledger.

The production rollout found that the shared-throughput database already had 25 collections. Implicitly creating `accountRegistration` attempted to allocate another 400 RU/s and hit the account's 1,000-RU/s cap. Reusing the settings collection avoids both the [shared-throughput collection limit](https://learn.microsoft.com/en-us/azure/cosmos-db/concepts-limits) and an additional throughput allocation. No collections or user data are deleted, and the throughput cap stays unchanged. Production explicitly sets `MongoDB__RegistrationCollectionName=conversationSettings` to pin this durable location.

An admission counts distinct identities in that single record, then atomically reserves a place only if its observed revision still matches. After the account write is acknowledged, one atomic update records the admitted identity and removes only that operation's reservation. Two callbacks for the same identity count as one account.

This avoids a cross-collection count-then-create race and does not require MongoDB transactions or a stronger Azure consistency tier. [MongoDB's single-document atomicity](https://www.mongodb.com/docs/manual/core/write-operations-atomicity/) protects the conditional update; a stale singleton read cannot win a revision comparison. Cosmos DB's [session consistency](https://learn.microsoft.com/en-us/azure/cosmos-db/consistency-levels) is not treated as a globally current multi-collection snapshot.

Account erasure releases the registered place after owned chat/settings/usage data are erased, while retaining the disabled deletion recovery record until that release succeeds. The account deletion worker retries interrupted cleanup. In-flight registration reservations are not indiscriminately cleared by deletion.

Uncertain account writes or cancelled requests retain a reservation conservatively: releasing it could oversubscribe if the write committed late. The same verified identity may retry even at capacity. Never add a TTL to these reservations or reset the singleton while callbacks are running. If an interrupted registration leaves an orphan reservation, reconcile it administratively only after draining callbacks and verifying the account/provider state; removing a reservation is a capacity-affecting operation. The registration record contains identity references and must be included in account-erasure operational checks for these exceptional interrupted flows.

The singleton design is intentionally small for this 100-account release. `0` removes the configured ceiling, but a very large future user base should migrate the admission ledger before approaching MongoDB's document-size limit.

## Safe public-launch sequence

1. Deploy the API and frontend with `Registration__AccountLimit=100` (or the checked-in default). Keep external signup restrictions in place during the rollout, and drain all API revisions that predate this guard.
2. Call `/api/user/registration` to initialize the ledger. Verify its imported identities match the existing user accounts before opening signup. Do not recreate or edit the ledger casually: it is durable admission state, not a cache.
3. In the existing **production** WorkOS environment, review signup, invitations/waitlist, registration Actions, and email-domain restrictions. Enable public signup/remove only the restriction responsible for the private beta. WorkOS [invitations](https://workos.com/docs/authkit/invitations) can allow selected users while signup is otherwise disabled.
4. If the Google OAuth application is still in Testing, review/publish that application's production audience separately. Do not confuse Google's test-user restriction with the backend account cap. Keep identity verification, redirect URI restrictions, and private networking intact.
5. Verify an existing account can sign in and a permitted new identity can complete signup. Use a non-production environment with a small limit to verify the closed state; do not create 100 disposable production accounts or temporarily lower the live cap to test it.

The deployment workflow also checks that `/api/user/registration` returns a boolean `isOpen` after the new API revision is healthy, so a database admission failure cannot be hidden by the process-health endpoint.

External WorkOS/Google settings and deployment are not changed merely by editing this repository. Do not announce registration as publicly open until those rollout steps are verified.

## Compatibility

The registration-availability GET route and configuration are additive. Existing login/callback URLs are unchanged. The new intentional callback outcome is a redirect with `registration=closed` instead of creating an excess application account. Existing users and their saved data require no migration; the first ledger initialization imports their provider IDs. Existing account deletions now also update the admission ledger.
