# AskARabbiPrototype

`AskARabbiPrototype` is a thin .NET 10 Spectre.Console host for `AskARabbiLIB`. Its namespace, project, assembly, and independent solution are all named `AskARabbiPrototype`.

The full implemented question-to-answer pipeline is documented in [`docs/CHAT_WORKFLOW.md`](../docs/CHAT_WORKFLOW.md).

The console owns menus, profile entry, prompt-file loading, formatting, configuration discovery, and exit codes. Manifest models, file safety, search/ranking, SQLite indexing/retrieval, AI access, optional Key Vault access, evidence construction, validation, and session models all remain in `AskARabbiLIB`.

## Solution boundary

```text
Prototype/
├── AskARabbiPrototype.slnx
├── Profiles/
│   ├── README.md
│   └── profile.example.json
├── Prompts/
│   ├── README.md
│   ├── current-question.json
│   ├── grounded-answer.schema.json
│   ├── grounded-support-validation.schema.json
│   ├── grounded-support-validation.txt
│   ├── interpretive-notice.txt
│   ├── prior-assistant-context.txt
│   ├── prior-user-context.txt
│   ├── system-behavior.txt
│   └── validation-repair.txt
└── AskARabbiPrototype/
    ├── AIChatConsole.cs
    ├── SourceSearchConsole.cs
    ├── SegmentIndexConsole.cs
    ├── OneShotCommandExecutor.cs
    ├── ConsolePresentation.cs
    ├── ApplicationStateLoader.cs
    ├── ConsoleApplication.cs
    └── AskARabbiPrototype.csproj
```

There is no prototype test project. All reusable behavior and automated MSTest coverage live in `Library/AskARabbiLIB.Tests` and are built from `Library/AskARabbiLIB.slnx`.

## Interactive console

Run from the repository root:

```powershell
dotnet run --project Prototype/AskARabbiPrototype --
```

The top-level menu contains:

- **Start AI chat:** the first and initially selected option. Select a local JSON profile or enter context, then ask questions and follow-ups directly in a continuous `You` / `AskARabbi AI` conversation. All approved logical sources begin enabled; use `/sources` to review or change them.
- **Search the source library:** existing fast manifest keyword/facet search, ranked results, raw text, full Sefaria metadata, original JSON, normalized Markdown, and checksum verification.
- **Statistics:** manifest memory/timing information plus segment-index validity and size.
- **Exit:** clears any process-memory AI chat session.

Source Search does not require Azure configuration or the SQLite segment index. AI Chat rejects a missing or stale index and offers an atomic rebuild with progress. It then requires the AI settings below.

Successful chat answers use conversational BLUF: a natural acknowledgment when appropriate, the direct answer first, and only the explanation needed after it. The opening answer is bold and citation numbers are cyan beside the claims they support. An exact quotation already present in a claim is highlighted in yellow in that paragraph and is not repeated below it; a supporting quotation absent from the prose appears once with a compact cyan source line. The console does not automatically dump the surrounding segment or repeat a bibliography; `/evidence` exposes the complete retrieved packet whenever the reader wants to inspect the wider context. Tables remain available in Source Search where tabular results are useful. Every successful answer ends with the compact italic application-controlled text in [`Prompts/interpretive-notice.txt`](Prompts/interpretive-notice.txt), which can be edited without recompiling the library.

AI Chat conversation history exists only in the current process. Questions, prompts, answers, and evidence are not written to files, application logs, a persistence interface, or chat history. Clearing the session, leaving AI Chat, or exiting drops the references. A profile entered without saving is also process-only. A profile explicitly saved under `Prototype/Profiles` remains on disk for future chats; personal profile JSON files are ignored by Git. Azure requests set `store=false`; provider-side handling still follows the configured Azure service agreement and must not be described as zero retention without separate verification.

## User profiles

Interactive AI Chat requires context before the first question. The user may select a valid JSON profile from [`Profiles`](Profiles), enter a custom profile for the current process, or enter and explicitly save a custom profile locally. The profile contains:

- Required name.
- Required date of birth, used to calculate age.
- Optional birth time and IANA birth time zone for local Hebrew-calendar calculations.
- Optional short bio.
- Optional self-described religious background or movement.
- Required self-described Jewish heritage or community background.

The tracked [`profile.example.json`](Profiles/profile.example.json) documents the strict camel-case schema. Unknown properties, missing required fields, future dates, ages over 130, and overlong values are rejected before retrieval or an AI call. The interactive date prompt accepts `MM/DD/YYYY`; JSON uses ISO `YYYY-MM-DD`.

The normal writing prompt contains only calculated age, name, optional bio, optional religious background, and Jewish heritage—not the exact birth date, time, or time zone. When a calendar question requires the saved date, the model omits that function argument and trusted process-local code performs the calculation; only the derived result and its assumptions return as citable evidence. `GroundedAnswerService` marks all ordinary profile fields as untrusted personalization context, does not add them to retrieval keywords, and instructs the model not to stereotype, infer observance, or claim profile-specific rules without supporting textual evidence.

Inside the chat, `/profile` displays the active context. `/clear` removes conversation content but keeps the selected profile active until the user leaves the chat.

## Configuration

Configuration loads in this order: the ignored root `appsettings.json`, .NET User Secrets, then environment variables. JSON contains only the non-sensitive Azure OpenAI endpoint, deployment name, and unused Key Vault endpoint. Store the Azure OpenAI resource key in User Secrets; no `appsettings*.json` file contains or documents a secret field. Leave `KeyVault:Endpoint` empty because the current prototype does not consume Key Vault.

```powershell
Copy-Item appsettings.example.json appsettings.json
dotnet user-secrets --project Prototype/AskARabbiPrototype set "AI:APIKey" "your-resource-api-key"
```

The endpoint and deployment name may remain in the ignored JSON. Environment variables are an alternative and have the highest precedence:

```powershell
$env:AI__ProjectEndpoint = "https://your-resource.openai.azure.com"
$env:AI__ModelName = "your-deployment-name"
$env:AI__APIKey = "your-resource-api-key"
```

`AI:ProjectEndpoint`, `AI:ModelName`, and `AI:APIKey` are validated only when AI Chat or the `ask` command is used. `ProjectEndpoint` must be an absolute HTTPS URL. User Secrets are stored in the current Windows user profile outside the repository and are never copied to build or publish output. The unused `KeyVault` section remains in the example only to make its current status explicit; reusable Key Vault support lives in the library and is not initialized by this host.

The prototype uses the library's 2,000-output-token ceiling, a 120-second timeout, medium reasoning effort, strict JSON Schema output, `store=false`, and the same three bounded local calendar functions as production. The prompts target roughly 180–325 words of explanatory prose for ordinary questions, leaving room for required exact quotations without encouraging report-length responses.

If Azure returns 401, verify that `AI:APIKey` is a current key from the same resource named by `AI:ProjectEndpoint`. If Azure returns 403, verify that local/key authentication is enabled on the resource and that `AI:ModelName` is a deployment on that resource.

Inside AI Chat, plain text is always treated as the next question. Optional commands are:

- `/sources` — turn any logical source on or off for subsequent answers and set optional language/category filters.
- `/profile` — display the personalization context active for the chat.
- `/evidence` — inspect the exact packet used for the last response.
- `/trace` — inspect retrieval, model, token, and validation diagnostics.
- `/clear` — remove the in-memory conversation.
- `/back` — clear the session and return to the main menu.

## Prompt files

Every model-facing instruction, the strict response schema, and the application-controlled closing notice live in the tracked [`Prompts`](Prompts) directory. They are copied into build and publish output, loaded only when AI Chat or `ask` is used, and validated before the first model request. The library receives them as a `GroundedPromptSet`; it no longer contains hidden default prompt or notice text.

See the [prompt catalog](Prompts/README.md) for the purpose of each file and the placeholders that must remain intact. Prompt files contain behavior and formatting instructions, not credentials, so they should remain committed and reviewable.

## Segment index lifecycle

The generated index is `Data/NormalizedData/Sefaria/Metadata/segment-search-v3.sqlite`. It contains 476,116 citation-addressable segments for the current corpus, remains disk-backed, is ignored by Git, and can be reproduced from normalized Markdown. Supplemental work keys and usage limitations survive retrieval and are supplied to the AI with the passage. Source names in grounded answers link to the trusted original-edition URL; CC BY and CC BY-SA links also display the exact retained license label.

```powershell
dotnet run --project Prototype/AskARabbiPrototype -- index build
dotnet run --project Prototype/AskARabbiPrototype -- index verify
dotnet run --project Prototype/AskARabbiPrototype -- index stats --format json
```

The builder verifies each Markdown checksum and manifest segment range, records schema/corpus fingerprints inside SQLite, verifies the completed file, and atomically replaces an older index. `verify` and `stats` return a nonzero exit code for a missing, corrupt, or stale index.

## One-shot search and AI

Existing document-search commands remain compatible:

```powershell
dotnet run --project Prototype/AskARabbiPrototype -- search "Shabbat" --language English --collection Talmud --category "Seder Moed" --limit 10 --format json
dotnet run --project Prototype/AskARabbiPrototype -- facets --format json
dotnet run --project Prototype/AskARabbiPrototype -- stats --format json
```

Search supports `--language`, `--collection`, `--category`, `--title`, `--version`, `--license`, `--match all|any`, `--min-segments`, `--max-segments`, `--skip`, `--limit 1-200`, and `--format table|json`.

Ask one non-persistent grounded question with optional repeated source filters:

```powershell
dotnet run --project Prototype/AskARabbiPrototype -- ask "What do these texts say about lighting before Shabbat?" --collection Talmud --language English
dotnet run --project Prototype/AskARabbiPrototype -- ask "מה אומר המקור?" --language Hebrew --format json
dotnet run --project Prototype/AskARabbiPrototype -- ask "Why do customs differ?" --profile amitai-erfanian.json
dotnet run --project Prototype/AskARabbiPrototype -- ask "What does the Zohar say about light?" --source work:zohar
```

`--source` may be repeated. Current keys are `collection:Torah`, `collection:Tanakh`, `collection:Mishnah`, `collection:Talmud`, `work:rif`, `work:mishneh_torah`, `work:shulchan_arukh_with_rema`, `work:zohar`, `work:zohar_chadash`, and `work:mesillat_yesharim`. All sources are enabled when an interactive chat begins; `/sources` opens named checkboxes with edition, passage, and language counts.

`--profile` accepts a file name from `Prototype/Profiles`; it intentionally does not accept an arbitrary path. It is optional for one-shot automation so existing scripts remain compatible.

`ask` fails closed with a nonzero exit code if evidence is absent or tangential, the index is stale, Azure is unavailable, or citation, quotation, relevance, or claim-support validation fails after one repair. A successful answer normally uses one model request to draft the answer and a second structured request to audit every claim against its cited passages. Console output includes the answer, exact evidence packet, and trace without quote panels or a repeated source bibliography. JSON output returns the typed result—including the current editable interpretive notice—for agents and scripts.

Use global overrides when needed:

```text
--manifest <path> --repository-root <path> --index <path>
```

## Build

Build the standalone prototype solution:

```powershell
dotnet build Prototype/AskARabbiPrototype.slnx -c Release
```

Build and test the reusable library separately:

```powershell
dotnet build Library/AskARabbiLIB.slnx -c Release
dotnet test Library/AskARabbiLIB.slnx -c Release --no-build
```
