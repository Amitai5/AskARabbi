# AskRabbi frontend

The production frontend shell is a React 19, TypeScript, Tailwind CSS 4, and Vite 8 application. It currently implements:

- A responsive demo login screen with email and Google entry points.
- A provider-neutral authentication client boundary that can later call a WorkOS-backed AskRabbi API.
- A responsive conversation dashboard with local sample conversations.
- A working new-conversation interaction and local-only composer demonstration.
- A profile menu with disabled Settings and Personalization placeholders plus working logout.
- Desktop sidebar collapse and a mobile navigation drawer.

No account, profile, question, or conversation data is sent to an API in this milestone.

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
