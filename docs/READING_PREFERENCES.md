# Settings & Personalization

The profile menu opens one settings area. Its sidebar has deep-linked sections:

- `/settings/account`: account email, password reset, and monthly usage.
- `/settings/personalization`: name, birth details, shared locations, languages, Jewish background, and additional context.
- `/settings/reading`: reading presets, theme, focused reading, and source-context defaults.
- `/settings/notifications`: optional product-update emails.
- `/settings/app`: app installation and device-local offline teaching/audio controls.
- `/settings/data`: chat privacy information, delete-all-chats, and account deletion.

`/settings` redirects client-side to Account; `/personalization` redirects to Personalization. Browser back/forward and direct section links are supported. Existing chat requests stay in the dashboard while settings are open. Switching sections preserves unsaved form edits.

Search metadata and targets are defined in `Frontend/src/features/settings/settingsRegistry.ts`. Register each new setting and render its `SettingAnchor` (or matching `setting-<id>` target). Search considers labels, descriptions, section names, and keywords. Results navigate, scroll within the settings pane, focus the control, and highlight it briefly.

## Reading behavior

Text sizes: `small`, `default`, `large`, `extra-large`. Line spacing: `compact`, `default`, `relaxed`. Theme: `light`, `dark`, `system` (default). `focusLongContent` defaults to false. Typography applies to answers, teachings, and the live preview, not navigation or other interface controls.

Reading changes apply immediately and autosave after a short debounce. Writes are serialized so rapid changes cannot save out of order. Failed/offline writes retain the local choice, expose Retry, and sync when the device reconnects. Other settings retain their existing save behavior.

Answers and teachings with at least 150 words or 1,000 characters offer Focus. If enabled by default, only the latest eligible chat answer enters automatically. Focus hides navigation, other messages, and the composer; Exit focused reading or Escape returns to the regular view. Teaching audio, word seeking, citations, and copying retain their existing state. The offline teaching also uses the cached presentation preferences.

## API and storage

Authenticated `GET` and `PUT /api/conversation-settings/reading` return this complete JSON shape:

```json
{"textSize":"default","lineSpacing":"default","theme":"system","focusLongContent":false}
```

PUT validates supported presets and derives the owner from the authenticated session. Responses are private/no-store. MongoDB stores an optional `readingPreferences` subdocument in the existing per-user conversation-settings document. Atomic field updates preserve personalization, notification, source-context, and legacy calendar preferences. Account deletion removes the containing document as before. Existing accounts need no migration: absent preferences return defaults. Deploy the new endpoint with the frontend; unavailable endpoints leave local reading choices usable with a visible sync error.

The browser stores only presentation choices and a pending-sync flag under `askarabbi.reading.v1:<userId>`, plus an active-user hint. No name, email, location, chat text, or credentials are added to this cache. A small pre-paint script restores the cached theme and reading presets before the app renders. Signing out clears the active-user hint; each account uses a separate cache. Offline audio preferences remain device-local, unchanged.

No new runtime dependencies were added.
