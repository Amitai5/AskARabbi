# Conversations about a teaching

Current and archived D'var Torah pages offer **Ask about this teaching** at the top of the article. This is the only teaching-context question action: selecting body text does not display a popup or prepare a question. The top button prepares a new conversation with the whole teaching and an editable question; nothing is sent until the reader submits it. A context card identifies the teaching and can be removed before the first message. Existing conversation drafts are preserved.

Native text selection and copying remain available alongside word-by-word audio seeking. The top ask button supports normal keyboard navigation and activation. Narrated words still support arrow navigation and Enter/Space to play.

## Additive API contract

`POST /api/conversations` (including `?compact=true`) accepts an optional `teaching` object alongside the existing first-message fields:

```json
{"weekKey":"diaspora:2026-08-29","selectedText":null}
```

The frontend sets `selectedText` to `null` for the whole teaching. The optional selection contract remains supported for existing clients and stored conversations; a non-null selection must be nonblank, at most 4,000 characters, and match the publication after display-text and whitespace normalization. Only eligible published teachings are accepted. Invalid selections return 400; unavailable, future, or wrong-cycle publications return 404, without creating a conversation or calling the model. Clients cannot supply their own teaching body.

Conversation details and compact turn responses include optional `teachingContext` with `weekKey`, `title`, and `selectedText`. Append-message requests are unchanged: the server reuses the saved context. List responses remain small and unchanged.

## Persistence and grounding

An optional `teachingContext` subdocument on the existing MongoDB conversation stores the normalized full body, title, week key, reference list, and selected text. It is saved once with the first message and retained for follow-ups, even if the publication later changes. Existing documents without this field work unchanged; no migration or new index is required. Deleting the conversation removes its snapshot with the same ownership checks as other conversation data.

The snapshot is explicitly untrusted reading context, not instructions or approved source evidence. The entire body reaches the answer model and grounding audit. Source retrieval uses bounded context and up to eight cited canonical references while preserving enabled-source/language filters. Existing evidence and quotation checks remain in force. Teaching conversations use additional model context tokens; ordinary conversations do not attach a body. No production dependency was added.

## Rollout

Deploy the backend before the frontend so the optional request field is understood when readers first use the feature. There is no data backfill and no change to teaching generation or publication schedules.
