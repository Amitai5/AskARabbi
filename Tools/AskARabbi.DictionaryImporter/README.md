# Local BDB dictionary

This imports **original Brown–Driver–Briggs articles**, not AI-written answers or a curated FAQ. The backend can search these articles and read additional original context through its existing attribute-based AI capability registry. Other dictionaries can later implement the same `ILexiconStore` contract with separate dictionary IDs, editions, language scopes, and reviewed importers.

## Edition and provenance

- [BibleAquifer BDB](https://github.com/BibleAquifer/BDBHebrewLexicon/tree/4fa2054acd751af9b1bc16c5102a542b1261f5d1), immutable commit `4fa2054acd751af9b1bc16c5102a542b1261f5d1`.
- The pinned README and `eng/metadata.json` declare **CC0 1.0** for this digital edition. See the repository's [third-party notice](../../THIRD_PARTY_NOTICES.md).
- 23 original JSON files, `000.content.json` through `022.content.json`; **3,005 articles including abbreviations**, not 3,005 distinct Hebrew words. Some articles group several subentries and senses.
- Downloaded JSON: 42,550,445 bytes. Deterministic plain-text articles: 5,622,268 UTF-16 characters, longest article 59,635 characters.
- Approved source fingerprint: `1f4847c7ab99e79e3f422e797c4a10f5ba2c17ea5807656d500933bc76fc1858`. SHA-256 covers each filename plus newline followed by the exact file bytes, in numeric file order. Validation finishes before connecting to MongoDB for import.

Each entry retains its original pointed headword, article ID, readable text, immutable source URL, source file checksum, edition, language scope, and license. HTML, script, and style markup is not exposed to the model or browser. Separate homograph articles retain separate IDs. The data is not silently corrected, translated, or supplemented by a model; the upstream historical/digital edition can contain errors and obsolete interpretations.

## Storage and indexed search

`MongoDB:LexiconCollectionName` / `MongoDB__LexiconCollectionName` defaults to **`lexiconEntries`** in the existing application database. This is shared reference data, not user data, and is independent of account/chat deletion. API constructors only obtain a collection handle; the explicit import creates the collection and indexes.

- `_id` is a unique deterministic `dictionary:revision:articleId` key. The same headword can have several distinct articles.
- Scalar `dictionaryId` and `revision` indexes isolate editions. A multikey `lookupKeys` index serves exact normalized headword, Hebrew body-word, English body-word, and paragraph-opening lookups.
- Vowels and cantillation are ignored **for search only**; source text is preserved. Different consonants are not collapsed and no root or transliteration is guessed.
- Exact headword results take precedence and do not get padded with unrelated mentions. Otherwise original paragraph openings are ranked before other body matches. This is a structural ranking heuristic, not an assertion that every paragraph opening is a definition.
- Short multiword queries require all normalized terms. English search is lexical, not semantic: try alternate terms rather than expecting an English question or a transliteration to match automatically. No database regex scan or vector service is used.
- Indexes are deliberately single-field: [Cosmos DB for MongoDB does not support text indexes or compound indexes on array fields](https://learn.microsoft.com/en-us/azure/cosmos-db/mongodb/indexing).

Import writes 100-article upsert batches and publishes an edition manifest **only after every batch succeeds**. A reserved content fingerprint prevents different imports from mixing data under one revision. Interrupted imports stay unavailable and can be rerun. Repeating a completed identical import does not rewrite or duplicate articles. Changed data or transformations under an already reserved/published revision are rejected. Plan a new reviewed edition/collection migration for future parser or index-format changes; never overwrite a live edition in place.

## Commands

Run from the repository root; no new NuGet dependency is needed.

```powershell
dotnet build Tools/AskARabbi.DictionaryImporter/AskARabbi.DictionaryImporter.csproj -c Release

# Public, pinned download into a NEW or empty directory; no database access.
dotnet run --project Tools/AskARabbi.DictionaryImporter -c Release -- download artifacts/bdb-4fa2054

# Offline integrity/structure check; no database access or writes.
dotnet run --project Tools/AskARabbi.DictionaryImporter -c Release -- validate artifacts/bdb-4fa2054
```

Set `MongoDB__ConnectionString` and `MongoDB__DatabaseName` for the **explicitly approved target** through the environment/secret store. The importer does not load API user secrets or discover a production connection. An optional `MongoDB__LexiconCollectionName` override must match the API.

```powershell
# Creates/writes the selected collection, so requires an explicit write flag.
dotnet run --project Tools/AskARabbi.DictionaryImporter -c Release -- import artifacts/bdb-4fa2054 --confirm-write

# Read-only diagnostic searches. Output includes IDs and pinned source links.
dotnet run --project Tools/AskARabbi.DictionaryImporter -c Release -- search "קרא"
dotnet run --project Tools/AskARabbi.DictionaryImporter -c Release -- search "gourd"
dotnet run --project Tools/AskARabbi.DictionaryImporter -c Release -- search "BDB08691"
```

Downloads refuse to overwrite a nonempty directory. A partially downloaded directory should be retained for diagnosis and a new empty directory used for retry. Cancellation, invalid input, fingerprint mismatch, Mongo failures, and missing imports produce explicit failure rather than empty successful evidence. No importer command deletes a collection or source files.

## Model access and limits

The API registers `BdbDictionaryAITools` alongside `CalendarAITools`:

- `search_bdb_dictionary(query)` returns up to three original excerpts, 2,400 characters each, with match type, article IDs, and continuation offsets.
- `read_bdb_entry(entryId, offset)` reads another contiguous excerpt from a known article, including abbreviation articles.
- Existing request-level function-call limits apply. Citation IDs remain distinct from calendar and religious evidence. Exact quotation checks and the independent semantic audit still apply; failed lookups do not become evidence.
- The model can use its knowledge to suggest searches, but cannot substitute remembered definitions for retrieved support. It must not describe tools or internal searches in the user-facing answer.
- BDB quotation text stays original. The model explains it in the selected response language without overriding the user's separate Torah quotation-language preference.

BDB primarily covers **Biblical Hebrew and Biblical Aramaic**. It is not a comprehensive rabbinic-Aramaic dictionary and cannot establish a later custom or prayer merely from a similar spelling. The earlier Rosh Hashanah squash question still requires the actual custom/prayer sources; this dictionary is additional linguistic context, not a complete fix for religious-source retrieval. A separately reviewed Jastrow import would be a sensible later expansion. No additional dictionaries or synthetic answer corpus are included here.

## Production rollout (not performed by these code changes)

1. Review/approve the pinned edition and the target database/collection. Check Cosmos collection capacity, request-unit and storage impact; this uses existing infrastructure but does not mean zero database cost.
2. Run the approved import from a machine/job with private-network access and write/index permissions. Do not open the production database to the public internet. Runtime API lookups only require reads.
3. Confirm 3,005 articles plus one published manifest, inspect indexes, and verify pointed/unpointed Hebrew, English meanings, distinct homographs, abbreviation IDs, and no-match cases. An absent/unpublished dictionary returns a controlled failure; no startup import occurs.
4. Deploy the matching API and both prompt changes. Test real conversations and citations with the production model. No REST endpoint, frontend change, migration of user records, or religious vector-store rebuild is required.

Local verification uses a disposable MongoDB 7 container bound only to localhost. That proves driver serialization, index use, import, idempotency, and lookup behavior; it does **not** prove production Cosmos permissions/capacity or live-model answer quality. Automated MSTest suites remain hermetic with fake persistence and AI boundaries. See [verification notes](VERIFICATION.md) for the commands and results.
