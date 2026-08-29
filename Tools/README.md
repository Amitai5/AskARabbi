# AskARabbi corpus tools

`AskARabbi.CorpusPublisher` is the reviewed, reproducible path from checksum-verified normalized Sefaria Markdown to an Azure OpenAI managed vector store. It never asks an Assistant to answer a question and never accepts an API key or client secret.

Run these commands from the repository root:

```powershell
dotnet run --project Tools/AskARabbi.CorpusPublisher -- fingerprint
dotnet run --project Tools/AskARabbi.CorpusPublisher -- validate
dotnet run --project Tools/AskARabbi.CorpusPublisher -- publish --endpoint https://your-resource.openai.azure.com/ --maximum-documents 3 --name "AskARabbi Development Sefaria Pilot"
dotnet run --project Tools/AskARabbi.CorpusPublisher -- verify --endpoint https://your-resource.openai.azure.com/ --vector-store-id vs_replace_me --maximum-documents 3
dotnet run --project Tools/AskARabbi.CorpusPublisher -- search --endpoint https://your-resource.openai.azure.com/ --vector-store-id vs_replace_me --query "chicken and milk"
```

`validate` performs the full checksum, Markdown, metadata-limit, segment, record, and upload-byte preflight without contacting Azure. Publication repeats that preflight before it creates a billable store. Omit `--maximum-documents` only for the full production publication. The tool creates a new immutable store, uploads deterministic search documents, applies source/language/license attributes, waits for indexing, and verifies the store fingerprint and counts before printing the production configuration values. Existing stores are never overwritten or deleted.
