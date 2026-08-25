# Sefaria raw data

This snapshot comes from the public [Sefaria-Export](https://github.com/Sefaria/Sefaria-Export) dataset and selects only these canonical category paths:

- `Tanakh > Torah`
- `Tanakh > Prophets`
- `Tanakh > Writings`
- `Mishnah > Seder ...`
- `Talmud > Bavli|Yerushalmi > Seder ...|Minor Tractates`
- Rif primary texts under `Talmud > Bavli > Rishonim on Talmud > Rif`
- Mishneh Torah primary texts under `Halakhah > Mishneh Torah`
- The four divisions of `Halakhah > Shulchan Arukh`, including embedded Rema glosses
- `Kabbalah > Zohar` for the selected Zohar and Zohar Chadash editions
- `Musar > Acharonim > Mesillat Yesharim`

The exact paths prevent primary texts from being silently mixed with Sefaria commentary categories. Torah is stored separately from Prophets and Writings without duplicating files.

Each text path ends with `{work}/{Sefaria language bucket}/{version title}--{URL hash}.json`. The short hash prevents collisions after Windows-safe filename normalization. The export's legacy `English` bucket contains translations in several languages; use `actualLanguage` from the JSON or manifest instead of inferring language from the directory.

`Metadata/permissive-versions.json` is the download allowlist. It is built from Sefaria's version-level metadata, tied to the active `books.json` checksum, and contains only versions classified as permissive. The upstream `books.json` remains discovery metadata and is not an AI document list.

`Metadata/manifest.jsonl` records only retained raw artifacts, with original URLs, local paths, SHA-256 checksums, version metadata, and `licenseStatus: permissive`. The accepted version-level license families are:

- Public domain (`Public Domain` or `PD`)
- CC0
- CC-BY
- CC-BY-SA

Licenses containing a noncommercial restriction are excluded. Versions with missing or unknown licenses, merged versions, ambiguous metadata, and anything else requiring review are also excluded. An explicit source-review denylist additionally excludes `Miqra Mevoar, trans. and edited by David Kokhav, Jerusalem 2020` because its original source terms conflict with Sefaria's `PD` label. The downloader applies this rule during discovery, cached-catalog validation, payload inspection, and pruning so a full redownload cannot restore it.

Supplemental records also carry a stable `workKey` and a `usageNote`. Edition selection is deliberately narrow: one preferred permissible edition per component and language, with a fallback only when the preferred edition is unavailable under an accepted license. English coverage is partial where Sefaria has no qualifying edition; restricted or unverified translations are not used to fill gaps.
