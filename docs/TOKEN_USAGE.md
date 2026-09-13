# Monthly chat token allowance

Each account has **5,000,000 tokens per UTC calendar month**. Settings display the remaining percentage, not a token or question count; chats show a notice when the allowance is exhausted. The server remains authoritative even if a browser is stale or bypasses the UI.

## Configuration

- Production default: `Backend/AskARabbi.Api/appsettings.Production.json`, `Usage:MonthlyTokenLimit`.
- Environment override: `Usage__MonthlyTokenLimit=5000000`.
- Code fallback and validation: `Backend/AskARabbi.Api/Usage/MonthlyUsageOptions.cs`.
- The old `Usage:MonthlyAnswerLimit` / `Usage__MonthlyAnswerLimit` setting is no longer used.

All three defaults agree at 5M: application options, production settings, and the example configuration. A non-positive limit fails startup validation.

Changing the allowance does not reset or rewrite existing monthly counters. After deploying the reduced limit, already-recorded usage is compared with 5M; an account at or above 5M cannot start another new chat or follow-up until the next monthly reset. If the API has an explicit environment override, set it to `5000000` or remove it to use the production default.

## Accounting and enforcement

The monthly MongoDB document keeps a durable `Int64` `tokenCount`, identified by the authenticated local user ID and `yyyyMM`. This counter is shared across every conversation and API replica; it is not calculated from whichever chats the browser happens to load.

Each completed provider response reports its input and output usage before its result is interpreted. Retrieval, answer generation, calendar-tool continuations, independent validation, repairs, and retry responses are counted, including incomplete or rejected answers. Output usage already includes reasoning tokens. Cached input tokens are not subtracted or added a second time. Dvar Torah generation, reading, and audio do not spend a user's chat allowance.

Both conversation-creation and follow-up endpoints check admission before saving the new user message. A per-account/month lease permits one active chat request at a time, across tabs and replicas. Each subsequent provider call rechecks the counter. Cumulative compare-and-set accounting avoids duplicate increments if an accounting write is repeated; a completed follow-up replay returns the saved answer without calling the model again.

An already-admitted provider response can finish slightly above the allowance, because its exact usage is only available afterward. The actual total is retained, the consumed percentage clamps at 100% (0% remaining in the UI), and no further provider request or question is admitted. This is an admission limit, not a promise that a paid response is terminated at exactly token 5,000,000. A provider timeout without returned usage cannot be accurately charged from an estimate.

Usage is assigned to the month when the chat request began, even if its answer completes after midnight. A fresh month has a fresh counter without a reset job. The frontend refreshes at the UTC month boundary, on focus, and when another tab reports new usage. Deleting chats does **not** reset allowance; full account erasure removes the usage data with the rest of the account.

The chat lease expires after ten minutes to recover from a crashed replica, longer than the existing five-minute API mutation timeout. A lost lease or failed accounting write stops additional AI work. Received usage writes use their own bounded timeout so a browser disconnect cannot cancel an already-known charge.

## API/frontend release contract

Deploy the backend and frontend together. No Mongo data rewrite is required; new fields are additive, and the legacy `answerCount` field remains readable but is not used for enforcement. Historical chats did not retain accurate per-user token totals, so old answer counts are **not** converted into invented token usage. Tracking starts when this release runs.

`GET /api/conversation-settings/usage` returns:

```json
{
  "periodStartUtc": "2026-09-01T00:00:00+00:00",
  "periodEndUtc": "2026-10-01T00:00:00+00:00",
  "tokensUsed": 2500000,
  "tokenLimit": 5000000,
  "tokensRemaining": 2500000,
  "usedPercent": 50,
  "isLimitReached": false
}
```

This replaces the old answer-count response fields. Full and compact conversation-turn responses also include the latest `usage` object, eliminating a mandatory extra refresh after an answer.

- Already exhausted: HTTP **429**, problem `code: "usage_limit_reached"`, the `usage` object, and `Retry-After` set to the UTC month end. No new message is saved.
- Another answer is active: HTTP **409**, `code: "chat_in_progress"`.
- Accounting cannot safely continue: HTTP **503**, `code: "usage_unavailable"`.
- If an earlier stage consumes the final tokens and the next stage is blocked, the normal turn response has `status: "usage_limit_reached"`, the persisted question, and latest usage. No unvalidated answer is shown.

At 100%, the composer disables button and Enter submissions for new and old conversations, explains the reset date, and links to Dvar Torah. Saved messages, sources, account settings, and weekly text/audio remain accessible.

## Weekly publication idempotency

Dvar Torah uniqueness remains keyed by the exact Shabbat date and configured reading cycle, e.g. `diaspora:2026-09-05`, not the parashah name. The coordinator checks for a published article and acquires the existing exclusive Mongo publication lease **before** the deferred generator loads the corpus or initializes AI/research services. Repeated or concurrent job runs cannot publish a second article for that key; the same parashah in another year's week remains valid. Existing publication audio can independently resume its idempotent audio workflow without regenerating the text.

## Impact

- **Performance:** small indexed Mongo quota reads and accounting writes per provider response; duplicate weekly runs avoid corpus/model initialization, and turn responses include usage to avoid an extra browser round trip.
- **Correctness:** account-scoped enforcement, atomic accounting, repeat-write protection, and backend checks independent of browser state. The in-flight overshoot and unavailable historical usage are intentional limitations described above.
- **Maintenance:** one configurable token limit and one shared usage response, with no new runtime dependencies or changes to AI answer/grounding contracts.
