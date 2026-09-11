# Jewish Calendar

The authenticated Jewish Calendar is a deterministic learning destination, independent of chat generation and monthly allowance. It uses the existing Hebrew calendar library, a bounded Hebcal HTTP integration, and the existing account-settings storage. No production package, API key, paid service, infrastructure, or configuration key was added.

## Navigation and experience

The sidebar now groups **This week's Dvar Torah** and **Jewish Calendar** under **Learning & Tools**, followed by **Conversations**, **New conversation**, the scrolling chat list, and the profile menu. Conversations remain the default sign-in destination. The calendar component and its requests are lazy-loaded when opened. Selecting it does not spend tokens, discard drafts, cancel pending answers, or navigate back to a completed answer automatically.

The responsive page shows Today, collapsible This Shabbat (including local times), the current/next holiday, and Upcoming holidays, in that order. Location is edited in Personalization, not in a separate calendar form. The existing 90/30/365-day ranges and event filters remain available. Per-holiday Read about links are removed; required Hebcal/CC BY attribution remains.

The Dvar Torah button opens the publications generally. A publication keeps its own date and configured cycle; changing the calendar to Israel does not relabel a Diaspora publication.

## Authenticated API

| Endpoint | Result |
| --- | --- |
| `GET /api/calendar/overview?days=90` | Dates, selected preferences, weekly/festival reading, holiday agenda/highlight, local times, availability/freshness, and `nextRefreshAtUtc`. Only 30, 90, and 365 are accepted. |
| `GET /api/calendar/preferences` | `{ preferences, cities }`: saved choices or defaults, plus the reviewed city list. |
| `PUT /api/calendar/preferences` | Validates and atomically saves the authenticated owner's calendar object; returns normalized preferences. |

All three endpoints use existing authentication and `Cache-Control: no-store`. There is no caller-supplied owner ID. The existing user-data operation middleware also protects preference mutation during account deletion. The controller does not depend on generation or quota services. Existing chat and Dvar Torah contracts are unchanged.

Civil dates are ISO `yyyy-MM-dd` values, not timestamps. Precise times carry an offset and a named timezone. Each agenda entry includes an occurrence-specific `id`, stable `kind`, category, civil date range, beginning date/rule, explanation, source URL, ongoing flag, and individual occurrences. Availability distinguishes missing data from an empty filtered agenda. `today.solarData`, `holidays`, and `timing` identify applicable cached fallback data and its last successful fetch.

Invalid range/location/settings return a validation error. A transient ZIP-resolution failure returns 503 without saving anything. Overview provider failures instead return a usable partial overview with locally calculated dates/readings and section-specific availability messages.

## Preferences and storage

Current location is stored under `conversationSettings.personalization.currentLocation`, alongside a separate `birthLocation` for private birthday calculations. Both support a five-digit U.S. ZIP or reviewed city, with server-resolved timezone and coordinates. The calendar automatically enables local times, uses the location's candle-lighting default and 8.5-degree nightfall, and derives Israel/Diaspora from current location. There are no separate location, local-times checkbox, candle-offset, Havdalah, or reading-cycle controls on the calendar.

`conversationSettings.calendarPreferences` retains its optional legacy object and event filters for compatibility:

- `location`: used as a current-location fallback only when personalization has none. Never used as an inferred birthplace.
- `inIsrael`, `showLocalTimes`, `candleLightingMinutes`, `havdalah`, `havdalahMinutes`: accepted by the legacy preferences API, but effective overview settings use the shared current location and automatic conventions above.
- `majorHolidays`, `minorHolidays`, `fastDays`, `roshChodesh`: true by default.
- `specialShabbatot`, `modernObservances`: false by default; existing saved filters remain unchanged.

The initial searchable city list contains New York, Los Angeles, Chicago, Toronto, London, Paris, Jerusalem, Tel Aviv, Haifa, and Melbourne. ZIP lookup uses Hebcal's documented location resolution. Submitted display labels, timezones, and default offsets are not trusted: the server replaces them from the city catalog or provider. Current location is never inferred from birth details or chat content.

Mongo updates set only the owner-scoped calendar or personalization object and timestamp, preserving unrelated settings. Existing calendar locations are read into shared personalization as a fallback until the next profile save, without a bulk rewrite; missing birthplaces stay unset. Existing account deletion removes the containing settings document, including both locations and calendar preferences. Common schedules are not copied into account records. In-memory demo/test storage follows the same ownership/deletion behavior. See [Personalization](PERSONALIZATION.md#location-contract-and-compatibility) for signup, API, privacy, and rollout details.

## Dates, readings, and timing conventions

- Local civil date comes from the chosen timezone; absent a location, UTC is clearly identified.
- The existing `IHebrewCalendarService` converts the Hebrew date, advancing it at local sunset when that day's solar data is usable. Otherwise the UI explicitly identifies a daytime date, without inventing sunset times.
- The same library calculates weekly readings, including Israel/Diaspora differences and festival-displaced readings. Saturday evening advances to the following Shabbat after the selected Havdalah boundary. Without usable astronomy, the page does not guess a boundary.
- The integration corrected an existing one-based weekly-table index and enum/display-list offset in `HebrewCalendarService`. Regression fixtures include the April 2022 Israel/Diaspora split and the combined **Nitzavim–Vayeilech on September 5, 2026**. Existing published Dvar Torah records are not rewritten by this change.
- Standard candle lighting defaults to 18 minutes, Jerusalem to 40, and Haifa to 30. Havdalah uses the sun 8.5 degrees below the horizon. Calculations use sea level, not elevation; confirm your community's convention for practical observance.
- Candle-lighting, Havdalah, and fast timing events are retained from Hebcal, including consecutive festivals and Shabbat transitions. They are not all computed by subtracting an offset from sunset.
- Agenda beginning rules distinguish the previous sunset, the same evening (Chanukah candles), nightfall (Selichot), dawn (minor fasts), and civil-date observances. When exact ending data is unavailable, the affected civil-date entry remains visible through that day rather than inventing a precise end.
- Browser formatting keeps civil dates on their original day. Hebrew script is isolated with RTL direction inside the otherwise LTR interface.

## Provider, caching, and failure handling

`HebcalCalendarClient` calls only the documented HTTPS endpoints on `www.hebcal.com`, with redirects disabled. Parameters contain only calendar year/range, location identifiers, cycle, and timing conventions, never user identity, conversations, or birth dates/times. Personalization resolves birthplace coordinates using a current-date lookup; the private birth date is not sent. The production API needs outbound HTTPS access to that host; there is no new inbound route to an external service or cloud resource to provision.

- Holiday cache key: year and reading schedule/options.
- Local cache key: location, resolved timezone, date range, and timing conventions.
- Per-replica cache capacity: 128 entries; at most 32 distinct in-flight requests, with identical calls combined.
- Per-replica outbound limit: 60 requests per ten seconds, below Hebcal's documented 90 per ten seconds. Retry attempts count too. Provider `Retry-After` throttles further work.
- Each attempt has a six-second timeout and a two-megabyte JSON limit. One retry is allowed for server errors; other failures return explicit unavailable/cached results.
- Freshness respects `Cache-Control`, `Age`, and `Expires`, including `no-store`, `no-cache`, and `must-revalidate`. The fallback TTL is six hours, capped at 24 hours. Failure retries are bounded rather than continuously polled.
- If allowed by the provider's cache directives, data from the exact same key may be reused for up to seven days after its successful fetch. The UI labels it saved/stale and exposes the timestamp, including solar data used for date boundaries. Data from another location/convention is never substituted.
- Malformed JSON, invalid dates, mismatched locations/timezones, unavailable astronomy, throttling, and network errors have hermetic regression coverage. No estimated local times are fabricated.

The overview computes `nextRefreshAtUtc` from provider freshness, local midnight, sunset, dawn, nightfall, the chosen Saturday ending, and upcoming local timing events. The page refreshes at that boundary, after changed preferences, or on focus when expired. Offline mode pauses calendar requests and explains that current data requires connectivity; saved Dvar Torah remains available. Account-specific calendar responses are excluded from the public service-worker asset cache. Full offline calendar access is intentionally outside this release.

Hebcal requires attribution; the page visibly links to Hebcal and CC BY 4.0. Operational details are documented by the provider:

- [Developer APIs, attribution, and limits](https://www.hebcal.com/home/developer-apis)
- [Jewish calendar categories and reading schedules](https://www.hebcal.com/home/195/jewish-calendar-rest-api)
- [Supported location parameters](https://www.hebcal.com/home/4912/specifying-a-location-for-jewish-calendar-apis)
- [Shabbat times and timing conventions](https://www.hebcal.com/home/197/shabbat-times-rest-api)
- [Zmanim and solar calculations](https://www.hebcal.com/home/1663/zmanim-halachic-times-api)

## Verification

The deterministic tests use fixed clocks, fake providers/HTTP handlers, and isolated account stores. They do not call Hebcal, a model, or MongoDB. Mongo serialization and focused-update behavior are covered separately; production MongoDB was not mutated for testing.

Commands run from the repository root (existing dependencies were already restored; `--no-restore` avoids the sandbox-restricted user NuGet configuration):

```powershell
dotnet test Library/AskARabbiLIB.Tests/AskARabbiLIB.Tests.csproj -c Release --filter 'FullyQualifiedName~Calendar' --no-restore
dotnet test Backend/AskARabbi.Api.Tests/AskARabbi.Api.Tests.csproj -c Release --filter 'FullyQualifiedName~Calendar' --no-restore
pnpm --dir Frontend test:run src/features/calendar/CalendarPage.test.tsx
dotnet test Library/AskARabbiLIB.slnx -c Release --no-restore --collect:'XPlat Code Coverage' --logger 'trx;LogFileName=calendar-verification.trx' --results-directory Library/TestResults/CalendarVerification
dotnet test Backend/AskARabbiBackend.slnx -c Release --no-restore --logger 'trx;LogFilePrefix=calendar-verification' --results-directory Backend/TestResults/CalendarVerification
pnpm --dir Frontend verify
```

The existing coverage gate was also run using the bundled Python runtime:

```powershell
& 'C:\Users\aberf\.cache\codex-runtimes\codex-primary-runtime\dependencies\python\python.exe' scripts/check-cobertura-coverage.py Library/TestResults/CalendarVerification --minimum-branch-rate 80 --warn-only
```

Verification results: 53 targeted library-calendar tests, 62 targeted API-calendar tests, and 12 targeted calendar-page tests passed. The full suites passed 1,277 library tests, 196 API tests, 39 generator-job tests, and 201 frontend tests. Frontend lint, TypeScript compilation, and production bundling passed. Library branch coverage was 80.91%, above the existing 80% threshold. One intermediate API build was blocked by the running local test server's Windows executable lock; stopping that server and rerunning passed. No test failures remain from that condition.

A separate local Chrome smoke check used the production frontend build with an authenticated **in-memory local API**, real Hebcal data, and fixtures for unrelated chat/auth UI. Desktop 1435×1096 and mobile 390×844 checks covered initial lazy loading, range/category controls, expandable holidays, Jerusalem and ZIP settings, real candle-lighting events, drawer keyboard/focus behavior, preserved drafts, exhausted-quota access, offline/reconnect, a simulated API failure/retry, no API entries in the service-worker cache, and no chat-generation requests. It did not connect to or deploy production.

## Impact and limits

The calendar's cached, deterministic path avoids model latency and token charges. The page is a separate lazy-loaded chunk. Existing-host compute/network and small preference reads/writes still apply; this is not a claim of zero operating cost.

The free external provider remains a dependency. Rate limiting is per replica, so replicas sharing an outbound IP can collectively encounter the provider limit; 429 handling and applicable cached fallback remain necessary. The initial location list is intentionally reviewed and limited, with US ZIP entry as the alternative. A month grid, reminders, personal anniversaries, exports/subscriptions, and complete offline calendar support are deferred. No deployment or content regeneration is part of this implementation.
