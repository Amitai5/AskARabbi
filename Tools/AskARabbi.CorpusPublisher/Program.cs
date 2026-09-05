using System.Text.Json;
using AskARabbiLIB;
using AskARabbiLIB.Files;
using AskARabbiLIB.Retrieval;
using Azure.Identity;

return await RunAsync(args).ConfigureAwait(false);

static async Task<int> RunAsync(string[] args)
{
    if (args.Length == 0 || args[0] is "help" or "--help" or "-h")
    {
        PrintHelp();
        return 0;
    }

    using var cancellationSource = new CancellationTokenSource();
    Console.CancelKeyPress += (_, eventArgs) =>
    {
        eventArgs.Cancel = true;
        cancellationSource.Cancel();
    };

    try
    {
        var command = args[0].ToLowerInvariant();
        var options = ParseOptions(args[1..]);
        var repositoryRoot = Path.GetFullPath(GetOption(options, "repository-root") ?? Directory.GetCurrentDirectory());
        var manifestPath = Path.GetFullPath(GetOption(options, "manifest") ?? Path.Combine(repositoryRoot, "Data", "NormalizedData", "Sefaria", "Metadata", "document-manifest.json"));
        var manifest = await new ManifestLoader().LoadAsync(manifestPath, cancellationSource.Token).ConfigureAwait(false);
        var maximumDocuments = ParseOptionalPositiveInt(options, "maximum-documents");
        var publicationManifest = maximumDocuments is null ? manifest : manifest with { DocumentCount = Math.Min(maximumDocuments.Value, manifest.DocumentCount), Documents = manifest.Documents.Take(maximumDocuments.Value).ToArray() };
        var fingerprint = SourceIndexBuilder.ComputeCorpusFingerprint(publicationManifest);
        if (command == "read")
        {
            var reader = new BundledCanonicalSourceReader(manifest, Path.Combine(repositoryRoot, "Backend", "AskARabbi.Api", "Data", "canonical-sources.zip"));
            var segments = await reader.ReadAsync(RequireOption(options, "reference", null), new SourceRetrievalQuery { Languages = ["English", "Hebrew"] }, cancellationSource.Token).ConfigureAwait(false);
            WriteJson(segments.Select(segment => new { segment.CanonicalReference, segment.Text, segment.Version, segment.Language }));
            return 0;
        }
        if (command == "bundle")
        {
            var destination = Path.GetFullPath(RequireOption(options, "output", "CANONICAL_BUNDLE_OUTPUT"));
            var selected = manifest.Documents.Where(document => document.FileLanguageCode is "en" or "he")
                .GroupBy(document => (document.Collection, document.FileTitle, document.FileLanguageCode, document.WorkKey))
                .Select(group => group.OrderByDescending(document => document.SegmentCount)
                    .ThenByDescending(document => document.VersionTitle.Contains("JPS 1917", StringComparison.Ordinal) || document.VersionTitle == "Tanach with Nikkud")
                    .ThenBy(document => document.DocumentId, StringComparer.Ordinal).First())
                .OrderBy(document => document.DocumentId, StringComparer.Ordinal).ToArray();
            Directory.CreateDirectory(Path.GetDirectoryName(destination) ?? throw new InvalidOperationException("The archive requires a directory."));
            var loader = new SefariaDocumentFileLoader(repositoryRoot);
            using var output = new FileStream(destination, FileMode.CreateNew, FileAccess.Write);
            using var archive = new System.IO.Compression.ZipArchive(output, System.IO.Compression.ZipArchiveMode.Create);
            foreach (var document in selected)
            {
                await loader.LoadNormalizedMarkdownAsync(document, cancellationSource.Token).ConfigureAwait(false);
                var entry = archive.CreateEntry(document.Sha256 + ".md", System.IO.Compression.CompressionLevel.SmallestSize);
                entry.LastWriteTime = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
                await using var input = File.OpenRead(Path.Combine(repositoryRoot, document.FilePath));
                await using var entryStream = entry.Open();
                await input.CopyToAsync(entryStream, cancellationSource.Token).ConfigureAwait(false);
            }
            WriteJson(new { documents = selected.Length, originalBytes = selected.Sum(document => document.FileSizeBytes), destination });
            return 0;
        }
        if (command == "fingerprint")
        {
            WriteJson(new { corpusFingerprint = fingerprint, publicationManifest.DocumentCount, segmentCount = publicationManifest.Documents.Sum(document => (long)document.SegmentCount) });
            return 0;
        }
        if (command == "validate")
        {
            var provider = new SefariaNormalizedDocumentProvider(new SefariaDocumentFileLoader(repositoryRoot));
            var formatter = new AzureOpenAIVectorStoreCorpusFormatter();
            long sourceSegments = 0;
            long searchRecords = 0;
            long uploadBytes = 0;
            var fileCount = 0;
            for (var index = 0; index < publicationManifest.Documents.Count; index++)
            {
                var document = publicationManifest.Documents[index];
                var markdown = await provider.LoadAsync(document, cancellationSource.Token).ConfigureAwait(false);
                var formatted = formatter.FormatParts(document, markdown, fingerprint);
                fileCount += formatted.Count;
                sourceSegments += formatted.Sum(part => (long)part.SourceSegmentCount);
                searchRecords += formatted.Sum(part => (long)part.SearchRecordCount);
                uploadBytes += formatted.Sum(part => part.Content.LongLength);
                if ((index + 1) % 50 == 0 || index + 1 == publicationManifest.Documents.Count)
                {
                    Console.Error.WriteLine($"Validated {index + 1:N0}/{publicationManifest.Documents.Count:N0} documents.");
                }
            }
            WriteJson(new { corpusFingerprint = fingerprint, publicationManifest.DocumentCount, fileCount, sourceSegments, searchRecords, uploadBytes });
            return 0;
        }

        var endpointText = RequireOption(options, "endpoint", "AI__ProjectEndpoint");
        var tenantId = GetOption(options, "tenant-id") ?? Environment.GetEnvironmentVariable("AI__TenantId");
        var modelName = GetOption(options, "model") ?? Environment.GetEnvironmentVariable("AI__ModelName");
        using var httpClient = new HttpClient { Timeout = Timeout.InfiniteTimeSpan };
        Azure.Core.TokenCredential credential = command == "answer"
            ? new AzureCliCredential(new AzureCliCredentialOptions { TenantId = string.IsNullOrWhiteSpace(tenantId) ? null : tenantId })
            : new DefaultAzureCredential(new DefaultAzureCredentialOptions { TenantId = string.IsNullOrWhiteSpace(tenantId) ? null : tenantId });
        var client = new AzureOpenAIVectorStoreClient(new AzureOpenAIVectorStoreClientOptions { ProjectEndpoint = new Uri(endpointText, UriKind.Absolute), ModelName = modelName, Timeout = TimeSpan.FromMinutes(2) }, credential, httpClient);
        var publisher = new AzureOpenAIVectorStoreCorpusPublisher(client);

        switch (command)
        {
            case "answer":
                return await ConversationProbe.RunAsync(manifest, repositoryRoot, new Uri(endpointText), modelName ?? throw new ArgumentException("--model is required."), RequireOption(options, "vector-store-id", "AI__VectorStoreId"), credential, client, RequireOption(options, "question", null), GetOption(options, "sources") == "core", cancellationSource.Token).ConfigureAwait(false);
            case "publish":
            {
                var provider = new SefariaNormalizedDocumentProvider(new SefariaDocumentFileLoader(repositoryRoot));
                var name = GetOption(options, "name") ?? $"AskARabbi Sefaria Corpus {fingerprint[..12]}";
                var concurrency = ParseOptionalPositiveInt(options, "concurrency") ?? 4;
                var progress = new Progress<AzureOpenAIVectorStorePublicationProgress>(value => Console.Error.WriteLine($"{value.Stage}: {value.CompletedDocuments:N0}/{value.TotalDocuments:N0} documents, {value.CompletedFiles:N0}/{value.TotalFiles:N0} files, {value.SearchRecordCount:N0} records{(value.CurrentTitle is null ? string.Empty : $" - {value.CurrentTitle}")}"));
                var result = await publisher.PublishAsync(manifest, provider, name, maximumDocuments, concurrency, progress, cancellationSource.Token).ConfigureAwait(false);
                WriteJson(result);
                return 0;
            }
            case "resume":
            {
                var provider = new SefariaNormalizedDocumentProvider(new SefariaDocumentFileLoader(repositoryRoot));
                var vectorStoreId = RequireOption(options, "vector-store-id", "AI__VectorStoreId");
                var concurrency = ParseOptionalPositiveInt(options, "concurrency") ?? 4;
                var progress = new Progress<AzureOpenAIVectorStorePublicationProgress>(value => Console.Error.WriteLine($"{value.Stage}: {value.CompletedDocuments:N0}/{value.TotalDocuments:N0} documents, {value.CompletedFiles:N0}/{value.TotalFiles:N0} files, {value.SearchRecordCount:N0} records{(value.CurrentTitle is null ? string.Empty : $" - {value.CurrentTitle}")}"));
                var result = await publisher.ResumeAsync(manifest, provider, vectorStoreId, maximumDocuments, concurrency, progress, cancellationSource.Token).ConfigureAwait(false);
                WriteJson(result);
                return 0;
            }
            case "replace":
            {
                var provider = new SefariaNormalizedDocumentProvider(new SefariaDocumentFileLoader(repositoryRoot));
                var sourceVectorStoreId = RequireOption(options, "source-vector-store-id", "AI__VectorStoreId");
                var name = GetOption(options, "name") ?? $"AskARabbi Sefaria Corpus Replacement {fingerprint[..12]}";
                var concurrency = ParseOptionalPositiveInt(options, "concurrency") ?? 16;
                var progress = new Progress<AzureOpenAIVectorStorePublicationProgress>(value => Console.Error.WriteLine($"{value.Stage}: {value.CompletedDocuments:N0}/{value.TotalDocuments:N0} documents, {value.CompletedFiles:N0}/{value.TotalFiles:N0} files, {value.SearchRecordCount:N0} records{(value.CurrentTitle is null ? string.Empty : $" - {value.CurrentTitle}")}"));
                var result = await publisher.CreateCleanReplacementAsync(manifest, provider, sourceVectorStoreId, name, maximumDocuments, concurrency, progress, cancellationSource.Token).ConfigureAwait(false);
                WriteJson(result);
                return 0;
            }
            case "verify":
            {
                var vectorStoreId = RequireOption(options, "vector-store-id", "AI__VectorStoreId");
                var store = await publisher.VerifyAsync(vectorStoreId, fingerprint, publicationManifest.DocumentCount, cancellationSource.Token).ConfigureAwait(false);
                WriteJson(store);
                return 0;
            }
            case "inventory":
            {
                var vectorStoreId = RequireOption(options, "vector-store-id", "AI__VectorStoreId");
                var store = await client.GetAsync(vectorStoreId, cancellationSource.Token).ConfigureAwait(false);
                var storeFiles = await client.ListStoreFilesAsync(vectorStoreId, cancellationSource.Token).ConfigureAwait(false);
                var uploadedFileNames = await client.ListUploadedFileNamesAsync(cancellationSource.Token).ConfigureAwait(false);
                WriteJson(new
                {
                    store,
                    associationCount = storeFiles.Count,
                    nonCompletedFiles = storeFiles.Where(file => !string.Equals(file.Status, "completed", StringComparison.OrdinalIgnoreCase)).Select(file => new
                    {
                        file.FileId,
                        file.Status,
                        fileName = uploadedFileNames.GetValueOrDefault(file.FileId),
                    }),
                });
                return 0;
            }
            case "search":
            {
                var vectorStoreId = RequireOption(options, "vector-store-id", "AI__VectorStoreId");
                var query = RequireOption(options, "query", null);
                var page = await client.SearchAsync(vectorStoreId, new AzureOpenAIVectorStoreSearchRequest { Queries = [query], MaximumResults = 10 }, cancellationSource.Token).ConfigureAwait(false);
                WriteJson(page);
                return 0;
            }
            case "retrieve":
            {
                var vectorStoreId = RequireOption(options, "vector-store-id", "AI__VectorStoreId");
                var query = RequireOption(options, "query", null);
                var retriever = new AzureOpenAIVectorStoreRetriever(client, new AzureOpenAIVectorStoreRetrieverOptions
                {
                    VectorStoreId = vectorStoreId,
                    ExpectedCorpusFingerprint = fingerprint,
                }, publicationManifest);
                var hits = await retriever.SearchAsync(new SourceRetrievalQuery { QueryText = query, CandidateLimit = 10 }, cancellationSource.Token).ConfigureAwait(false);
                WriteJson(hits.Select(hit => new
                {
                    hit.Score,
                    hit.IsExactReference,
                    hit.Segment.SegmentId,
                    hit.Segment.CanonicalReference,
                    hit.Segment.Title,
                    hit.Segment.Language,
                    hit.Segment.Collection,
                    hit.Segment.License,
                    hit.Segment.Text,
                }));
                return 0;
            }
            default:
                throw new ArgumentException($"Unknown command '{command}'. Use 'help' for available commands.");
        }
    }
    catch (OperationCanceledException)
    {
        Console.Error.WriteLine("Operation cancelled.");
        return 2;
    }
    catch (Exception exception)
    {
        Console.Error.WriteLine($"Error: {exception.Message}");
        return 1;
    }
}

static Dictionary<string, string> ParseOptions(string[] values)
{
    var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
    for (var index = 0; index < values.Length; index += 2)
    {
        if (!values[index].StartsWith("--", StringComparison.Ordinal) || index + 1 >= values.Length)
        {
            throw new ArgumentException($"Expected '--name value' at argument {index + 2}.");
        }
        result.Add(values[index][2..], values[index + 1]);
    }
    return result;
}

static string? GetOption(IReadOnlyDictionary<string, string> options, string name) => options.TryGetValue(name, out var value) ? value : null;

static string RequireOption(IReadOnlyDictionary<string, string> options, string name, string? environmentName)
{
    var value = GetOption(options, name);
    if (string.IsNullOrWhiteSpace(value) && environmentName is not null)
    {
        value = Environment.GetEnvironmentVariable(environmentName);
    }
    return string.IsNullOrWhiteSpace(value) ? throw new ArgumentException($"--{name} is required{(environmentName is null ? "." : $" or set {environmentName}.")}") : value;
}

static int? ParseOptionalPositiveInt(IReadOnlyDictionary<string, string> options, string name)
{
    var text = GetOption(options, name);
    if (text is null)
    {
        return null;
    }
    return int.TryParse(text, out var value) && value > 0 ? value : throw new ArgumentException($"--{name} must be a positive integer.");
}

static void WriteJson<T>(T value) => Console.WriteLine(JsonSerializer.Serialize(value, new JsonSerializerOptions { WriteIndented = true }));

static void PrintHelp()
{
    Console.WriteLine("AskARabbi managed corpus publisher");
    Console.WriteLine();
    Console.WriteLine("Commands:");
    Console.WriteLine("  fingerprint [--manifest path] [--maximum-documents n]");
    Console.WriteLine("  validate [--manifest path] [--maximum-documents n]");
    Console.WriteLine("  bundle --output new-archive.zip [--manifest path]");
    Console.WriteLine("  read --reference canonical-reference");
    Console.WriteLine("  answer --endpoint uri --model deployment --vector-store-id id --question text [--sources core]");
    Console.WriteLine("  publish --endpoint uri [--name value] [--maximum-documents n] [--concurrency 1-16]");
    Console.WriteLine("  resume --endpoint uri --vector-store-id id [--maximum-documents n] [--concurrency 1-16]");
    Console.WriteLine("  replace --endpoint uri --source-vector-store-id id [--name value] [--maximum-documents n] [--concurrency 1-16]");
    Console.WriteLine("  verify --endpoint uri --vector-store-id id [--maximum-documents n]");
    Console.WriteLine("  inventory --endpoint uri --vector-store-id id");
    Console.WriteLine("  search --endpoint uri --model deployment --vector-store-id id --query text");
    Console.WriteLine("  retrieve --endpoint uri --model deployment --vector-store-id id --query text [--maximum-documents n]");
    Console.WriteLine();
    Console.WriteLine("Common options: --repository-root path --manifest path --tenant-id guid");
    Console.WriteLine("Environment fallbacks: AI__ProjectEndpoint, AI__ModelName, AI__VectorStoreId, AI__TenantId");
    Console.WriteLine("No API key or client secret is accepted; answer uses AzureCliCredential, other network commands use DefaultAzureCredential.");
}
