# Conversations about a teaching

Current and archived D'var Torah pages offer **Ask about this teaching**. Selecting body text also offers **Ask about this passage**. Both prepare a new conversation with an editable question; nothing is sent until the reader submits it. A context card identifies the teaching and optional passage, and can be removed before the first message. Existing conversation drafts are preserved.

Text selection remains available alongside word-by-word audio seeking. Keyboard users can press Tab after selecting text to reach the passage action, Enter to activate it, and Escape to dismiss it. Narrated words still support arrow navigation and Enter/Space to play.

## Additive API contract

`POST /api/conversations` (including `?compact=true`) accepts an optional `teaching` object alongside the existing first-message fields:

```json
{"weekKey":"diaspora:2026-08-29","selectedText":"Choose life."}
```

Set `selectedText` to `null` or omit it for the whole teaching. A non-null selection must be nonblank, at most 4,000 characters, and match the publication after display-text and whitespace normalization. Only eligible published teachings are accepted. Invalid selections return 400; unavailable, future, or wrong-cycle publications return 404, without creating a conversation or calling the model. Clients cannot supply their own teaching body.

Conversation details and compact turn responses include optional `teachingContext` with `weekKey`, `title`, and `selectedText`. Append-message requests are unchanged: the server reuses the saved context. List responses remain small and unchanged.

## Persistence and grounding

An optional `teachingContext` subdocument on the existing MongoDB conversation stores the normalized full body, title, week key, reference list, and selected text. It is saved once with the first message and retained for follow-ups, even if the publication later changes. Existing documents without this field work unchanged; no migration or new index is required. Deleting the conversation removes its snapshot with the same ownership checks as other conversation data.

The snapshot is explicitly untrusted reading context, not instructions or approved source evidence. The entire body reaches the answer model and grounding audit. Source retrieval uses bounded context and up to eight cited canonical references while preserving enabled-source/language filters. Existing evidence and quotation checks remain in force. Teaching conversations use additional model context tokens; ordinary conversations do not attach a body. No production dependency was added.

## Rollout

Deploy the backend before the frontend so the optional request field is understood when readers first use the feature. There is no data backfill and no change to teaching generation or publication schedules.
