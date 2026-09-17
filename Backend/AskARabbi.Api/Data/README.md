# Canonical source archive

`canonical-sources.zip` is a server-only snapshot of 593 approved normalized Sefaria editions (about 27 MB compressed). It is not downloaded by the frontend. It supports exact-reference and continuous Torah-portion reading; the existing Azure vector store remains the semantic-search provider.

The API Docker build also produces `Data/canonical-search.sqlite`: a read-only FTS5 index of the archive's 240,103 original segments. It is approximately 302 MiB on disk and increases the container image size, not the frontend bundle. SQLite accesses it on demand rather than keeping the entire corpus in memory. It uses the existing dependency and ephemeral container disk, with no new Azure service or provisioned database throughput. When semantic results fail topical adequacy, `ResearchSourceRetriever` searches this index with unchanged source filters. Multi-concept relevance precedes word frequency; unrelated repetitions must not outrank a passage connecting the object and its context. Source claims still have to pass exact quotation and support validation. If no relevant evidence remains, reviewed Background or Uncertainty may still answer an ordinary question; missing or stale assets and retrieval outages retain their error behavior. See [answer reliability](../../../docs/ANSWER_RELIABILITY.md).

For a local configured API, generate the index into its build output's `Data` directory before running it (substitute the actual configuration):

```powershell
dotnet run --project Tools/AskARabbi.CorpusPublisher -- index-bundle --index Backend/AskARabbi.Api/bin/Debug/net10.0/Data/canonical-search.sqlite
dotnet run --project Tools/AskARabbi.CorpusPublisher -- search-bundle --index Backend/AskARabbi.Api/bin/Debug/net10.0/Data/canonical-search.sqlite --query "pumpkin Rosh Hashanah prayer"
```

The index builder verifies every archived document before indexing and records the exact bundled-manifest fingerprint. A stale or missing index is rejected; deploy through the Docker build rather than copying an API binary alone. The private diagnostic image builds the identical index.

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

For a local `answer` probe, first run `index-bundle --index Data/canonical-search.sqlite` from the repository root. The private-network probe image includes this asset automatically.

For private-network checks, build `Tools/AskARabbi.CorpusPublisher/Dockerfile` and run `answer` with `--credential managed-identity` as a [one-off execution override](https://learn.microsoft.com/en-us/azure/container-apps/jobs#start-a-job-execution) on an already-authorized cloud job. This selects the host's system-assigned identity without Azure CLI sign-in or copied credentials. Preserve the parent job's scheduled template. The probe includes religious-source research and prints operation names, success flags, attempted canonical references, and returned references (not search arguments, credentials, or profile data). Use non-sensitive test questions. It deliberately does not connect to MongoDB or test BDB availability; verify BDB with the dictionary importer's separate read-only commands, then test the full deployed API.
