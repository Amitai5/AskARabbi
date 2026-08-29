# AskRabbi data

This directory has two explicit stages: immutable provider exports in `Raw` and reproducible Markdown documents in `NormalizedData`. Both stages are grouped first by provider and then by collection so future sources can coexist without losing provenance.

## Layout

```text
Data/
├── Raw/
│   └── Sefaria/
│       ├── Torah/
│       ├── Tanakh/
│       ├── Mishnah/
│       ├── Talmud/
│       ├── Halakhah/
│       ├── Kabbalah/
│       ├── Musar/
│       └── Metadata/
└── NormalizedData/
    └── Sefaria/
        ├── Torah/
        ├── Tanakh/
        ├── Mishnah/
        ├── Talmud/
        ├── Halakhah/
        ├── Kabbalah/
        ├── Musar/
        └── Metadata/
            ├── manifest.jsonl
            ├── document-manifest.json
            └── segment-search-v3.sqlite  # generated locally; ignored by Git
```

Raw JSON and normalized Markdown are local working data rather than project code. They are intentionally excluded from Git because they are large and reproducible. Provider READMEs, compact summaries, the permissive-version catalog, and the scripts that reproduce both stages remain trackable.

## Pipeline

1. `scripts/download-sefaria-core.py` obtains version-level license metadata, builds a fail-closed allowlist, and downloads only public-domain, CC0, CC-BY, or CC-BY-SA text versions. It also applies the source-review denylist, verifies each payload, and prunes any raw text that is no longer allowlisted.
2. `scripts/normalize-sefaria-markdown.py` refuses non-permissive manifest records, validates every raw checksum, removes HTML presentation markup, preserves meaningful emphasis and footnotes as Markdown, reconstructs canonical segment references, and writes one Markdown document per allowed source version under `NormalizedData/Sefaria`. Stale normalized documents are pruned.
3. Each normalized document carries YAML provenance including its source URL, raw checksum, language, version, and license status. The normalized manifest records output checksums and segment counts for later chunking, embeddings, or training-set construction.
4. `scripts/create-sefaria-document-manifest.py` joins raw Sefaria metadata to the normalized manifest, validates every Markdown checksum, and writes schema 1.3 of the AI-facing JSON catalog at `NormalizedData/Sefaria/Metadata/document-manifest.json`. Every entry has a stable `documentId`, a typed `licenseCategory`, validated attribution/ShareAlike flags, and an `attributionUrl` for the original edition source.
5. `AskARabbiLIB` parses checksum-verified Markdown headings into canonical segments and atomically builds `segment-search-v3.sqlite`. The index retains supplemental `workKey` and `usageNote` metadata, records its schema version and a corpus-and-license fingerprint, and rejects missing, corrupt, wrong-license, or stale indexes rather than querying them.
6. `AskARabbi.CorpusPublisher` converts the same verified documents into deterministic records and upload parts capped at 60,000 UTF-8 bytes for a new Azure OpenAI managed vector store. It preserves the corpus fingerprint, stable IDs, source metadata, license fields, exact text, explicit overlong excerpts, and work-level filters; generated upload artifacts are streamed and are not committed. The checked corpus becomes 8,332 provider files from 1,441 logical documents and remains below Azure's 10,000-file limit.

The corpus contains only versions with an explicitly permissive license classification that are not on the source-review denylist. Noncommercial, missing, unknown, merged, denied, and otherwise review-required versions are excluded from raw text storage, normalized data, and both document manifests. This filter does not waive each retained version's attribution or ShareAlike obligations; downstream systems must preserve the exact `license` value and honor the typed license terms.

## Commands

Run from the repository root:

```powershell
& "C:\path\to\python.exe" scripts/download-sefaria-core.py --data-root Data --workers 8
& "C:\path\to\python.exe" scripts/normalize-sefaria-markdown.py --data-root Data --workers 4
& "C:\path\to\python.exe" scripts/create-sefaria-document-manifest.py --data-root Data
dotnet run --project Prototype/AskARabbiPrototype -- index build
dotnet run --project Prototype/AskARabbiPrototype -- index verify --format json
dotnet run --project Tools/AskARabbi.CorpusPublisher -- fingerprint
```

The SQLite file is reproducible and intentionally excluded from Git together with raw JSON and normalized Markdown. Rebuild it after downloading, normalizing, or regenerating the manifest. A managed publication is also reproducible, but Azure-assigned file/store IDs are environment state rather than repository data. Publish a new store after any fingerprint change, verify it, switch the API configuration atomically, and retire the old store only after smoke testing. See [`docs/MANAGED_VECTOR_STORE.md`](../docs/MANAGED_VECTOR_STORE.md).

Add `--refresh-index` to fetch the latest Sefaria export index and rebuild the license allowlist from current version metadata. Use `--refresh-license-metadata` by itself to refresh the allowlist without replacing the export index. Both refresh paths fail closed: a text is not downloaded unless its version-level license is explicitly classified as permissive.
