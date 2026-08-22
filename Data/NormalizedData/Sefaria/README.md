# Normalized Sefaria Markdown

Each allowlisted raw Sefaria text version becomes one UTF-8 Markdown document under the same collection, work, language-bucket, and version hierarchy. Every document contains:

- YAML front matter with provider, collection, work, version, language, license, source URL, raw path, and raw checksum.
- A top-level work title.
- One `##` block per nonempty Sefaria segment with a stable canonical reference.
- Text normalized to NFC Unicode with HTML entities decoded, layout tags removed, line breaks preserved, and semantic emphasis or footnotes converted to Markdown.

References preserve source structure: `Genesis 1:1`, `Mishnah Berakhot 1:1`, `Shabbat 2a:1`, and `Jerusalem Talmud Berakhot 1:1:1`. Named nodes in complex Minor Tractates are retained in the reference.

The normalizer accepts only records whose version-level license is public domain, CC0, CC-BY, or CC-BY-SA and whose `licenseStatus` is `permissive`. It fails instead of processing noncommercial, missing, unknown, merged, or review-required records, and prunes normalized Markdown that is no longer in the allowed raw manifest. Retained license metadata must still be used to satisfy attribution and share-alike terms.

## AI document catalog

`Metadata/document-manifest.json` is the permissive-only AI-facing catalog for the normalized corpus. Schema 1.1 has one entry per document with a repository-relative normalized Markdown `filePath`, original Sefaria JSON `rawFilePath`, both file checksums, factual `fileDescription`, human-readable `fileLanguage`, and `fileTitle`. Entries also retain Sefaria categories, version metadata, canonical reference bounds, segment count, source URL, and exact license so an agent can narrow its candidates before opening source files.

Regenerate the catalog from the verified raw and normalized manifests after downloading or normalizing new data:

```powershell
& "C:\path\to\python.exe" scripts/create-sefaria-document-manifest.py --data-root Data
```
