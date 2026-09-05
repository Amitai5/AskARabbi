# Canonical source archive

`canonical-sources.zip` is a server-only snapshot of 593 approved normalized Sefaria editions (about 27 MB compressed). It is not downloaded by the frontend. It supports exact-reference and continuous Torah-portion reading; the existing Azure vector store remains the semantic-search provider.

The publisher selects the most complete approved edition for each title, collection, work, and English/Hebrew language. For equally complete Torah editions it prefers JPS 1917 and Tanach with Nikkud. Entries contain the original, unchanged normalized Markdown, addressed by the SHA-256 hashes in `Data/NormalizedData/Sefaria/Metadata/document-manifest.json`. The reader verifies size and checksum before parsing, preserves stable segment IDs and edition/license attribution, respects source filters, and bounds its in-memory cache.

Reproduce from the checksum-verified normalized corpus using a **new** output path (the command never overwrites an existing archive):

```powershell
dotnet run --project Tools/AskARabbi.CorpusPublisher -- bundle --output <new-output-path.zip>
```

Compare/review the generated archive before replacing the deployment asset. No API keys or account data are included. Source licensing and attribution remain in the approved manifest and the normalized document headers.

Read-only diagnostic commands:

```powershell
dotnet run --project Tools/AskARabbi.CorpusPublisher -- read --reference "Deuteronomy 6:4"
dotnet run --project Tools/AskARabbi.CorpusPublisher -- answer --endpoint <Azure-endpoint> --model <deployment> --vector-store-id <store> --tenant-id <tenant> --question "<question>||<follow-up>" --sources core
```

`answer` explicitly makes billable requests using the signed-in Azure CLI identity, medium reasoning, and priority processing. It does not connect to MongoDB or save user conversations. Its synthetic calendar profile is fixed test data, not a production account.
