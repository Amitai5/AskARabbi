# Normalized data

Reproducible, model-ready textual normalization is stored under `{provider}/{collection}`. These files are intended for later chunking, retrieval, embedding, evaluation, or appropriately licensed training workflows.

Normalized files must remain traceable to raw provider artifacts. Do not manually edit generated Markdown; update the normalizer and regenerate it.

Each Markdown `##` heading is a canonical source reference. `AskARabbiLIB` verifies the file checksum and the manifest's segment count/reference range before adding those segments to the local FTS5 index. `Sefaria/Metadata/document-manifest.json` is committed at schema 1.3 with typed license terms, original-source attribution URLs, and paired supplemental `workKey`/`usageNote` metadata; `Sefaria/Metadata/segment-search-v3.sqlite` is generated locally, corpus-fingerprinted, and ignored by Git.

Curated supplemental documents include structured `workKey` and `usageNote` metadata in the JSON manifest and corresponding YAML fields in Markdown. These fields, canonical segment headings, checksums, and typed license fields form the handoff for a later SQL import without requiring the SQLite index to be rebuilt during data acquisition.
