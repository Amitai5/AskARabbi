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

Raw JSON and normalized Markdown are local working data rather than project code. They are intentionally excluded from Git because they are large, reproducible, and licensed independently. Provider READMEs, compact summaries, and the scripts that reproduce both stages remain trackable.

## Pipeline

1. `scripts/download-sefaria-core.py` downloads and verifies canonical Sefaria JSON, schemas, license metadata, and manifests under `Raw/Sefaria`.
2. `scripts/normalize-sefaria-markdown.py` validates every raw checksum, removes HTML presentation markup, preserves meaningful emphasis and footnotes as Markdown, reconstructs canonical segment references, and writes one Markdown document per source version under `NormalizedData/Sefaria`.
3. Each normalized document carries YAML provenance including its source URL, raw checksum, language, version, and license status. The normalized manifest records output checksums and segment counts for later chunking, embeddings, or training-set construction.

The normalized corpus is source material prepared for later chunking or vectorization, not a vector index and not an automatically approved training set. Downstream jobs must filter by `license_status` and retain required attribution or share-alike terms.

## Commands

Run from the repository root:

```powershell
& "C:\path\to\python.exe" scripts/download-sefaria-core.py --data-root Data --workers 8
& "C:\path\to\python.exe" scripts/normalize-sefaria-markdown.py --data-root Data --workers 4
```

Add `--refresh-index` to the downloader to fetch the latest monthly Sefaria index. For downstream subsets, filter the normalized manifest by `licenseStatus` while keeping the complete archive reproducible.
