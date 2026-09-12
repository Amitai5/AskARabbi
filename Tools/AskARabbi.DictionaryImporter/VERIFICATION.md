# BDB implementation verification — 2026-09-12

Implemented and verified locally. **No production deployment, production MongoDB import, or live Azure model evaluation was performed.** Unrelated frontend/narration working-tree changes were left untouched. The disposable MongoDB container was stopped and removed after verification; the downloaded, checksum-verified source files remain in gitignored `artifacts/bdb-4fa2054` for reuse.

## Automated checks

Run from the repository root:

```powershell
dotnet test Library/AskARabbiLIB.Tests/AskARabbiLIB.Tests.csproj -c Release --no-restore --collect:"XPlat Code Coverage" --results-directory artifacts/bdb-final --logger "console;verbosity=minimal"
dotnet test Backend/AskARabbi.Api.Tests/AskARabbi.Api.Tests.csproj -c Release --no-restore --logger "console;verbosity=minimal"
dotnet test Backend/AskARabbi.DvarTorahJob.Tests/AskARabbi.DvarTorahJob.Tests.csproj -c Release --no-restore --logger "console;verbosity=minimal"
dotnet build Tools/AskARabbi.DictionaryImporter/AskARabbi.DictionaryImporter.csproj -c Release --no-restore
dotnet build Tools/AskARabbi.Tools.slnx -c Release --no-restore
git -c core.safecrlf=false diff --check -- Backend Library Prototype Tools THIRD_PARTY_NOTICES.md
```

- Library: **1,392 passed**, no failures/skips; 50 tests added over the 1,342-test baseline.
- API: **294 passed**, no failures/skips; includes the new production-composition-root registry test.
- Weekly job: **39 passed**, no failures/skips.
- Importer/project and tools solution builds passed with zero warnings/errors. API and job builds were also exercised by their tests. Scoped whitespace check passed.
- Total: **1,725 passing tests**, including 51 new tests.
- Library line coverage: **85.82% → 86.15%**; branch coverage: **80.94% → 81.07%**. This is whole-library coverage, not a claim of complete branch coverage for the new code. Importer network/file operations were checked separately with the real pinned download and isolated database.

Regression coverage includes Hebrew normalization, separate homograph IDs, original HTML-to-text conversion, invalid import metadata, unsafe identifiers, search filters/index definitions, paragraph-opening ranking, exact article reads, unpublished editions, interrupted import recovery, idempotency, conflicting reserved/published content, cancellation, no-match handling, citation identity, bounded continuation, forged-quotation rejection, religious quotation-language separation, and API registration alongside calendar functions.

## Real-data checks

The importer downloaded the 23 pinned source files and validated the expected SHA-256 fingerprint and 3,005 articles. An isolated `mongo:7.0` container was bound to `127.0.0.1`, with no production secrets or user records. The final test used database `askarabbi_bdb_verification`, collection override `lexiconEntriesRanked`.

```powershell
dotnet Tools/AskARabbi.DictionaryImporter/bin/Release/net10.0/AskARabbi.DictionaryImporter.dll download artifacts/bdb-4fa2054
dotnet Tools/AskARabbi.DictionaryImporter/bin/Release/net10.0/AskARabbi.DictionaryImporter.dll validate artifacts/bdb-4fa2054
# The following were run only with the disposable local database configuration:
dotnet Tools/AskARabbi.DictionaryImporter/bin/Release/net10.0/AskARabbi.DictionaryImporter.dll import artifacts/bdb-4fa2054 --confirm-write
dotnet Tools/AskARabbi.DictionaryImporter/bin/Release/net10.0/AskARabbi.DictionaryImporter.dll search "קרא"
dotnet Tools/AskARabbi.DictionaryImporter/bin/Release/net10.0/AskARabbi.DictionaryImporter.dll search "קָרָא"
dotnet Tools/AskARabbi.DictionaryImporter/bin/Release/net10.0/AskARabbi.DictionaryImporter.dll search "gourd"
dotnet Tools/AskARabbi.DictionaryImporter/bin/Release/net10.0/AskARabbi.DictionaryImporter.dll search "ABBR.447"
dotnet Tools/AskARabbi.DictionaryImporter/bin/Release/net10.0/AskARabbi.DictionaryImporter.dll search "zyxnotaword"
dotnet Tools/AskARabbi.DictionaryImporter/bin/Release/net10.0/AskARabbi.DictionaryImporter.dll import artifacts/bdb-4fa2054 --confirm-write
```

All final operations succeeded. Both Hebrew spellings returned the two distinct `קרא` articles. English `gourd` ranked the relevant `פקע` article first, before passing mentions in other articles. `ABBR.447` resolved `NH` to `New (Late) Hebrew`; the nonexistent term returned no entries. Reimport retained exactly 3,005 articles plus one manifest, with no duplicates.

MongoDB `explain("executionStats")` used `ix_lexicon_lookupKeys` for all sampled search filters (same dictionary/revision filters and `_id` sort as the application):

| Lookup | Returned | Index keys examined | Documents examined |
| --- | ---: | ---: | ---: |
| Exact `קרא` headword | 2 | 2 | 2 |
| `gourd` in paragraph openings | 1 | 1 | 1 |
| `gourd` anywhere in article | 3 | 4 | 4 |
| `read` and `call` in paragraph openings | 1 | 12 | 12 |

The local collection's BSON data was 18,813,015 bytes and its indexes occupied 5,545,984 bytes. These are local MongoDB measurements, **not** estimates of Cosmos request units, storage billing, or production latency.

## Remaining verification

Before enabling production use, import from an approved private-network execution context and verify Cosmos collection/index provisioning and permissions. Then exercise real model conversations, including ambiguity, a linguistic follow-up, and the original Rosh Hashanah question together with its religious sources. The automated conversation tests prove evidence plumbing and rejection of fabricated quotes using fake AI responses; they do not prove that a live model always chooses the right entry or answers every custom question.
