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
│       └── Metadata/
└── NormalizedData/
    └── Sefaria/
        ├── Torah/
        ├── Tanakh/
        ├── Mishnah/
        ├── Talmud/
        └── Metadata/
```

Raw JSON and normalized Markdown are local working data rather than project code. They are intentionally excluded from Git because they are large and reproducible. Provider READMEs, compact summaries, the permissive-version catalog, and the scripts that reproduce both stages remain trackable.

## Pipeline

1. `scripts/download-sefaria-core.py` obtains version-level license metadata, builds a fail-closed allowlist, and downloads only public-domain, CC0, CC-BY, or CC-BY-SA text versions. It verifies each payload and prunes any raw text that is no longer allowlisted.
2. `scripts/normalize-sefaria-markdown.py` refuses non-permissive manifest records, validates every raw checksum, removes HTML presentation markup, preserves meaningful emphasis and footnotes as Markdown, reconstructs canonical segment references, and writes one Markdown document per allowed source version under `NormalizedData/Sefaria`. Stale normalized documents are pruned.
3. Each normalized document carries YAML provenance including its source URL, raw checksum, language, version, and license status. The normalized manifest records output checksums and segment counts for later chunking, embeddings, or training-set construction.
4. `scripts/create-sefaria-document-manifest.py` joins raw Sefaria metadata to the normalized manifest, validates every Markdown checksum, and writes an AI-facing JSON catalog at `NormalizedData/Sefaria/Metadata/document-manifest.json`.

The corpus contains only versions with an explicitly permissive license classification. Noncommercial, missing, unknown, merged, and otherwise review-required versions are excluded from raw text storage, normalized data, and both document manifests. This filter does not waive each retained version's attribution or share-alike obligations; downstream systems must preserve and honor the exact `license` value.

## Commands

Run from the repository root:

```powershell
& "C:\path\to\python.exe" scripts/download-sefaria-core.py --data-root Data --workers 8
& "C:\path\to\python.exe" scripts/normalize-sefaria-markdown.py --data-root Data --workers 4
& "C:\path\to\python.exe" scripts/create-sefaria-document-manifest.py --data-root Data
```

Add `--refresh-index` to fetch the latest Sefaria export index and rebuild the license allowlist from current version metadata. Use `--refresh-license-metadata` by itself to refresh the allowlist without replacing the export index. Both refresh paths fail closed: a text is not downloaded unless its version-level license is explicitly classified as permissive.
