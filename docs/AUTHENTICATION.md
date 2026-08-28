# Production authentication design

## Decision and current state

AskRabbi uses WorkOS AuthKit as its identity and user-management boundary. WorkOS is not presented as a separate sign-in brand: users see the Google, email/password, Apple, Microsoft, or other reviewed methods enabled in the hosted AuthKit configuration. The browser never receives the WorkOS API key or provider tokens.

The .NET 10 backend implements the server flow with the pinned `WorkOS.net` 6.2.0 SDK. The React `backendAuthClient` begins purpose-specific email, Google, or sign-up flows, hydrates the user only from the session endpoint, sends credentialed API requests, completes password recovery, and performs backend-owned logout.

The implementation was checked against:

- [AuthKit overview](https://workos.com/docs/authkit/overview)
- [Social Login](https://workos.com/docs/authkit/social-login)
- [Sessions](https://workos.com/docs/authkit/sessions)
- [Password reset](https://workos.com/docs/reference/authkit/password-reset)
- [Official WorkOS .NET SDK](https://github.com/workos/workos-dotnet)

## Implemented flow

```mermaid
sequenceDiagram
    participant Browser as AskRabbi frontend
    participant Api as AskRabbi backend
    participant WorkOS as WorkOS AuthKit
    participant Provider as Enabled login method
    participant Mongo as Cosmos DB for MongoDB

    Browser->>Api: GET /api/user/login + optional email/provider/screen hint
    Api->>Api: Generate state, PKCE verifier, and S256 challenge
    Api-->>Browser: Short-lived HttpOnly state/verifier cookies + redirect
    Browser->>WorkOS: Follow hosted AuthKit redirect
    WorkOS->>Provider: Complete configured login method
    Provider-->>WorkOS: Verified identity
    WorkOS-->>Api: Authorization code + state
    Api->>Api: Constant-time state check; recover PKCE verifier
    Api->>WorkOS: Exchange code + verifier using server API key
    WorkOS-->>Api: Verified WorkOS user and session response
    Api->>Mongo: Upsert by immutable WorkOS user ID
    Api-->>Browser: Encrypted HttpOnly AskRabbi cookie + frontend redirect
    Browser->>Api: GET /api/user/session
    Api->>WorkOS: Rotate refresh token near access-token expiry
    Api-->>Browser: Renew protected AskRabbi ticket
    Api-->>Browser: Minimum safe local account projection
```

State and PKCE cookies expire after ten minutes and are deleted at the callback. The authorization code is exchanged only by the backend. The WorkOS access token is never stored or returned to React. Its `sid` and `exp` claims are read only from the provider response; the rotating refresh token, session ID, and expiration are retained inside the ASP.NET Core protected application ticket. Near expiration, the API exchanges the refresh token, updates the local WorkOS user projection in Cosmos, and renews the encrypted ticket. A transient provider failure retains only a still-unexpired session; an expired or provider-rejected session fails closed.

## Implemented routes

| Method and route | Responsibility |
| --- | --- |
| `GET /api/user/login` | Create state and S256 PKCE values, validate optional email/provider/screen hints, set short-lived cookies, and redirect to hosted AuthKit. |
| `GET /api/user/callback` | Validate state/verifier, exchange the code, resolve the local account, and issue the application cookie. |
| `GET /api/user/session` | Return the authenticated local user ID, display name, email verification state, and optional image URL. |
| `POST /api/user/forgot-password` | Ask WorkOS to send recovery while returning the same `202` shape for valid input regardless of account existence. |
| `POST /api/user/reset-password` | Confirm a WorkOS reset and clear the current AskRabbi cookie. |
| `POST /api/user/logout` | Clear the AskRabbi cookie and return the WorkOS logout URL when a provider session ID is available. |

There is no separate Google controller endpoint: the frontend requests `provider=google`, and the backend maps that allow-listed value into the WorkOS authorization URL. Email uses a hosted login hint, and account creation uses WorkOS's sign-up screen hint. Apple and Microsoft are accepted backend provider values for future reviewed buttons; the dashboard still controls whether each method is actually enabled.

## Identity and data ownership

WorkOS owns credentials, authentication methods, provider identities, verification, MFA, and identity linking. AskRabbi owns its immutable local user ID, religious personalization, source selections, saved conversations, usage, product authorization, and retention behavior.

Azure Cosmos DB for MongoDB stores a unique WorkOS user ID beside the immutable AskRabbi user ID. Email is mutable profile data and is never used as the datastore authorization key. Every user-owned persistence query uses the local user ID from the encrypted application principal.

Only this account projection reaches the browser:

- AskRabbi user ID
- Display name
- Email address and verification state
- Optional profile-image URL

## Cookie and secret boundary

- `WorkOS:ApiKey` and the MongoDB connection string are backend-only secrets supplied through .NET user secrets locally or deployment secret configuration.
- The application cookie is `HttpOnly`, `SameSite=Strict`, essential, and secure on HTTPS. Production must use HTTPS exclusively.
- The cookie contains an ASP.NET Core protected authentication ticket plus the rotating WorkOS refresh token needed for session continuity. It is encrypted/authenticated by ASP.NET Core Data Protection, `HttpOnly`, and unavailable to frontend JavaScript; the WorkOS access token is not stored.
- The callback target and post-login frontend URI come from server configuration and must be exact allow-listed HTTPS URLs outside local development.
- Authorization codes, cookies, provider tokens, API keys, connection strings, reset tokens, passwords, and full identity payloads must never be logged.
- Forgot-password responses intentionally do not disclose whether an email address exists.

## Required hardening before public launch

The implemented ticket has an eight-hour sliding lifetime and refreshes its WorkOS session near access-token expiration. Password reset revokes WorkOS sessions, and this API clears the requesting browser's local ticket after a successful reset. This is substantially better than a disconnected local cookie, but it is not the final horizontally scaled cross-device revocation design: another already-issued AskRabbi ticket is rejected only when it next attempts a WorkOS refresh.

Before public deployment:

1. Add an opaque shared server-side session record with atomic rotation, expiry, revocation, and account-wide invalidation before running multiple API instances.
2. Persist ASP.NET Core data-protection keys in approved shared Azure storage and protect them with managed identity/Key Vault before running multiple API instances.
3. Add rate limits to login, callback, forgot-password, and reset-password routes.
4. Complete the CSRF strategy for the final frontend/API topology. Credentialed CORS already uses an exact configured origin allow-list, with only `http://localhost:5173` defaulted in Development.
5. Validate WorkOS webhook signatures and use lifecycle events for account suspension/deletion and session invalidation.
6. Add provider-enabled-method discovery before rendering more provider-specific buttons beyond Google.
7. Run a live WorkOS smoke suite separately from normal CI and complete replay, cancellation, expiry, account-linking, and cross-user security tests.

## Frontend integration

The implemented frontend keeps WorkOS SDK types and secrets out of React:

1. Email, Google, and account-creation controls send the browser to `GET /api/user/login` with only an email hint, an allow-listed provider, or a sign-up hint; no provider secret enters React.
2. `AuthProvider` hydrates the authenticated user only from `GET /api/user/session` after the backend redirects home.
3. The shared API client sends `credentials: "include"` to the exact configured backend origin.
4. Login and Settings use the backend forgot-password route and display its non-enumerating response; `/reset-password?token=...` submits the new password only to the API.
5. Logout awaits `POST /api/user/logout`, clears React state, and navigates to the returned destination.

The frontend never claims a session exists merely because a provider redirect returned. The backend session endpoint remains authoritative. Hermetic frontend tests use injected in-memory clients; the Development-only `local-demo` API profile exercises the real redirect, cookie, controller, and persistence boundaries without contacting WorkOS or MongoDB.
