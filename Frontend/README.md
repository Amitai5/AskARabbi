# AskRabbi frontend

The production frontend shell is a React 19, TypeScript, Tailwind CSS 4, and Vite 8 application. It currently implements:

- A responsive demo login screen with email and Google entry points.
- A provider-neutral authentication client boundary that can later call a WorkOS-backed AskRabbi API.
- A responsive conversation dashboard with local sample conversations.
- A working new-conversation interaction and local-only composer demonstration.
- A working Personalization screen for full name, birth date/time/place/time zone, religious background, Jewish heritage or community, and up to 2,000 characters of optional context.
- A profile menu with active Personalization and logout actions; Settings remains a disabled placeholder.
- Desktop sidebar collapse and a mobile navigation drawer.

No account, profile, question, or conversation data is sent to an API in this milestone. Saved personalization lives only in React process memory and is removed by logout, refresh, or closing the tab; it is not written to browser storage.

## Personalization boundary

The demo collects birthplace and an IANA birth time zone in addition to birth date and time. Those details are necessary because a Hebrew calendar date changes at sunset. The frontend intentionally does not calculate or claim a Hebrew birthday: the future backend must use a reviewed Hebrew-calendar and sunset calculation, preserve the original input, and return a verifiable result.

Profile labels guide wording and potentially relevant community distinctions. They do not count as source evidence, establish observance, or authorize the model to stereotype a user. The production API will own validation, authorization, storage, deletion, and the minimum profile projection sent to the answer system.

## Requirements

- Node.js 22.12 or newer; Node.js 24 LTS is recommended.
- pnpm 11.

## Commands

```powershell
cd Frontend
pnpm install
pnpm dev
```

Verification:

```powershell
pnpm verify
```

The Vite development server defaults to `http://localhost:5173`. Use `pnpm preview` after `pnpm build` to inspect the production bundle locally.

## Authentication boundary

`src/features/auth/AuthProvider.tsx` depends on the narrow `AuthClient` contract in `authTypes.ts`. `demoAuthClient.ts` is process-memory-only. It models user-facing methods such as Google rather than presenting WorkOS as a login choice.

The production adapter will call the AskRabbi backend, which will own the WorkOS AuthKit authorization URL, callback, token exchange, user mapping, and secure application session. No WorkOS API key or provider secret belongs in this Vite project. See the [production authentication plan](../docs/AUTHENTICATION.md).
