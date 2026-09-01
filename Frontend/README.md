# AskRabbi frontend

The production frontend shell is a React 19, TypeScript, Tailwind CSS 4, and Vite 8 application. It currently implements:

- A responsive login screen whose email, Google, and account-creation actions begin the backend-owned WorkOS/AuthKit flow.
- Cookie-authenticated session hydration from the .NET API; no WorkOS secrets or provider tokens enter the Vite bundle.
- A responsive conversation dashboard backed by owner-scoped API data.
- A weekly-learning destination separated from recent conversations in the sidebar; its React module and current-or-latest API request load only when opened.
- Working create, load, rename, source-update, message-save, and confirmed-delete operations.
- Per-conversation source controls for the same approved logical sources used by the .NET prototype: Torah, Tanakh, Mishnah, Talmud, Rif, Mishneh Torah, Shulchan Arukh with Rema, Zohar, Zohar Chadash, and Mesillat Yesharim.
- Structured answer sources with clickable inline citation numbers, exact validated quotations, direct canonical Sefaria passage links, edition/license metadata, and expandable surrounding context. On wide screens, citations open an independently scrollable source reader beside the chat; on smaller screens, they open a dismissible bottom sheet with the same source navigation.
- An accessible animated grounded-answer status while retrieval, quotation checks, and generation are still running.
- Welcome prompts that cycle on page load and new conversations, then remain static while visible.
- A compact Personalization screen for full name, birth date/time, a reviewed U.S. time-zone selection, response and source-quotation languages, religious background, Jewish heritage or community, and up to 2,000 characters of optional context.
- An active Settings screen with verified account email, WorkOS password-reset request and confirmation flows, exact API-reported usage periods, and Cosmos-backed conversation preferences.
- A profile menu with active Settings, Personalization, and logout actions.
- Desktop sidebar collapse and a mobile navigation drawer.

The browser now uses the .NET API as the authority for the authenticated user, personalization, account preferences, usage, weekly Dvar Torah publication, conversation summaries, source selections, and message history. Requests send `credentials: "include"`; the backend owns and validates the `HttpOnly` application cookie. The weekly module is dynamically imported and fetches `/api/dvar-torah` only after its sidebar destination is selected; a current publication is cached through the next weekly boundary, while pending or fallback responses use a short retry cache. A new-conversation screen remains an unsaved local draft until its first message is submitted. The first successful grounded response returns the backend-applied AI title; later responses preserve that title unless the user explicitly renames it. Only the non-sensitive welcome-prompt index remains session-local.

The message endpoint now stores the user turn, retrieves only from the configured approved corpus, generates a structured draft, validates every citation and quotation, and persists the validated assistant answer together with trusted source snapshots. New frontend requests opt into the bounded current-turn response and merge those messages into already loaded context, so long chats do not repeatedly download their complete history; the API's original full-context response remains available for compatibility. The UI surfaces typed fail-closed outcomes instead of fabricating or retaining a local answer. Inline citations open the exact trusted source in a side-by-side reader at the `xl` breakpoint and in a touch-friendly bottom sheet below it; previous/next controls move among the current answer's sources without disturbing the chat scroll position. Source context starts closed unless the account-backed `showSourceContextByDefault` preference is explicitly enabled; exact quotations and canonical source links remain available either way. The redundant edition-attribution footer is not rendered, while its provenance URL remains in the trusted stored source record. Older off-screen messages use browser content visibility, and assistant rows are memoized to reduce mobile rendering work while an answer arrives.

## Personalization boundary

Personalization collects one reviewed U.S. IANA birth time zone in addition to birth date and time. A time zone establishes regional civil-date context, but it cannot determine the precise local sunset without a location. The frontend intentionally does not calculate or claim a Hebrew birthday: a future backend milestone must request birthplace when sunset precision matters, use a reviewed Hebrew-calendar and sunset calculation, preserve the original input, and return a verifiable result.

Password-reset requests/confirmation, monthly usage, and conversation defaults now come from the backend. Usage remains informational until grounded-answer generation reserves and records completed answers.

Profile labels guide wording and potentially relevant community distinctions. They do not count as source evidence, establish observance, or authorize the model to stereotype a user. The API owns validation, authorization, and persistence; the later answer endpoint must send only the minimum profile projection required for personalization.

The response-language and quotation-language choices are independent. Both default to English and support English, French, German, Hebrew, Italian, Persian, Polish, Russian, Spanish, and Yiddish in alphabetical display order. A quotation preference is a retrieval preference, not permission to fabricate a translation: the production answer system must disclose when an approved edition is unavailable and use only validated corpus text.

Source filters are conversation-scoped and use the prototype's stable `collection:*` and `work:*` keys. New conversations enable only the four core collections—Torah, Tanakh, Mishnah, and Talmud—by default. Supplemental works remain visible and can be added individually, while quick actions restore the core set or select every approved source. Non-empty selections are saved through the conversation API, and the composer disables sending when all sources are cleared so the answer pipeline cannot silently use an unsupported source set.

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

The Vite development server defaults to `http://localhost:5173`, and development API calls default to `http://localhost:5090`. A production build defaults to `https://api.askarabbi.ai` for the frontend hosted at `https://askarabbi.ai`. Override only the public API origin with `VITE_API_BASE_URL`; Vite exposes every `VITE_*` value to browser code, so never place a WorkOS key, MongoDB connection string, or other secret there. Use `pnpm preview` after `pnpm build` to inspect the production bundle locally, and follow the [production deployment plan](../docs/PRODUCTION_DEPLOYMENT.md) for DNS, SPA rewrites, WorkOS, and backend secrets.

## Authentication boundary

`src/features/auth/AuthProvider.tsx` depends on the narrow `AuthClient` contract in `authTypes.ts`; `backendAuthClient.ts` implements it with the AskRabbi API. Email supplies a WorkOS login hint, Google selects WorkOS Google OAuth directly, and account creation supplies the WorkOS sign-up screen hint. The backend owns every authorization URL, callback, token exchange, password-reset confirmation, user mapping, and secure application session. No WorkOS API key or provider secret belongs in this Vite project. Hermetic UI tests inject in-memory clients from `src/test` instead of contacting external services. See the [production authentication design](../docs/AUTHENTICATION.md).

For a credential-free account and conversation walkthrough, run the API with its explicitly Development-only `local-demo` profile, then run Vite. That mode exercises the real controllers, cookies, ownership rules, and API adapters while storing data only in the API process. Grounded chat additionally needs all `AI:*` settings; when they are omitted, the API remains healthy and returns `ai_unavailable` without calling a model:

```powershell
dotnet run --project Backend/AskARabbi.Api --launch-profile local-demo
cd Frontend
pnpm dev
```
