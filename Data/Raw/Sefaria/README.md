# Sefaria raw data

This snapshot comes from the public [Sefaria-Export](https://github.com/Sefaria/Sefaria-Export) dataset and selects only these canonical category paths:

- `Tanakh > Torah`
- `Tanakh > Prophets`
- `Tanakh > Writings`
- `Mishnah > Seder ...`
- `Talmud > Bavli|Yerushalmi > Seder ...|Minor Tractates`

The exact paths prevent primary texts from being silently mixed with Sefaria commentary categories. Torah is stored separately from Prophets and Writings without duplicating files.

Each text path ends with `{work}/{Sefaria language bucket}/{version title}--{URL hash}.json`. The short hash prevents collisions after Windows-safe filename normalization. The export's legacy `English` bucket contains translations in several languages; use `actualLanguage` from the JSON or manifest instead of inferring language from the directory.

`Metadata/manifest.jsonl` records original URLs, local paths, SHA-256 checksums, version metadata, and one of these conservative license classes:

- `permissive`: public domain (`Public Domain` or `PD`), CC0, CC-BY, or CC-BY-SA.
- `noncommercial`: a license containing a noncommercial restriction.
- `review_required`: absent, unknown, merged, or otherwise unclassified licensing.

Sefaria merged files maximize coverage but omit a top-level license. They are retained as review-required raw snapshots and must not be treated as production-approved merely because they are present.
