# Normalized Sefaria Markdown

Each allowlisted raw Sefaria text version becomes one UTF-8 Markdown document under the same collection, work, language-bucket, and version hierarchy. Every document contains:

- YAML front matter with provider, collection, work, version, language, license, source URL, raw path, and raw checksum.
- A top-level work title.
- One `##` block per nonempty Sefaria segment with a stable canonical reference.
- Text normalized to NFC Unicode with HTML entities decoded, layout tags removed, line breaks preserved, and semantic emphasis or footnotes converted to Markdown.
- Structured `work_key` and `usage_note` fields for curated supplemental works. In Shulchan Arukh documents, source `<small>` blocks are preserved as explicit `**Rema:**` glosses rather than losing the distinction from Rabbi Yosef Karo's base text.

References preserve source structure: `Genesis 1:1`, `Mishnah Berakhot 1:1`, `Shabbat 2a:1`, and `Jerusalem Talmud Berakhot 1:1:1`. Named nodes in complex Minor Tractates are retained in the reference.

The normalizer accepts only records whose version-level license is public domain, CC0, CC-BY, or CC-BY-SA, whose `licenseStatus` is `permissive`, and whose exact edition is not on the source-review denylist. It fails instead of processing noncommercial, missing, unknown, merged, denied, or review-required records, and prunes normalized Markdown that is no longer in the allowed raw manifest.

## AI document catalog

`Metadata/document-manifest.json` is the permissive-only AI-facing catalog for the normalized corpus. Schema 1.3 has one entry per document with its stable ID, normalized/raw paths and checksums, descriptive metadata, canonical reference bounds, and segment count. It retains the exact upstream license and adds `licenseCategory` (`publicDomain`, `cc0`, `ccBy`, or `ccBySa`), `requiresAttribution`, `requiresShareAlike`, and an original-source `attributionUrl` used for trusted clickable citations. Supplemental entries also expose structured `workKey` and `usageNote` values for later SQL columns or retrieval filters.

Regenerate the catalog from the verified raw and normalized manifests after downloading or normalizing new data:

```powershell
& "C:\path\to\python.exe" scripts/create-sefaria-document-manifest.py --data-root Data
```
