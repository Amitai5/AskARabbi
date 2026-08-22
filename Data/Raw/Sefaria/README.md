# Sefaria raw data

This snapshot comes from the public [Sefaria-Export](https://github.com/Sefaria/Sefaria-Export) dataset and selects only these canonical category paths:

- `Tanakh > Torah`
- `Tanakh > Prophets`
- `Tanakh > Writings`
- `Mishnah > Seder ...`
- `Talmud > Bavli|Yerushalmi > Seder ...|Minor Tractates`

The exact paths prevent primary texts from being silently mixed with Sefaria commentary categories. Torah is stored separately from Prophets and Writings without duplicating files.

Each text path ends with `{work}/{Sefaria language bucket}/{version title}--{URL hash}.json`. The short hash prevents collisions after Windows-safe filename normalization. The export's legacy `English` bucket contains translations in several languages; use `actualLanguage` from the JSON or manifest instead of inferring language from the directory.

`Metadata/permissive-versions.json` is the download allowlist. It is built from Sefaria's version-level metadata, tied to the active `books.json` checksum, and contains only versions classified as permissive. The upstream `books.json` remains discovery metadata and is not an AI document list.

`Metadata/manifest.jsonl` records only retained raw artifacts, with original URLs, local paths, SHA-256 checksums, version metadata, and `licenseStatus: permissive`. The accepted version-level license families are:

- Public domain (`Public Domain` or `PD`)
- CC0
- CC-BY
- CC-BY-SA

Licenses containing a noncommercial restriction are excluded. Versions with missing or unknown licenses, merged versions, ambiguous metadata, and anything else requiring review are also excluded. The downloader checks the allowlist before fetching text, revalidates the downloaded payload, and removes stale raw files that are not in the current permissive manifest.
