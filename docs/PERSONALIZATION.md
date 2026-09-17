# Conversation personalization

Saved personalization is read again for every new answer, including replies in an existing conversation. Changing a preference does not rewrite previously saved answers. Current location and birthplace are now shared, server-resolved settings used by both calendar and chat; see the additive contract and migration notes below.

## What each setting controls

| Setting | Effect and boundary |
|---|---|
| Full name | The first name token is available for occasional natural address. An explicit preferred name can be supplied in Additional information. The full profile name is not included in the ordinary answer prompt. |
| Birth date and time | Entered in the birthplace's local clock. The ordinary prompt receives only a child, teenager, or adult audience group. Private calendar tools use the birth time and resolved birthplace to calculate sea-level sunset; an unknown boundary is explicitly qualified. |
| Birthplace | A five-digit U.S. ZIP or supported city, resolved to coordinates and an IANA time zone on save. Used privately for Hebrew birthdays and bar/bat mitzvah anniversaries, never for today's date. A one-time copy button can copy current location without linking future moves. |
| Current location | A five-digit U.S. ZIP or supported city. Supplies the calendar and current-date chat tools with local timezone/sunset, and selects Israel/Diaspora automatically. Explicit user-requested reading-cycle overrides remain possible in chat. |
| Conversation language | Explanations, generated chat titles, quotation roles, and follow-ups use this language independently of the question's language and earlier answers. Application-written navigation replies, answer transitions, and fixed validation/insufficient-evidence recovery replies are localized too. |
| Torah and source quotation language | Selects approved wording in that language for the same passage, independently of explanation language. English quotations stay English even when the explanation is Hebrew, Persian, or another language. A specifically requested comparison can include both editions. |
| Religious movement or practice | Supplies the user's self-described perspective when relevant. It is not a score for literacy, observance, or Jewishness, and does not establish a religious ruling. All 13 visible choices are preserved. |
| Heritage or community | Helps with relevant, source-supported community distinctions and ordinary transliteration. Ashkenazi context may use Teves/Shabbos; Sephardi or Mizrahi context may use Tevet/Shabbat. Explicit harmless wording preferences take precedence outside verbatim quotations. All 15 choices are preserved; mixed, converted, unsure, and undisclosed backgrounds do not imply a single custom. |
| Additional information | Learning goals, desired depth, unfamiliar terminology, accessibility needs, family context, and harmless style preferences reach drafting, repair, and auditing. They may shorten the default answer. They cannot override selected languages, evidence, privacy, safety, or citation rules. |

The ten languages are English, French, German, Hebrew, Italian, Persian, Polish, Russian, Spanish, and Yiddish. The separate account setting for opening source context remains a presentation preference; email-update consent is not a model instruction. Reviewed Background and Uncertainty can be citation-free, so a quotation-language preference never creates a source requirement or an invented translation. Fixed recovery replies use the response language, default to English for an absent or unsupported value, and are not model-personalized from the user's biography; see [answer reliability](ANSWER_RELIABILITY.md).

## Enforcement

### Location contract and compatibility

- `GET /api/conversation-settings/locations` returns the authenticated, reviewed city catalog (U.S. ZIP input is also supported).
- `PUT /api/conversation-settings/personalization` accepts optional `birthLocation` and `currentLocation`, each containing only `{ "kind": "zip" | "city", "id": "..." }`. Labels, timezones, and coordinates are resolved by the server. The response includes resolved metadata and the legacy `birthTimeZone` derived from the birthplace.
- New signup onboarding requires both locations. Personalization replaces the old standalone birth-timezone dropdown with the same shared controls. City-list failures leave ZIP entry available; failed location resolution returns 503 without changing the saved profile.
- The Mongo personalization object gains nullable `birthLocation` and `currentLocation`. Existing clients/documents remain readable; missing request locations preserve saved values. An existing calendar location is offered as current location until the profile is next saved. It is never assumed to be a birthplace. No bulk migration or production data rewrite is required.
- Only location identifiers are sent to Hebcal, never names, birth dates/times, or chat text. Birth sunset calculations use the existing local Zmanim library. Approximate ZIP/city coordinates and unknown/ambiguous birth times are not a substitute for checking near-sunset cases with a qualified rabbi.
- Current-date tools share the calendar's bounded solar-data provider/cache. No current location means an explicitly labeled UTC fallback; missing solar data means an explicitly labeled daytime Hebrew date. Ordinary model prompts exclude both locations and exact birth details.

`ConversationPersonalization` is the shared contract for drafting, repair, and the existing independent answer audit. User-provided context remains JSON data, not an interpolated system instruction. The auditor sees the current preferences as evaluation criteria, including response language, quotation language, safe style preferences, and understandable wording.

Quotation selection keeps topic relevance and source filters intact. For generic retrieval, bounded local canonical lookups prefer an approved translation of the same reference before the small evidence budget fills. They do not replace a source with a different passage. Existing canonical-reference and parashah lookup paths also prioritize quotation language.

After the audit, quotations still pass exact-source validation. An available preferred-language edition cannot silently be replaced by another edition. Extended inline quotations are checked too, so correct source-card metadata cannot disguise an invented translation in the answer body. Ordinary unquoted explanations remain in the response language. Short vocabulary labels are allowed.

If an approved requested-language edition is unavailable, the answer retains a verified available edition and adds a brief localized availability notice. The system must not manufacture a translation or refuse the whole explanation solely because that edition is missing. Calendar and technical evidence are not treated as Torah quotations.

Chat paragraphs and source quotations/context use independent native `dir="auto"` direction, while citation numbers remain left-to-right. This supports English explanations beside Hebrew quotations and the inverse on both desktop and mobile. Browser-native directionality follows the frontend testing/React guidance without adding a rendering dependency.

## Impact and rollout

- Correctness: preferences now reach both model stages, survive repairs, and take effect without starting a new chat. Source verification remains mandatory for source-backed statements; all generated claim kinds still receive independent review.
- Privacy: only a preferred-name token, broad audience group, background, and relevant user-supplied context enter the ordinary model prompt; exact birth details stay in server-side calendar context.
- Performance: no new model-validation stage was added. Prompts contain additional bounded instructions/context, and generic translation selection can add bounded local archive reads. Non-English or explicitly personalized date answers use the existing calendar-capable model path instead of returning a fixed English answer.
- Maintenance: one shared preference contract and centralized application-written language strings prevent drafting, auditing, and rendering from diverging. No new production dependencies or infrastructure changes are required.
- Compatibility: `GroundedAnswer` has additive `ResponseLanguage` and `QuotationLanguage` presentation properties. Location request/response fields and Mongo fields are additive; old profiles retain their birth timezone until their location setup is completed. Deploy the backend before or together with the frontend; old frontends remain supported.
- Deployment: rebuild and deploy the backend and frontend through the normal workflow. The API Docker build already copies `Prototype/Prompts`. This implementation does not modify production profiles or rewrite saved messages.

## Verification — 2026-09-05

Executed successfully from the repository root, except `pnpm verify`, which ran in `Frontend` with the bundled supported Node runtime:

```powershell
dotnet test Library/AskARabbiLIB.Tests/AskARabbiLIB.Tests.csproj --no-restore --collect:"XPlat Code Coverage" --results-directory Library/TestResults/PersonalizationBaseline --logger "console;verbosity=minimal"
dotnet test Library/AskARabbiLIB.Tests/AskARabbiLIB.Tests.csproj --no-restore --filter "FullyQualifiedName~GroundedAnswerCalendarToolTests|FullyQualifiedName~ConversationPersonalizationTests" --logger "console;verbosity=minimal"
dotnet test Library/AskARabbiLIB.Tests/AskARabbiLIB.Tests.csproj --no-restore --collect:"XPlat Code Coverage" --results-directory Library/TestResults/PersonalizationFinal --logger "console;verbosity=minimal"
dotnet test Backend/AskARabbi.Api.Tests/AskARabbi.Api.Tests.csproj --no-restore --logger "console;verbosity=minimal"
dotnet build Backend/AskARabbi.Api/AskARabbi.Api.csproj -c Release --no-restore
pnpm verify
git -c core.safecrlf=false diff --check
```

- Library baseline: 879 passed. Final: 1,170 passed, zero failed or skipped.
- API: 124 passed, zero failed or skipped. The real HTTP settings save/load/next-turn regression traverses all ten languages and time zones in the same conversation with fake external services.
- Frontend: 129 tests passed; lint, TypeScript, and production build passed. Backend Release build passed with zero warnings or errors.
- The 100 response/quotation language combinations are covered in deterministic drafting and audit contract tests. These establish propagation/selection rules, not native-language fluency.
- All visible movement and heritage choices, audience boundaries, minimal profile disclosure, source filters, translation fallback, repair behavior, and personalized calendar routing have regression coverage.
- Chrome checks used actual local message/source-reader components on desktop and a 430-pixel mobile viewport: Hebrew sources were RTL, English sources LTR, no sheet overflow, and no console warnings/errors. Production settings were inspected without saving changes; temporary QA tabs/server were closed afterward.
- Bounded synthetic checks used the existing Azure GPT-5-mini deployment at medium reasoning, priority service, the actual prompts, and the approved local corpus. Final samples succeeded in each of the ten response languages, including English/Hebrew quotation separation and an unavailable Spanish-edition fallback. Early failures led to fixes for inline quotation parsing and shorter, plainer Yiddish responses. Some samples required the existing repair pass. These are smoke tests, not a guarantee of every possible answer or a native-speaker certification; Yiddish in particular remains a language to include in ongoing quality review.
- Additional live calendar checks passed without repair: a Spanish current-date answer and an Ashkenazi profile whose explicit wording preference requested Tevet. The latter used Tevet in the explanation while preserving Teves in the exact calculated evidence. Custom calendar introductions are no longer overwritten with fixed English wording.

Library coverage increased from 83.51% to 83.82% lines and 79.55% to 80.29% branches. The new preference contract is 100% line / 95.71% branch covered; application language strings are 100% / 100%; the renderer is 100% / 95%. Coverage-guided priorities were translation selection (`EvidencePacketBuilder`, class-only final 61.95% / 47.22%), canonical language filtering (`ConversationReferenceGuide`, 75.75% / 75%), and answer orchestration (`GroundedAnswerService`, 86.08% / 78.53%). Unrelated pre-existing branches were not expanded into this task.

Tests are maintained in `ConversationPersonalizationTests`, `GroundedAnswerServiceTests`, `AIGroundedClaimEvidenceValidatorTests`, `GroundedAnswerTextRendererTests`, `GroundedAnswerCalendarToolTests`, `ConversationRetrievalRegressionTests`, and the API's `LocalDevelopmentIntegrationTests`. Frontend regressions cover `AssistantMessage`, `UserMessage`, `SourceReader`, and the settings success message in `App.test.tsx`.
