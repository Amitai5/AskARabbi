# Jewish Calendar and Sidebar Implementation Brief

## Objective

Implement a Jewish Calendar destination in the existing AskARabbi application. Users should be able to see upcoming holidays, Hebrew dates, weekly Torah readings, and optional local candle-lighting and Havdalah times without asking a chat question.

Rename the sidebar's **Weekly Learning** section to **Learning & Tools**, place Dvar Torah and Jewish Calendar in that section, and place all conversation controls below it.

Use the existing React/.NET application, calendar library, and account storage. The implementation must require no AI calls and no new paid service.

## Working requirements

- Read the applicable AGENTS.md instructions and inspect the current implementation before editing.
- Preserve all existing user-authored and unrelated working-tree changes. There are ongoing frontend and offline-behavior changes; inspect the current diff before modifying overlapping files.
- Keep changes localized. Follow the existing dependency injection, API client, authorization, persistence, and testing patterns.
- Reuse existing dependencies. Adding a new production package is not part of this plan; follow the repository's approval rule if one proves necessary.
- Implement and verify the feature. Deployment is outside this task.
- Treat the defaults in this brief as the implementation decisions unless the user provides newer instructions.

## Product decisions

### Sidebar

Use this order:

```text
AskRabbi

LEARNING & TOOLS
  This week’s Dvar Torah
  Jewish Calendar

CONVERSATIONS
  [+ New conversation]
  Recent conversation
  Recent conversation
  …

Profile / Settings
```

Requirements:

- Move the New conversation button into the Conversations section.
- Replace the Recent heading with Conversations.
- Keep Learning & Tools above the scrolling conversation list and the profile menu at the bottom.
- Preserve the existing parchment, pomegranate, typography, icon, and selection styling.
- Give each destination an accessible label and a correct selected state.
- Support the existing mobile navigation drawer and keyboard behavior.
- Keep the current conversation view as the default after sign-in.
- Calendar access must remain available when the user's chat allowance is exhausted.

### Calendar page

Build a responsive dashboard with an agenda as the primary calendar interface.

| Area | Required behavior |
| --- | --- |
| Today | Show Gregorian and Hebrew dates, including Hebrew script. Show the selected location and Israel/Diaspora schedule nearby. |
| Happening now / Coming next | Highlight an ongoing or next holiday, its date range, beginning information, and a short explanation. |
| Upcoming holidays | Show a chronological agenda. Default to the next 90 days, with options for 30 days and the next 365 days. |
| This Shabbat | Show the weekly Torah portion or festival reading status, Shabbat date, and access to Dvar Torah. |
| Local times | Optionally show candle-lighting and Havdalah times after a location has been selected. Include the date and location with each time. |

Event behavior:

- Include major holidays, minor holidays, fast days, and Rosh Chodesh by default.
- Offer optional filters for special Shabbatot and modern observances.
- Group multi-day holidays into expandable entries while preserving their individual occurrences and distinct associated holidays.
- Keep an ongoing holiday visible until it finishes, even if it started before today.
- Distinguish an event's civil observance date from the evening when it begins.
- Use accurate event-specific beginning information; do not apply an evening-start rule indiscriminately to every category.
- Opening an event shows its dates, a short reviewed explanation, and an authoritative source link.
- Maintain explanatory copy as reusable content keyed to stable event identifiers. Do not hard-code occurrence dates or generate explanations with AI.
- Respect Hebrew text direction without reversing the surrounding English interface.

### Location and timing preferences

Create dedicated calendar preferences:

- Selected supported city or U.S. ZIP code, with a display label.
- Resolved IANA timezone for that location.
- Israel or Diaspora holiday/reading schedule; default to Diaspora and make the selection visible and editable.
- Whether local times are shown.
- Candle-lighting offset and Havdalah calculation preference.
- Event category filters.

Implementation defaults:

- Begin with a searchable, reviewed list of supported cities using GeoNames identifiers, plus U.S. ZIP-code entry.
- Do not introduce a paid geocoding service or assume an undocumented city-search API exists.
- Use the selected current location. Do not reuse the birth timezone from personalization.
- Local times are optional and remain unavailable until location and timing settings are usable.
- Display and allow adjustment of the calculation conventions. Use the provider's documented location-appropriate defaults.
- Without a location, holiday dates and weekly readings remain usable. Identify the Hebrew date as the daytime date until local sunset can be determined.
- Send only the calendar parameters needed by the provider, not chat content, identity, or birth details.

## Implementation design

### Frontend

Inspect and extend:

- `Frontend/src/features/conversations/ConversationSidebar.tsx`
- `Frontend/src/features/conversations/ConversationDashboard.tsx`
- `Frontend/src/App.tsx`
- Existing Dvar Torah client, lazy-loading, and test-injection patterns.

Add a focused `Frontend/src/features/calendar/` feature containing the page, API client, types, preferences UI, and related components.

Requirements:

- Add a calendar destination to the existing active-view selection.
- Lazy-load the calendar page and its requests when opened.
- Preserve chat drafts, selected conversations, pending requests, and background answer completion while navigating.
- A completed chat answer must not navigate the user away from the calendar.
- Loading or failure of calendar data must not block the chat or Dvar Torah views.
- Closing the mobile drawer, restoring focus, and displaying active navigation must follow existing patterns.
- Preserve the existing offline Dvar Torah behavior.

The Dvar Torah publication currently uses a configured reading cycle. A calendar using another cycle must not relabel that publication as matching the user's selection. Use a general Dvar Torah navigation action or identify the actual publication cycle and date.

### Backend

Add a focused calendar overview service and authenticated API controller.

Suggested responsibilities:

- `CalendarOverviewService`: combines date calculations, reading information, holiday events, local times, and freshness information.
- Existing `IHebrewCalendarService`: authoritative Hebrew-date conversion and weekly-reading calculations.
- `HebcalCalendarClient`: typed HTTP integration for holiday schedules, sunset data, and candle-lighting/Havdalah events.
- An interface around the external provider boundary for deterministic tests and future replacement.
- Existing `TimeProvider`: current-time calculations and testable refresh boundaries.

Reuse:

- `Library/AskARabbiLIB/Calendar/IHebrewCalendarService.cs`
- `Library/AskARabbiLIB/Calendar/HebrewCalendarService.cs`
- Existing authenticated-controller and owner-scoped account-settings patterns.

Use the normal .NET HTTP and JSON facilities already available to the project. Keep external requests restricted to documented Hebcal HTTPS endpoints.

### Proposed API

| Endpoint | Purpose |
| --- | --- |
| `GET /api/calendar/overview?days=90` | Return today's dates, selected schedule/location, ongoing and upcoming events, weekly reading, optional local times, and freshness/availability information. |
| `GET /api/calendar/preferences` | Return the authenticated user's saved calendar preferences or explicit defaults. |
| `PUT /api/calendar/preferences` | Validate and save only that user's calendar preferences. |

Contract requirements:

- Bound the requested range; support the UI's 30-, 90-, and 365-day options.
- Keep civil dates as date-only values.
- Represent precise instants with an offset and associated timezone.
- Give events stable identifiers and explicit categories.
- Include source attribution and links where applicable.
- Distinguish unavailable data from an empty event list.
- Return a refresh boundary so the browser can update at the next relevant date/time transition.
- Keep these APIs independent of model generation and chat quota accounting.

These are additive endpoints. Preserve existing chat and Dvar Torah contracts.

### Persistence

Store a dedicated calendar-preferences object in the existing account settings storage.

- Use owner-scoped reads and atomic updates to that object.
- Preserve personalization and conversation preferences when saving calendar preferences.
- Old accounts with no calendar object receive defaults; no bulk data rewrite should be necessary.
- Ensure existing account deletion removes the new preferences.
- Avoid storing a separate copy of common holiday schedules for every user.

### Calendar correctness

- Determine local civil time from the selected timezone.
- Advance the Hebrew date at local sunset when the necessary data is available.
- Refresh the Gregorian date at local midnight.
- Handle Saturday-evening rollover so the upcoming reading does not remain attached to the completed Shabbat.
- Preserve Israel/Diaspora differences and festival-displaced weekly readings.
- Handle Hebrew leap years and Gregorian-year boundaries.
- Use documented provider timing events for consecutive festivals and Shabbat/festival transitions.
- Do not calculate every candle-lighting time as a universal fixed offset from sunset.
- Treat unavailable astronomical calculations explicitly; do not fabricate times.
- Do not pass date-only values through browser UTC conversions that shift their displayed day.

### Caching, performance, and failure behavior

- Cache common holiday schedules by year, reading schedule, and relevant presentation/options.
- Cache local calculations by resolved location, date range, timezone, and timing conventions.
- Use bounded caches and combine concurrent identical provider requests.
- Respect documented provider cache directives and rate limits; avoid continuous polling.
- Refresh on relevant date boundaries, location/settings changes, and page focus when data needs updating.
- Apply bounded timeouts and retries. Provider failure must not prevent locally available dates/readings from rendering.
- When cached data is used after a refresh failure, show its freshness and only use records applicable to the selected date, location, and conventions.
- If no applicable data is available, show that state for the affected section.
- Keep account-specific responses out of the existing public service-worker asset cache.
- Full offline calendar support is outside the first release.

Hebcal's API is free, requires attribution, and is rate-limited. Include a visible attribution link. No Redis instance, scheduled generation job, or additional hosted service is required for this plan.

## Delivery sequence

1. **Navigation and page shell:** update sidebar grouping, add calendar view, preserve navigation state, and build the responsive layout.
2. **Core calendar:** implement overview API, cached holiday schedules, Hebrew dates, weekly readings, event details, and agenda filters.
3. **Location and local times:** persist preferences, resolve supported locations, add timing options, sunset handling, and freshness behavior.
4. **Verification and documentation:** complete regression coverage, exercise failure states and mobile behavior, and document provider/configuration details.

The completed first release includes all four passes.

## Tests and verification

Use the repository's existing MSTest and frontend test frameworks.

### Backend and library tests

Use fixed time, fake HTTP responses, and recorded fixtures. Automated tests must not call live providers.

Cover:

- Sunset and midnight boundaries, daylight-saving transitions, and Saturday evening.
- Israel/Diaspora schedule differences and festival readings.
- Leap years, cross-year ranges, ongoing holidays, and multi-day grouping.
- Date-only serialization and offset-aware local times.
- Consecutive holiday/Shabbat timing events.
- Missing or invalid locations, invalid settings, and unavailable astronomical calculations.
- Provider timeout, rate limiting, malformed responses, and cached-data behavior.
- Owner-scoped preferences, defaults for existing accounts, and account deletion.
- No AI calls and no chat quota consumption.

### Frontend tests

Cover:

- Sidebar order, selected states, mobile drawer, and keyboard/focus behavior.
- Calendar filters, ongoing/upcoming events, dates, and Hebrew rendering.
- Location setup and editing, loading, empty results, unavailable sections, and retries.
- Preserved drafts and background chat completion during calendar navigation.
- Access when the chat allowance is exhausted.
- Refresh at the appropriate boundary and when settings change.
- Existing offline Dvar Torah behavior.

Run the narrowest relevant tests first, then the affected solution and frontend checks:

```powershell
dotnet test Library/AskARabbiLIB.slnx -c Release
dotnet test Backend/AskARabbiBackend.slnx -c Release
pnpm --dir Frontend verify
```

Follow the existing CI coverage requirements. Verify the rendered page at desktop and mobile sizes. Report exact commands and outcomes; do not claim unperformed checks passed.

## Acceptance criteria

- [ ] Learning & Tools contains This week’s Dvar Torah and Jewish Calendar.
- [ ] Conversations and New conversation appear below Learning & Tools.
- [ ] Conversations remain the default sign-in destination.
- [ ] Users can browse ongoing/upcoming holidays without sending a chat message.
- [ ] The default agenda covers 90 days and supports the other specified ranges and filters.
- [ ] Hebrew dates and weekly readings use the existing trusted calendar service.
- [ ] Local times use the selected current location and displayed calculation conventions.
- [ ] Missing location or provider data produces an accurate, usable partial page.
- [ ] Chat drafts and in-flight answers survive navigation.
- [ ] Calendar use generates no AI calls and consumes no chat allowance.
- [ ] Existing account settings, deletion, and offline Dvar Torah behavior continue working.
- [ ] Attribution, caching, automated tests, and desktop/mobile verification are complete.
- [ ] No new paid service or unapproved production dependency has been introduced.

## Later enhancements

Keep these outside the first release:

- Full month-grid view.
- Calendar export or subscriptions.
- Reminders and notifications.
- Full offline calendar access.

## Expected impact

- **Performance and cost:** cached, deterministic calendar access avoids model latency and token charges. Incremental cost is existing-host compute, network traffic, and small preference reads/writes.
- **Correctness:** explicit date/time types, selected location, reading-cycle settings, and boundary tests reduce incorrect dates and local times.
- **Maintainability:** reuse the existing application, calendar service, and storage while isolating the external provider.
- **Tradeoff:** holiday and timing coverage depends on a free, rate-limited external service, so partial rendering and cache behavior are required.

## Final implementation handoff

Report what was implemented, tests/checks run and their outcomes, any remaining limitations, and any additive API/configuration changes. Do not deploy as part of this task.

## References

- [Hebcal developer APIs, attribution, and limits](https://www.hebcal.com/home/developer-apis)
- [Jewish calendar API: categories and Israel/Diaspora schedules](https://www.hebcal.com/home/195/jewish-calendar-rest-api)
- [Supported location parameters](https://www.hebcal.com/home/4912/specifying-a-location-for-jewish-calendar-apis)
- [Shabbat times and timing preferences](https://www.hebcal.com/home/197/shabbat-times-rest-api)
- [Zmanim API for sunset information](https://www.hebcal.com/home/1663/zmanim-halachic-times-api)
