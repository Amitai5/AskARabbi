# Normalized Sefaria Markdown

Each raw Sefaria text version becomes one UTF-8 Markdown document under the same collection, work, language-bucket, and version hierarchy. Every document contains:

- YAML front matter with provider, collection, work, version, language, license, source URL, raw path, and raw checksum.
- A top-level work title.
- One `##` block per nonempty Sefaria segment with a stable canonical reference.
- Text normalized to NFC Unicode with HTML entities decoded, layout tags removed, line breaks preserved, and semantic emphasis or footnotes converted to Markdown.

References preserve source structure: `Genesis 1:1`, `Mishnah Berakhot 1:1`, `Shabbat 2a:1`, and `Jerusalem Talmud Berakhot 1:1:1`. Named nodes in complex Minor Tractates are retained in the reference.

The complete normalized corpus includes every raw version so no source data is silently discarded. Presence is not permission: filter `Metadata/manifest.jsonl` by `licenseStatus` before any production, redistribution, model-training, or commercial use.
