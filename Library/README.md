# AskARabbiLIB

`AskARabbiLIB` is the reusable .NET 10 class library for the local Sefaria corpus. Its namespace, project, assembly, and solution all use the `AskARabbiLIB` name.

The library owns manifest deserialization and validation, immutable in-memory search indexes, facet discovery, deterministic ranking, and checksum-verified source-file loading. It has no dependency on the console prototype or Spectre.Console, so future APIs, agents, and background services can reference it directly.

## Solution layout

```text
Library/
├── AskARabbiLIB.slnx
├── AskARabbiLIB/
│   └── AskARabbiLIB.csproj
└── AskARabbiLIB.Tests/
    └── AskARabbiLIB.Tests.csproj
```

`AskARabbiLIB.slnx` contains the production library and its MSTest project. Every model, manifest-loader behavior, search algorithm, facet rule, ranking rule, path-safety check, checksum check, and file-loading behavior is tested here. The test project references only `AskARabbiLIB`; it does not reference `AskARabbiPrototype`.

## Data lifecycle

1. `ManifestLoader` reads the complete `document-manifest.json` into memory and validates schema version 1.1.
2. `ManifestSearchIndex` builds immutable keyword and metadata indexes once.
3. Callers use `ManifestSearchQuery` to filter by keywords, language, collection, category, title, version, exact license, segment range, and pagination. License-status filtering is intentionally absent because every catalog entry is permissive.
4. `SefariaDocumentFileLoader` lazily loads only a selected result. `LoadRawFileAsync` uses the entry's `rawFilePath`; `LoadNormalizedMarkdownAsync` uses `filePath`.

`SefariaDocumentFile` preserves the complete original JSON, exposes every top-level metadata property except `text`, retains the nested `text` value as structured JSON, and can enumerate or join its unmodified string leaves in source order.

Source paths are resolved beneath a configured repository root and file checksums are verified before content is returned. The generated catalog admits only permissively classified versions, while callers still receive the exact `License` and `LicenseStatus` needed to preserve attribution and comply with share-alike terms.

## Build and test

Run from the repository root:

```powershell
dotnet build Library/AskARabbiLIB.slnx -c Release
dotnet test Library/AskARabbiLIB.slnx -c Release --no-build
```

Collect library coverage with:

```powershell
dotnet test Library/AskARabbiLIB.slnx -c Release --no-build --collect:"XPlat Code Coverage" --results-directory Library/TestResults
```
