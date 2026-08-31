# Managed Sefaria vector store

AskRabbi's first production retriever uses an Azure OpenAI managed vector store through a forced Responses API `file_search` tool call. The application does not create an Assistant and ignores all model-authored prose from that retrieval response; it accepts only the included scored file-search results. `AzureOpenAIVectorStoreRetriever` reconstructs and filters those results, `GroundedAnswerService` builds the bounded evidence packet, and the existing deterministic quotation/citation checks plus independent claim-support audit decide whether an answer may be shown.

## Why this design

The managed store has no continuously provisioned Azure AI Search unit, which makes it a practical starting point for a low-traffic launch. Azure handles parsing, embeddings, keyword/semantic retrieval, and vector storage. AskRabbi keeps stable source IDs, filters, provenance, evidence budgets, answer generation, and validation in application code.

The tradeoff is less ranking and index control than a dedicated hybrid search service, provider-managed chunking, usage-based file-search/storage charges, and an API surface that must be monitored for changes. Each answer also needs a small `gpt-5-mini` retrieval call before the separate grounded-answer generation call, adding token cost and latency. `ISourceRetriever` isolates that dependency so a future Azure AI Search or other retriever can replace it without changing the answer contract.

Always check the current [Azure OpenAI pricing page](https://azure.microsoft.com/en-us/pricing/details/azure-openai/) before publishing. Azure lists vector storage per binary GB per day, includes a free storage allowance, and may charge separately for file-search operations. The store's `usage_bytes` response is the source for the billable-size estimate; raw Markdown size is not a reliable estimate of embedded storage.

## Reproducible corpus contract

The checked corpus snapshot contains 1,441 permissively licensed documents and 476,116 canonical source segments. Its current full-corpus fingerprint is:

```text
7328aa9c42ece3fd9442f0596fd45de4c3dff950cc26c4615c704e37ac1a06cc
```

Recompute rather than copying this value after any data or manifest change:

```powershell
dotnet run --project Tools/AskARabbi.CorpusPublisher -- fingerprint
dotnet run --project Tools/AskARabbi.CorpusPublisher -- validate
```

`validate` reads and checksum-verifies every normalized file, formats every record, enforces Azure metadata limits, and reports exact source-segment, search-record, and upload-byte totals without contacting Azure. Publication repeats this complete preflight before creating a billable resource.

The current checked snapshot passes with 1,441 logical documents, 8,332 bounded upload files, 476,116 source segments, 488,019 searchable records, and 450,610,771 UTF-8 upload bytes. Azure's reported `usage_bytes` after indexing—not this upload total—determines managed vector-storage billing.

Publication creates one or more deterministic UTF-8 Markdown uploads per manifest document. No upload exceeds 60,000 UTF-8 bytes and a source record is never split across files; this avoids the provider failures observed with larger individual uploads while remaining below the 10,000-file store limit. The schema-v2 store metadata records both the logical-document count and provider-file count.

The publisher supplies the Azure maximum of 16 reviewed attributes on each file: corpus fingerprint, stable document ID, English/Hebrew title, language/name/code, collection, categories, edition, license/category, attribution URL, normalized path, optional work key/usage note, and provider. The current Azure Responses file-search result does not reliably return those attributes, so the production image bundles only the validated 2.6 MB document manifest and reconstructs citation provenance from the stable document prefix inside each record. Returned attributes, when present, must agree with that manifest. Raw JSON, normalized Markdown, and the SQLite index remain outside the image. Every searchable record contains:

- The existing stable document and segment IDs.
- Canonical reference and zero-based document ordinal.
- A deterministic context-lookup token.
- Exact normalized source text.
- Explicit source bounds for overlong segments.

A source segment of at most 1,500 characters remains one record. Longer segments become deterministic 1,500-character windows with 300-character overlap and IDs such as `...:excerpt:0001`; they are never silently truncated. Azure static chunking uses 4,096 tokens with 2,048-token overlap so each compact record can be returned intact. The retriever ignores incomplete record fragments and rejects altered IDs, bounds, licenses, manifest provenance, schema versions, fingerprints, logical-document counts, or provider-file counts.

## Authentication and roles

The publisher and API use `DefaultAzureCredential`; neither accepts an API key or client secret.

- A person publishing files and stores from a workstation needs **Cognitive Services OpenAI Contributor** on only the target Azure OpenAI resource. **Cognitive Services OpenAI User** is sufficient for Responses inference but did not permit the required file-management operations.
- The `askarabbi-api` Container App's system-assigned managed identity needs **Cognitive Services OpenAI User** on that resource for search and Responses API calls; it does not need file-management permission.
- The GitHub deployment identity does not need Azure OpenAI data-plane access because it only updates the Container App image.

Use the narrow resource scope, not the subscription or resource group, and allow time for role propagation before retrying a 403.

## Pilot and full publication

Sign into the intended tenant first and verify the selected subscription:

```powershell
az login --tenant 42c55b1a-363e-4e7a-b5f3-b6b275908185
az account set --subscription c2f8383e-2c4e-4822-82a7-506b2e2ddf38
az account show --query "{tenantId:tenantId,subscription:id,user:user.name}" --output table
```

Create a small pilot before the full corpus:

```powershell
dotnet run --project Tools/AskARabbi.CorpusPublisher -- publish `
  --endpoint https://aarproduction-openai.openai.azure.com/ `
  --tenant-id 42c55b1a-363e-4e7a-b5f3-b6b275908185 `
  --maximum-documents 3 `
  --name "AskARabbi Development Sefaria Pilot"
```

Use the returned store ID to verify and search it:

```powershell
dotnet run --project Tools/AskARabbi.CorpusPublisher -- verify `
  --endpoint https://aarproduction-openai.openai.azure.com/ `
  --vector-store-id vs_replace_me `
  --maximum-documents 3

dotnet run --project Tools/AskARabbi.CorpusPublisher -- retrieve `
  --endpoint https://aarproduction-openai.openai.azure.com/ `
  --model askarabbi-gpt-5-mini `
  --vector-store-id vs_replace_me `
  --query "Why did Moses repeat the commandments in Deuteronomy?" `
  --maximum-documents 3
```

After the pilot passes, omit `--maximum-documents` to publish the full corpus. The tool always creates a new store and never overwrites or deletes an existing one. Uploads run with bounded concurrency, each bounded file is attached explicitly, publication waits for indexing, and success requires exact fingerprint, logical-document-count, and provider-file-count agreement.

The active full store is named `AskARabbi Production Sefaria Corpus`. Descriptive names should identify the environment and corpus purpose; the store ID remains the immutable configuration value.

## API configuration

Set these non-secret environment variables on `askarabbi-api`:

```text
AI__ProjectEndpoint=https://aarproduction-openai.openai.azure.com/
AI__ModelName=askarabbi-gpt-5-mini
AI__VectorStoreId=vs_returned_by_full_publication
AI__CorpusFingerprint=7328aa9c42ece3fd9442f0596fd45de4c3dff950cc26c4615c704e37ac1a06cc
AI__TenantId=42c55b1a-363e-4e7a-b5f3-b6b275908185
AI__TimeoutSeconds=120
AI__MaximumOutputTokens=8000
AI__RetrievalScoreThreshold=0.0
```

The endpoint, deployment name, vector-store ID, fingerprint, and tenant ID are identifiers rather than credentials. They may be plain Container App environment variables, but keeping environment-specific values out of tracked JSON avoids accidentally deploying a development resource to production. `DefaultAzureCredential` selects the Container App managed identity in Azure. The API image contains the trusted document manifest needed to resolve source metadata when Azure returns empty file attributes.

Do not switch only the store ID or only the fingerprint. Update them in one Container App revision, wait for readiness, and run representative grounded questions. The first search verifies store status, schema, fingerprint, source provider, and file counts before any result is accepted.

## Update and rollback

1. Regenerate and validate raw, normalized, and manifest data.
2. Compute and record the new fingerprint.
3. Publish a new store; never mutate the active corpus in place.
4. Verify forced file-search retrieval and exact-reference lookups against the new store.
5. Deploy the new store ID and fingerprint together.
6. Verify live fail-closed behavior, source filters, quotations, and usage accounting.
7. Keep the previous store during the rollback window.
8. Delete an old store only after resolving its exact ID and confirming no environment references it.

If retrieval is unavailable or the store is stale, the API returns `retrieval_unavailable`; it does not call the model with no evidence. If retrieval is merely irrelevant, it returns `insufficient_evidence`. Failed structured generation or validation likewise returns a typed status and never persists unsupported assistant content.
