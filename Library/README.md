# AskARabbiLIB

`AskARabbiLIB` is the reusable .NET 10 library for manifest search, checksum-verified source access, segment indexing/retrieval, Azure AI access, bounded local AI tools, Hebrew-calendar calculations, optional Key Vault access, fail-closed grounded-answer validation, production account/conversation rules, and Azure Cosmos DB for MongoDB persistence. Its namespace, project, assembly, and solution are all named `AskARabbiLIB`.

The library has no Spectre.Console dependency. `AskARabbiPrototype` is only a host; future APIs, agents, workers, and evaluation tools can reuse these contracts directly.

## Solution layout

```text
Library/
├── AskARabbiLIB.slnx
├── AskARabbiLIB/
│   ├── AI/Tools/
│   ├── Accounts/
│   ├── Calendar/
│   ├── Conversations/
│   ├── ConversationSettings/
│   ├── Files/
│   ├── Grounding/
│   ├── Models/
│   ├── Profiles/
│   ├── Persistence/Mongo/
│   ├── Retrieval/
│   ├── Search/
│   ├── Secrets/
│   └── Usage/
└── AskARabbiLIB.Tests/
    └── AskARabbiLIB.Tests.csproj
```

All automated tests live in `AskARabbiLIB.Tests`. The prototype has no test project, and its solution does not include the library tests.

## Data and index lifecycle

1. `ManifestLoader` loads the complete schema 1.3 document manifest into memory. It rejects older schemas, unknown JSON fields, non-UTC generation timestamps, any status other than `permissive`, duplicate paths/IDs, malformed checksums, mismatched typed license terms, invalid source URLs, and any `documentId` that does not match `sefaria:{rawSha256}`.
2. `ManifestSearchIndex` builds immutable in-memory indexes for fast document/facet discovery. Existing keyword, language, collection, category, title, version, license, segment-range, and pagination APIs remain compatible.
3. `SefariaDocumentFileLoader` lazily resolves repository-relative paths, prevents traversal, verifies checksums, and returns the selected raw JSON or normalized Markdown.
4. `SourceIndexBuilder` parses canonical `##` Markdown headings into exact segments, validates each count/reference range, builds a contentless SQLite FTS5 index in one transaction, records a corpus fingerprint, verifies it, and atomically replaces the old index.
5. `SqliteSourceRetriever` keeps the generated segment index on disk. It supports exact canonical references, tiered BM25, Hebrew/Unicode normalization, language/collection/category filters, logical-source selection, and adjacent context. `DocumentSourceCatalog` partitions the manifest into broad core collections and named supplemental works, allowing any combination to be searched with one OR-based source filter. `RetrievalQueryPlanner` removes conversational filler, prioritizes reviewed concepts, maps equivalents such as `Saturday`/`Shabbat`, and retains automation, technology, and business as separate concepts. When it recognizes a topic anchor, every fallback remains paired with that anchor; questions without one retain the full-concept, paired-concept, and broad tiers. Small deterministic vocabulary families bridge wording differences without allowing model-authored search terms.
6. `AzureOpenAIVectorStoreCorpusPublisher` creates deterministic managed-search files capped at 60,000 UTF-8 bytes, preserves stable document/segment IDs across multi-file logical documents, stamps corpus and schema fingerprints, and verifies both logical-document and provider-file counts. `AzureOpenAIVectorStoreRetriever` forces a Responses API `file_search` call, ignores provider-authored prose, and reconstructs only complete records whose document IDs resolve through the bundled trusted manifest. It does not create an Assistant or let provider citation metadata control an answer.

The default generated index is `Data/NormalizedData/Sefaria/Metadata/segment-search-v3.sqlite`. It is reproducible and ignored by Git. Schema v3 persists optional supplemental `workKey` and `usageNote` fields, supports work-level retrieval filters, and returns those limitations with model evidence. Missing, corrupt, wrong-schema, wrong-license, or stale-fingerprint indexes are rejected.

## Grounded answer flow

`GroundedAnswerService` implements the provider-neutral orchestration contract:

1. Validate the question, source filters, and optional typed profile before retrieval.
2. Retrieve at most 50 local candidates using the current question plus limited recent user context.
3. Apply a deterministic topic-and-support adequacy gate. A normal textual question returns `InsufficientEvidence` without calling a model when retrieval is empty or merely tangential. A recognized Hebrew-calendar question may continue with only the explicitly registered local calendar tools available.
4. Build at most 24 evidence items and 48,000 text characters by default, with document diversity, up to six neighboring segments on each side, canonical-reference translation pairing, and explicit excerpt labels. The nine-segment per-document cap normally yields the cited segment, up to six preceding segments, and two following segments when all are available.
5. Send only the bounded packet, recent process-memory context, and a minimized profile context through `IAIEngine`. The normal prompt contains calculated age rather than date of birth. For a relevant calendar request, the model may call one of three bounded local functions; omitting the birth-date argument lets trusted server code use the saved date privately without putting it in the prompt or tool output.
6. Convert every successful tool result into request-local calculated evidence with an opaque evidence ID and exact text. Calendar calculations must be cited and quoted like corpus evidence, but remain visibly identified as calculations rather than source texts or religious rulings.
7. Require strict structured claims, evidence IDs, attributed perspectives, disagreements, mandatory quotations for every cited evidence ID, quotation roles, limitations, a clarifying question, and a human-guidance flag.
8. Reject unknown evidence IDs, uncited statements, unquoted cited sources, and quotations that are not exact substrings of the trusted source segment or calculated result. A claimed interpretation chain therefore needs exact passages for both the later view and its earlier textual basis.
9. Run an independent structured audit of every claim and disagreement. The audit checks relevance to the question and entailment from the cited passages or deterministic result, and rejects unrelated legal analogies or unsupported modern rulings even when their quotations are authentic.
10. Materialize every title, reference, edition, language, license, file path, and original-source URL from trusted application objects, never model output. Attribution-required sources render as trusted clickable links, and redirected output exposes the equivalent Markdown link.
11. Permit one same-evidence repair after either validation layer; fail visibly if validation still fails. Materialize the host-supplied, validated interpretive notice without asking the provider to generate or modify it.

Retrieved text and profile fields are serialized inside explicit untrusted-data boundaries. Profile fields do not enter retrieval and cannot count as evidence. The behavior contract is educational, pluralistic, non-shaming, and non-prescriptive; it forbids stereotypes, assumed observance, personalized *psak*, and calls for qualified human guidance when personal or high-consequence context matters.

The host supplies a validated `GroundedPromptSet` to `GroundedAnswerService`. In the prototype, every instruction template, the strict JSON schema, and the application-controlled closing notice are loaded from the tracked [`Prototype/Prompts`](../Prototype/Prompts) catalog. This keeps behavior and notice changes visible and reviewable while the reusable library remains independent of any particular filesystem layout.

## Primary contracts

- AI: `IAIEngine`, `AIEngineOptions`, `AIMessage`, `AIEngineResult<T>`, `AIUsage`, `AzureOpenAIEngine`.
- AI tools: `AIToolAttribute`, `AIToolParameterAttribute`, `IAIToolRegistry`, `AIToolRegistry`, `AIToolExecutionSession`, and `CalendarAITools`.
- Calendar: `IHebrewCalendarService`, `HebrewCalendarService`, `HebrewDateInfo`, and `WeeklyParashahInfo`.
- Retrieval: `SourceSegment`, `SourceRetrievalQuery`, `SourceRetrievalHit`, `ISourceRetriever`, `SqliteSourceRetriever`, `SourceIndexBuilder`, `AzureOpenAIVectorStoreCorpusPublisher`, `AzureOpenAIVectorStoreClient`, and `AzureOpenAIVectorStoreRetriever`.
- Grounding: `GroundedQuestion`, `EvidencePacket`, `GroundedAnswer`, `GroundedClaim`, `GroundedQuotation`, `SourceCitation`, `GroundedAnswerResult`, `GroundedPromptSet`, `IGroundedAnswerService`, `InMemoryGroundedSession`.
- Profiles: `UserProfile`, `UserProfileJsonSerializer`.
- Secrets: `ISecretStore`, `AzureKeyVaultSecretStore`.
- Accounts: `ExternalUserIdentity`, `UserAccount`, `IUserAccountStore`.
- Conversations: `Conversation`, `ConversationMessage`, `ConversationSourceCitation`, `ConversationSummary`, `ConversationService`, `ConversationSourceCatalog`, `IConversationStore`.
- Personalization and usage: `PersonalizationSettings`, `ConversationSettingsService`, `MonthlyUsageService`, `BillingPeriodUsage`, and their store contracts.
- Persistence: `MongoDatabaseOptions`, owner-scoped MongoDB store implementations, required-index initialization, invariant temporal serializers, and an explicit unconfigured-store failure.

`AzureOpenAIEngine` accepts either an `ApiKeyCredential` or an Entra `TokenCredential` (defaulting to `DefaultAzureCredential`). It uses a 120-second default timeout, 2,000 output tokens, medium reasoning, explicit per-request model selection, `store=false`, strict JSON Schema Structured Outputs, cancellation propagation, bounded retries, and typed provider failures. It exposes response ID, returned model, token usage, latency, and attempts without retaining prompt or response bodies. The local prototype chooses the API-key constructor.

`AzureKeyVaultSecretStore` performs no network work in its constructor. It loads only explicitly requested secrets, supports cancellation and forced refresh, coalesces concurrent misses, starts its 15-minute cache window after a successful provider response, and clears cached values during race-safe disposal. It is optional and is not used to load the prototype's Azure OpenAI API key.

## Host boundary

The library deliberately owns every reusable or safety-critical operation. The prototype owns only interaction concerns. Production hosts compose `AzureOpenAIVectorStoreRetriever`, `IGroundedAnswerService`, and `IAIEngine` through dependency injection, while the prototype continues to compose `SqliteSourceRetriever`. Prompt content remains host-supplied through `GroundedPromptSet`, while citation metadata and the interpretive notice are materialized by trusted code after validation.

Production code is organized with one primary type per file and provider-specific dependencies behind narrow internal or public contracts. Public models retain their current namespaces and serialization names; this cleanup did not change the manifest schema or console configuration keys.

The production MongoDB boundary keeps account data, conversation metadata, messages, personalization, and monthly counters in separate collections. Conversation summaries project only navigation fields; loading one conversation joins its owner-scoped metadata with ordered message records. Assistant messages may embed bounded `ConversationSourceCitation` records containing trusted quotations, presented context, canonical and attribution URLs, edition, language, license, and excerpt state; legacy messages without that additive field remain valid. Client-generated message IDs make retries idempotent, and every mutation requires both the immutable local user ID and resource ID. `TimeProvider` drives persisted application timestamps and calendar-month usage boundaries so business tests do not depend on system time.

## Dependencies

The library pins:

- `Microsoft.Data.Sqlite` 10.0.10 for disk-backed FTS5 instead of loading the segment corpus into managed memory.
- `Azure.AI.OpenAI` 2.9.0-beta.1 for the Responses API surface; it is isolated behind `IAIEngine` so a stable replacement is localized.
- `Azure.Identity` 1.21.0 for the Entra-capable engine overload and optional Key Vault adapter.
- `Azure.Security.KeyVault.Secrets` 4.11.0 for the optional secret adapter.
- `SQLitePCLRaw.bundle_e_sqlite3` 2.1.13 as a direct security override because the bundle transitively selected by `Microsoft.Data.Sqlite` 10.0.10 is affected by a high-severity native SQLite advisory.
- `MongoDB.Driver` 3.11.0 for the supported Mongo wire protocol, atomic updates, projections, and indexes used by Azure Cosmos DB for MongoDB. A custom HTTP/data-access layer was rejected because it would duplicate protocol, TLS, serialization, retry, and compatibility responsibilities while increasing correctness and security risk.
- `Zmanim` 1.5.0 for its established weekly parashah tables and Hebrew date formatting. .NET's built-in `HebrewCalendar` performs the numeric date conversion and independently classifies Hebrew year shapes; the wrapper uses the pinned Zmanim table for Diaspora/Israel reading schedules and corrects a known older-year classification path covered by regression tests. The package adds one approximately 75 KB managed assembly and no .NET Standard 2.0 transitive package dependencies. Its LGPL license and upstream details are recorded in [`THIRD_PARTY_NOTICES.md`](../THIRD_PARTY_NOTICES.md).

Alternatives considered were whole-corpus in-memory search, Azure AI Search Basic, Cosmos vector search, Foundry Agents, reflection-discovered tools, and model-controlled web/file search. Azure OpenAI managed vector storage was selected for the first production release because it avoids a continuously billed search service and provides semantic/keyword file search through the Responses API. The tradeoff is usage-based storage/search cost, one small retrieval-model call before answer generation, provider-managed chunking, preview/change risk, and less ranking control than a dedicated hybrid index. The provider remains isolated behind `ISourceRetriever`, so Azure AI Search can replace it later without changing grounding or API contracts.

The implementation adapts selected ClearVowAI patterns—provider interfaces, configuration validation, text prompt construction, structured results, credential-specific client creation, retries, diagnostics, Key Vault access, and invariant BSON temporal serialization—without depending on that external directory. The audited ClearVowAI services did not contain a reusable Mongo repository, so AskRabbi implements narrow owner-scoped stores around its own domain contracts. It does not port the Foundry Agent engine, model-controlled hosted retrieval, broad assembly scanning, a service locator, web/file/image tools, SQL/Redis services, cryptographic key rotation, Newtonsoft.Json, NJsonSchema, Tiktoken, or unrelated setup helpers. AskRabbi instead scans only explicitly supplied provider instances for its narrow tool attributes, validates their schemas at startup, bounds each request to four local calls, and turns successful calculations into application-owned evidence. Its managed vector-store adapter separately uses a forced, single-purpose file-search call whose output is parsed and filtered by application-owned grounding and validation.

## API migration

Manifest schema 1.3 adds required `ManifestDocument.LicenseCategory`, `RequiresAttribution`, `RequiresShareAlike`, and `AttributionUrl`. `ManifestDocument.WorkKey` and `UsageNote` are additive and optional for core texts; curated supplemental records provide both. Existing consumers must regenerate `document-manifest.json`, populate the required license properties in object initializers, rebuild the v3 segment index, and pass `SourceSegment.LicenseCategory` when constructing segments directly. `SourceSegment.WorkKey`, `SourceSegment.UsageNote`, `SourceRetrievalQuery.WorkKeys`, and `GroundedQuestion.WorkKeys` are additive and optional. `DocumentSourceCatalog`, `SourceRetrievalQuery.SourceKeys`, and `GroundedQuestion.SourceKeys` are additive APIs; existing callers that omit source keys continue to search all approved sources. `SourceSegment.SourceUrl` and `SourceCitation.SourceUrl` identify the original attribution source; the Sefaria export artifact remains in `ManifestDocument.SourceUrl`. `GroundedAnswerService` requires a validated `GroundedPromptSet`; hosts must load or construct the prompt set and pass it as the third constructor argument. `GroundedPromptSet.InterpretiveNotice` and `GroundedAnswer.InterpretiveNotice` are required; custom hosts must supply application-controlled closing text. Custom prompt-set initializers must now also provide `SupportValidationPrompt` and `SupportValidationJsonSchema`; the prototype loads them from the two `grounded-support-validation` prompt files. `GroundedQuestion.UserProfile` remains additive and optional. `GroundedAnswerOptions` defaults to 24 segments, 48,000 characters, nine segments per document, and a six-segment context radius.

The managed retrieval APIs are additive. Provider-returned excerpts add `IsExcerpt`, `OriginalSegmentId`, `ExcerptStart`, and `OriginalCharacterCount` to `SourceSegment`; existing local segments retain their prior behavior through defaults. `AzureOpenAIVectorStoreCorpusFormatter.FormatParts` is the publication path for documents that require multiple bounded files, while `Format` remains available for a single-file artifact. Publication results and progress now expose provider-file counts separately from logical-document counts. `GroundedQuestion.ConversationLanguage` and `QuotationLanguage` are optional presentation preferences and do not count as source evidence. New production hosts must bundle the matching validated document manifest and configure an immutable vector-store ID and corpus fingerprint; stale or mismatched stores are rejected before search. The new `IAIEngine.GenerateStructuredAsync` tool-session overload is additive and has a default implementation that preserves existing engines; hosts that want callable tools must register an `IAIToolRegistry` and use an engine implementation that forwards the session. `UserProfile.TimeOfBirth` and `BirthTimeZone` are additive optional fields used only by trusted local calculations.

## Build and test

Run from the repository root:

```powershell
dotnet build Library/AskARabbiLIB.slnx -c Release
dotnet test Library/AskARabbiLIB.slnx -c Release --no-build
```

Tests are MSTest and use in-memory SQLite, fake AI transports, fake Key Vault clients, controlled time providers, fake application stores, and fake document readers. BSON serializer compatibility is tested against the pinned driver. Normal verification performs no live Azure, MongoDB, WorkOS, system-time, or filesystem corpus access. Live provider calls are manual smoke tests only.

Collect coverage with:

```powershell
dotnet test Library/AskARabbiLIB.slnx -c Release --no-build --collect:"XPlat Code Coverage" --results-directory Library/TestResults
```

CI parses the generated Cobertura report with `scripts/check-cobertura-coverage.py` and fails below 80% branch coverage. The threshold guards reusable library behavior; the intentionally thin prototype has no separate test project.
