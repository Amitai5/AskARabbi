# AskARabbiPrototype

`AskARabbiPrototype` is a simple .NET 10 console application for exercising `AskARabbiLIB`. Its namespace, project, assembly, and solution all use the `AskARabbiPrototype` name.

The prototype contains only the console layer. Manifest models, loading, indexes, search, ranking, facets, checksum validation, and document-file access remain in `AskARabbiLIB`. The console uses Spectre.Console for guided prompts, searchable choices, status indicators, formatted tables, and safer plain-text source display.

## Solution layout

```text
Prototype/
├── AskARabbiPrototype.slnx
└── AskARabbiPrototype/
    └── AskARabbiPrototype.csproj
```

There is no prototype test project. All tests for the reusable library live in `Library/AskARabbiLIB.Tests` and are run from `Library/AskARabbiLIB.slnx`.

## Interactive use

Run from the repository root with no command:

```powershell
dotnet run --project Prototype/AskARabbiPrototype --
```

The guided interface can:

- Search by keywords and choose whether all or any terms must match.
- Filter with searchable language, collection, category, and license-status selectors.
- Rank and display results in a compact table.
- Open a result and inspect its manifest metadata, raw text, complete source metadata, original Sefaria JSON, or normalized Markdown.
- Browse available facets, inspect load/index statistics, and reload the manifest.

The application locates `Data/NormalizedData/Sefaria/Metadata/document-manifest.json` by walking upward from the working and executable directories. Use `--manifest <path>` and `--repository-root <path>` to override discovery.

The manifest contains only versions that passed the permissive-license gate. Every result still exposes its exact license so callers can preserve required attribution and comply with any share-alike terms.

## One-shot use

Scripts and agents can request table or machine-readable JSON output without entering the interactive interface:

```powershell
dotnet run --project Prototype/AskARabbiPrototype -- search "Shabbat fire" --language English --collection Talmud --category "Seder Moed" --limit 10 --format json
dotnet run --project Prototype/AskARabbiPrototype -- facets --format json
dotnet run --project Prototype/AskARabbiPrototype -- stats --format json
```

Search supports `--language`, `--collection`, `--category`, `--title`, `--version`, `--license`, `--license-status`, `--match all|any`, `--min-segments`, `--max-segments`, `--skip`, `--limit 1-200`, and `--format table|json`.

## Build

Build the standalone prototype solution with:

```powershell
dotnet build Prototype/AskARabbiPrototype.slnx -c Release
```

Build and test the reusable library separately with:

```powershell
dotnet build Library/AskARabbiLIB.slnx -c Release
dotnet test Library/AskARabbiLIB.slnx -c Release --no-build
```
