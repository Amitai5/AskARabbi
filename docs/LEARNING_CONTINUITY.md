# Learning continuity

The dashboard keeps pending answers alive when readers switch pages. Successful answers completed away from their conversation show a dismissible, polite “Answer ready” notice for ten seconds and a dot on the conversation. Dismissing the notice does not clear the dot; opening the answer does. Failures are not labeled as ready. These indicators are local to the open app, not push notifications.

## Navigation

The following additive frontend routes preserve the page on refresh and support browser Back/Forward:

- `/calendar?days=180&search=Rosh` (90 days by default; optional search and 180/360-day ranges).
- `/teachings` for the current weekly teaching.
- `/teachings/archive?page=2&search=community` for a filtered archive page.
- `/teachings/{encoded-week-key}` for a selected publication, including its Israel/Diaspora cycle.
- `/conversations/{conversation-id}` and `/conversations/new`.

Existing `/settings/...` routes and `/personalization` continue to work. No backend API or persisted document schema changes are required. The static host must serve the SPA entry point for these frontend paths, as it does for settings. URL IDs are still authorized by the existing API; a missing conversation or teaching shows an error rather than silently opening a different record.

Draft contents are never put in URLs or browser storage. Unsent drafts are kept while navigating within the open dashboard; they are not persisted across a full reload. Temporary conversation IDs stay in tab history state only and are resolved to the server ID when the answer finishes.

## Starting and continuing learning

Six starter questions fill the editable composer without submitting. The first uses the next major holiday from the existing calendar/offline preload when available, otherwise it asks about the weekly Torah portion. Suggestions make no extra network or model calls.

“Ask about this” is available in conversation and teaching source readers and holiday details. It includes the exact canonical source reference and link, or holiday name, dates and calendar reference. Existing draft text is kept, with the new question appended. If the combined text exceeds the composer limit, the existing draft remains unchanged and the reader is asked to shorten it. Offline and usage-exhausted chat restrictions still apply.

## Audio

Rewind/forward skip 15 seconds, clamp to the recording bounds, preserve paused playback, and use pending positions when metadata is still loading. They share the existing stream/saved-recording seek path so offline seeking and word highlights remain synchronized.

Where supported, Media Session exposes the teaching title, play/pause, seeking and playback position to device controls after the reader starts listening. Unsupported actions are optional and do not stop playback. The session and handlers are cleared when leaving the player. Physical lock-screen presentation depends on the browser and device; this does not add playback across app-page navigation.

## Verification

Regression coverage lives in `LearningContinuity.test.tsx`, `pageRoutes.test.ts`, `DvarTorahReadAloud.test.tsx`, and the existing navigation, source, calendar, and teaching suites. Run from `Frontend`:

```text
pnpm test:run --maxWorkers=2
pnpm lint
pnpm build
```

Browser QA should cover desktop and small/mobile widths, no unexpected scroll/focus jumps on completion, exact-page reloads, browser Back/Forward, draft preservation, the source sheet, paused/playing seeks, and unsupported Media Session behavior. Synthetic API/audio fixtures test interactions without creating production conversations or consuming model usage.
