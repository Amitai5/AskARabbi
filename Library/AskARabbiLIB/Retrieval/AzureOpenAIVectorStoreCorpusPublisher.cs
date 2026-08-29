using AskARabbiLIB.Models;

namespace AskARabbiLIB.Retrieval;

/// <summary>Publishes a reproducible, fingerprinted corpus to a new Azure OpenAI vector store.</summary>
public sealed class AzureOpenAIVectorStoreCorpusPublisher
{
    private const int MaximumVectorStoreFiles = 10_000;
    private const int MaximumPollAttempts = 720;
    private static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(5);

    private readonly AzureOpenAIVectorStoreClient client;
    private readonly AzureOpenAIVectorStoreCorpusFormatter formatter;
    private readonly Func<TimeSpan, CancellationToken, Task> delayAsync;

    /// <summary>Creates a corpus publisher.</summary>
    /// <param name="client">Authenticated Azure vector-store client.</param>
    public AzureOpenAIVectorStoreCorpusPublisher(AzureOpenAIVectorStoreClient client) : this(client, new AzureOpenAIVectorStoreCorpusFormatter(), Task.Delay)
    {
    }

    internal AzureOpenAIVectorStoreCorpusPublisher(AzureOpenAIVectorStoreClient client, AzureOpenAIVectorStoreCorpusFormatter formatter, Func<TimeSpan, CancellationToken, Task> delayAsync)
    {
        ArgumentNullException.ThrowIfNull(client);
        ArgumentNullException.ThrowIfNull(formatter);
        ArgumentNullException.ThrowIfNull(delayAsync);
        this.client = client;
        this.formatter = formatter;
        this.delayAsync = delayAsync;
    }

    /// <summary>Creates a new store, uploads selected documents, waits for indexing, and verifies immutable metadata.</summary>
    /// <param name="manifest">Validated source manifest.</param>
    /// <param name="documentProvider">Checksum-verifying normalized document provider.</param>
    /// <param name="name">User-visible vector-store name.</param>
    /// <param name="maximumDocuments">Optional deterministic prefix used only for a pilot publication.</param>
    /// <param name="uploadConcurrency">Maximum concurrent Azure file uploads.</param>
    /// <param name="progress">Optional publication progress observer.</param>
    /// <param name="cancellationToken">Token used to cancel publication.</param>
    /// <returns>Completed publication identifiers, counts, fingerprint, and billable bytes.</returns>
    public async Task<AzureOpenAIVectorStorePublication> PublishAsync(DocumentManifest manifest, INormalizedDocumentProvider documentProvider, string name, int? maximumDocuments = null, int uploadConcurrency = 4, IProgress<AzureOpenAIVectorStorePublicationProgress>? progress = null, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(manifest);
        ArgumentNullException.ThrowIfNull(documentProvider);
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        if (maximumDocuments is <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(maximumDocuments), "Pilot document limit must be positive when supplied.");
        }
        if (uploadConcurrency is < 1 or > 16)
        {
            throw new ArgumentOutOfRangeException(nameof(uploadConcurrency), "Upload concurrency must be between 1 and 16.");
        }
        if (manifest.Documents is null || manifest.DocumentCount != manifest.Documents.Count || !string.Equals(manifest.SchemaVersion, ManifestLoader.SupportedSchemaVersion, StringComparison.Ordinal))
        {
            throw new ArgumentException("Manifest is incomplete or has an unsupported schema.", nameof(manifest));
        }

        var selectedDocuments = manifest.Documents.Take(maximumDocuments ?? manifest.DocumentCount).ToArray();
        if (selectedDocuments.Length == 0)
        {
            throw new ArgumentException("Manifest must contain at least one document.", nameof(manifest));
        }
        var publicationManifest = manifest with { DocumentCount = selectedDocuments.Length, Documents = selectedDocuments };
        var fingerprint = SourceIndexBuilder.ComputeCorpusFingerprint(publicationManifest);
        var expectedSegmentCount = selectedDocuments.Sum(document => (long)document.SegmentCount);
        var expectedFileCount = 0;
        long validatedSegmentCount = 0;
        long expectedSearchRecordCount = 0;
        for (var index = 0; index < selectedDocuments.Length; index++)
        {
            var document = selectedDocuments[index];
            var markdown = await documentProvider.LoadAsync(document, cancellationToken).ConfigureAwait(false);
            var corpusDocuments = formatter.FormatParts(document, markdown, fingerprint);
            expectedFileCount = checked(expectedFileCount + corpusDocuments.Count);
            validatedSegmentCount += corpusDocuments.Sum(part => (long)part.SourceSegmentCount);
            expectedSearchRecordCount += corpusDocuments.Sum(part => (long)part.SearchRecordCount);
            progress?.Report(new AzureOpenAIVectorStorePublicationProgress("Validating documents", index + 1, selectedDocuments.Length, expectedSearchRecordCount, document.FileTitle)
            {
                CompletedFiles = expectedFileCount,
                TotalFiles = expectedFileCount,
            });
        }
        if (validatedSegmentCount != expectedSegmentCount)
        {
            throw new InvalidDataException($"Managed corpus contains {validatedSegmentCount} parsed source segments but the manifest records {expectedSegmentCount}.");
        }
        if (expectedFileCount > MaximumVectorStoreFiles)
        {
            throw new InvalidDataException($"Managed corpus requires {expectedFileCount} files, exceeding Azure's {MaximumVectorStoreFiles} file limit per vector store.");
        }
        var storeMetadata = CreateStoreMetadata(publicationManifest, fingerprint, expectedSegmentCount, expectedFileCount);
        progress?.Report(new AzureOpenAIVectorStorePublicationProgress("Creating vector store", 0, selectedDocuments.Length, 0, null) { TotalFiles = expectedFileCount });
        var store = await client.CreateStoreAsync(name.Trim(), storeMetadata, cancellationToken).ConfigureAwait(false);

        long searchRecordCount = 0;
        var completedDocuments = 0;
        var completedFiles = 0;
        using var gate = new SemaphoreSlim(uploadConcurrency, uploadConcurrency);
        var uploadTasks = selectedDocuments.Select((document, index) => UploadDocumentAsync(document, index)).ToArray();
        await Task.WhenAll(uploadTasks).ConfigureAwait(false);
        if (searchRecordCount != expectedSearchRecordCount)
        {
            throw new InvalidDataException($"Managed corpus changed between validation and upload; expected {expectedSearchRecordCount} records but formatted {searchRecordCount}.");
        }

        if (completedFiles != expectedFileCount)
        {
            throw new InvalidDataException($"Managed corpus changed between validation and upload; expected {expectedFileCount} files but formatted {completedFiles}.");
        }

        var verifiedStore = await WaitForStoreAsync(store.Id, selectedDocuments.Length, expectedFileCount, searchRecordCount, progress, cancellationToken).ConfigureAwait(false);
        ValidateCompletedStore(verifiedStore, fingerprint, selectedDocuments.Length, expectedFileCount);
        progress?.Report(new AzureOpenAIVectorStorePublicationProgress("Completed", selectedDocuments.Length, selectedDocuments.Length, searchRecordCount, null) { CompletedFiles = expectedFileCount, TotalFiles = expectedFileCount });
        return new AzureOpenAIVectorStorePublication(store.Id, fingerprint, selectedDocuments.Length, expectedSegmentCount, searchRecordCount, verifiedStore.UsageBytes) { FileCount = expectedFileCount };

        async Task UploadDocumentAsync(ManifestDocument document, int index)
        {
            await gate.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                var markdown = await documentProvider.LoadAsync(document, cancellationToken).ConfigureAwait(false);
                var corpusDocuments = formatter.FormatParts(document, markdown, fingerprint);
                foreach (var corpusDocument in corpusDocuments)
                {
                    var fileId = await client.UploadFileAsync(corpusDocument, cancellationToken).ConfigureAwait(false);
                    await client.AttachFileAsync(store.Id, new AzureOpenAIVectorStoreUploadedFile(fileId, corpusDocument.Attributes), cancellationToken).ConfigureAwait(false);
                    Interlocked.Add(ref searchRecordCount, corpusDocument.SearchRecordCount);
                    Interlocked.Increment(ref completedFiles);
                }
                var completed = Interlocked.Increment(ref completedDocuments);
                progress?.Report(new AzureOpenAIVectorStorePublicationProgress("Uploading and attaching documents", completed, selectedDocuments.Length, Interlocked.Read(ref searchRecordCount), document.FileTitle)
                {
                    CompletedFiles = Volatile.Read(ref completedFiles),
                    TotalFiles = expectedFileCount,
                });
            }
            finally
            {
                gate.Release();
            }
        }
    }

    /// <summary>Resumes an interrupted publication by preserving completed deterministic files and replacing only failed or absent files.</summary>
    /// <param name="manifest">Validated source manifest.</param>
    /// <param name="documentProvider">Checksum-verifying normalized document provider.</param>
    /// <param name="vectorStoreId">Existing fingerprinted vector-store identifier.</param>
    /// <param name="maximumDocuments">Optional deterministic prefix used only for a pilot publication.</param>
    /// <param name="uploadConcurrency">Maximum concurrent Azure file uploads.</param>
    /// <param name="progress">Optional publication progress observer.</param>
    /// <param name="cancellationToken">Token used to cancel publication.</param>
    /// <returns>Completed publication identifiers, counts, fingerprint, and billable bytes.</returns>
    public async Task<AzureOpenAIVectorStorePublication> ResumeAsync(DocumentManifest manifest, INormalizedDocumentProvider documentProvider, string vectorStoreId, int? maximumDocuments = null, int uploadConcurrency = 4, IProgress<AzureOpenAIVectorStorePublicationProgress>? progress = null, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(manifest);
        ArgumentNullException.ThrowIfNull(documentProvider);
        ArgumentException.ThrowIfNullOrWhiteSpace(vectorStoreId);
        if (maximumDocuments is <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(maximumDocuments), "Pilot document limit must be positive when supplied.");
        }
        if (uploadConcurrency is < 1 or > 16)
        {
            throw new ArgumentOutOfRangeException(nameof(uploadConcurrency), "Upload concurrency must be between 1 and 16.");
        }
        var plan = await CreatePublicationPlanAsync(manifest, documentProvider, maximumDocuments, progress, cancellationToken).ConfigureAwait(false);
        var selectedDocuments = plan.Documents;
        var fingerprint = plan.Fingerprint;
        var expectedSegmentCount = plan.SegmentCount;
        var expectedParts = plan.Parts;
        var expectedSearchRecordCount = plan.SearchRecordCount;

        var store = await client.GetAsync(vectorStoreId, cancellationToken).ConfigureAwait(false);
        if (!string.Equals(store.Status, "completed", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidDataException($"Azure vector store '{store.Id}' cannot be resumed while its status is '{store.Status}'.");
        }
        ValidateStoreIdentity(store, fingerprint, selectedDocuments.Length, expectedParts.Count);
        progress?.Report(new AzureOpenAIVectorStorePublicationProgress("Inspecting existing files", 0, selectedDocuments.Length, 0, null) { TotalFiles = expectedParts.Count });
        var storeFiles = await client.ListStoreFilesAsync(vectorStoreId, cancellationToken).ConfigureAwait(false);
        var uploadedFileNames = await client.ListUploadedFileNamesAsync(cancellationToken).ConfigureAwait(false);
        var completedFileNames = new HashSet<string>(StringComparer.Ordinal);
        var failedFiles = new List<(AzureOpenAIVectorStoreFileEntry File, string FileName)>();
        foreach (var storeFile in storeFiles)
        {
            if (!uploadedFileNames.TryGetValue(storeFile.FileId, out var fileName))
            {
                throw new InvalidDataException($"Azure vector-store file '{storeFile.FileId}' is missing from the uploaded-file catalog.");
            }
            if (!expectedParts.ContainsKey(fileName))
            {
                throw new InvalidDataException($"Azure vector store contains unexpected file '{fileName}'.");
            }
            if (string.Equals(storeFile.Status, "completed", StringComparison.OrdinalIgnoreCase))
            {
                if (!completedFileNames.Add(fileName))
                {
                    throw new InvalidDataException($"Azure vector store contains duplicate completed file '{fileName}'.");
                }
            }
            else if (string.Equals(storeFile.Status, "failed", StringComparison.OrdinalIgnoreCase) || string.Equals(storeFile.Status, "cancelled", StringComparison.OrdinalIgnoreCase))
            {
                failedFiles.Add((storeFile, fileName));
            }
            else
            {
                throw new InvalidDataException($"Azure vector-store file '{fileName}' cannot be resumed while its status is '{storeFile.Status}'.");
            }
        }

        foreach (var failedFile in failedFiles)
        {
            progress?.Report(new AzureOpenAIVectorStorePublicationProgress("Removing failed file", 0, selectedDocuments.Length, 0, failedFile.FileName)
            {
                CompletedFiles = completedFileNames.Count,
                TotalFiles = expectedParts.Count,
            });
            await client.DeleteStoreFileAsync(vectorStoreId, failedFile.File.FileId, cancellationToken).ConfigureAwait(false);
        }

        var missingFileNames = expectedParts.Keys.Where(fileName => !completedFileNames.Contains(fileName)).ToHashSet(StringComparer.Ordinal);
        long searchRecordCount = completedFileNames.Sum(fileName => (long)expectedParts[fileName].SearchRecordCount);
        var completedFiles = completedFileNames.Count;
        var completedDocuments = expectedParts.Values.GroupBy(part => part.Document.DocumentId, StringComparer.Ordinal).Count(group => group.All(part => completedFileNames.Contains(part.FileName)));
        progress?.Report(new AzureOpenAIVectorStorePublicationProgress("Resuming missing files", completedDocuments, selectedDocuments.Length, searchRecordCount, null)
        {
            CompletedFiles = completedFiles,
            TotalFiles = expectedParts.Count,
        });

        using var gate = new SemaphoreSlim(uploadConcurrency, uploadConcurrency);
        var documentsToResume = selectedDocuments.Where(document => expectedParts.Values.Any(part => string.Equals(part.Document.DocumentId, document.DocumentId, StringComparison.Ordinal) && missingFileNames.Contains(part.FileName))).ToArray();
        var uploadTasks = documentsToResume.Select(UploadDocumentAsync).ToArray();
        await Task.WhenAll(uploadTasks).ConfigureAwait(false);
        if (searchRecordCount != expectedSearchRecordCount || completedFiles != expectedParts.Count)
        {
            throw new InvalidDataException($"Managed corpus resume completed {completedFiles} of {expectedParts.Count} files and {searchRecordCount} of {expectedSearchRecordCount} records.");
        }

        var verifiedStore = await WaitForStoreAsync(vectorStoreId, selectedDocuments.Length, expectedParts.Count, searchRecordCount, progress, cancellationToken).ConfigureAwait(false);
        ValidateCompletedStore(verifiedStore, fingerprint, selectedDocuments.Length, expectedParts.Count);
        progress?.Report(new AzureOpenAIVectorStorePublicationProgress("Completed", selectedDocuments.Length, selectedDocuments.Length, searchRecordCount, null) { CompletedFiles = expectedParts.Count, TotalFiles = expectedParts.Count });
        return new AzureOpenAIVectorStorePublication(vectorStoreId, fingerprint, selectedDocuments.Length, expectedSegmentCount, expectedSearchRecordCount, verifiedStore.UsageBytes) { FileCount = expectedParts.Count };

        async Task UploadDocumentAsync(ManifestDocument sourceDocument)
        {
            await gate.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                var markdown = await documentProvider.LoadAsync(sourceDocument, cancellationToken).ConfigureAwait(false);
                var corpusDocuments = formatter.FormatParts(sourceDocument, markdown, fingerprint);
                foreach (var corpusDocument in corpusDocuments.Where(part => missingFileNames.Contains(part.FileName)))
                {
                    var fileId = await client.UploadFileAsync(corpusDocument, cancellationToken).ConfigureAwait(false);
                    await client.AttachFileAsync(vectorStoreId, new AzureOpenAIVectorStoreUploadedFile(fileId, corpusDocument.Attributes), cancellationToken).ConfigureAwait(false);
                    Interlocked.Add(ref searchRecordCount, corpusDocument.SearchRecordCount);
                    Interlocked.Increment(ref completedFiles);
                }
                var completed = Interlocked.Increment(ref completedDocuments);
                progress?.Report(new AzureOpenAIVectorStorePublicationProgress("Uploading and attaching missing files", completed, selectedDocuments.Length, Interlocked.Read(ref searchRecordCount), sourceDocument.FileTitle)
                {
                    CompletedFiles = Volatile.Read(ref completedFiles),
                    TotalFiles = expectedParts.Count,
                });
            }
            finally
            {
                gate.Release();
            }
        }
    }

    /// <summary>Creates a clean replacement store by reusing every completed uploaded file from a matching source store.</summary>
    /// <param name="manifest">Validated source manifest.</param>
    /// <param name="documentProvider">Checksum-verifying normalized document provider.</param>
    /// <param name="sourceVectorStoreId">Existing fingerprinted vector-store identifier whose completed files are reused.</param>
    /// <param name="replacementName">User-visible name for the clean replacement store.</param>
    /// <param name="maximumDocuments">Optional deterministic prefix used only for a pilot publication.</param>
    /// <param name="attachConcurrency">Maximum concurrent Azure file attachments.</param>
    /// <param name="progress">Optional publication progress observer.</param>
    /// <param name="cancellationToken">Token used to cancel publication.</param>
    /// <returns>Completed replacement identifiers, counts, fingerprint, and billable bytes.</returns>
    public async Task<AzureOpenAIVectorStorePublication> CreateCleanReplacementAsync(DocumentManifest manifest, INormalizedDocumentProvider documentProvider, string sourceVectorStoreId, string replacementName, int? maximumDocuments = null, int attachConcurrency = 16, IProgress<AzureOpenAIVectorStorePublicationProgress>? progress = null, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(manifest);
        ArgumentNullException.ThrowIfNull(documentProvider);
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceVectorStoreId);
        ArgumentException.ThrowIfNullOrWhiteSpace(replacementName);
        if (maximumDocuments is <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(maximumDocuments), "Pilot document limit must be positive when supplied.");
        }
        if (attachConcurrency is < 1 or > 16)
        {
            throw new ArgumentOutOfRangeException(nameof(attachConcurrency), "Attachment concurrency must be between 1 and 16.");
        }

        var plan = await CreatePublicationPlanAsync(manifest, documentProvider, maximumDocuments, progress, cancellationToken).ConfigureAwait(false);
        var sourceStore = await client.GetAsync(sourceVectorStoreId, cancellationToken).ConfigureAwait(false);
        if (!string.Equals(sourceStore.Status, "completed", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidDataException($"Azure vector store '{sourceStore.Id}' cannot be replaced while its status is '{sourceStore.Status}'.");
        }
        ValidateStoreIdentity(sourceStore, plan.Fingerprint, plan.Documents.Length, plan.Parts.Count);

        progress?.Report(new AzureOpenAIVectorStorePublicationProgress("Inspecting reusable files", 0, plan.Documents.Length, 0, null) { TotalFiles = plan.Parts.Count });
        var storeFiles = await client.ListStoreFilesAsync(sourceVectorStoreId, cancellationToken).ConfigureAwait(false);
        var uploadedFileNames = await client.ListUploadedFileNamesAsync(cancellationToken).ConfigureAwait(false);
        var completedFileIds = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var storeFile in storeFiles)
        {
            if (string.Equals(storeFile.Status, "failed", StringComparison.OrdinalIgnoreCase) || string.Equals(storeFile.Status, "cancelled", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }
            if (!string.Equals(storeFile.Status, "completed", StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidDataException($"Azure vector-store file '{storeFile.FileId}' cannot be reused while its status is '{storeFile.Status}'.");
            }
            if (!uploadedFileNames.TryGetValue(storeFile.FileId, out var fileName))
            {
                throw new InvalidDataException($"Completed Azure vector-store file '{storeFile.FileId}' is missing from the uploaded-file catalog.");
            }
            if (!plan.Parts.ContainsKey(fileName))
            {
                throw new InvalidDataException($"Azure vector store contains unexpected completed file '{fileName}'.");
            }
            if (!completedFileIds.TryAdd(fileName, storeFile.FileId))
            {
                throw new InvalidDataException($"Azure vector store contains duplicate completed file '{fileName}'.");
            }
        }

        if (completedFileIds.Count != plan.Parts.Count)
        {
            var missing = plan.Parts.Keys.Where(fileName => !completedFileIds.ContainsKey(fileName)).Take(3).ToArray();
            throw new InvalidDataException($"Azure vector store contains {completedFileIds.Count} reusable completed files; expected {plan.Parts.Count}. Missing: {string.Join(", ", missing)}.");
        }

        var metadata = CreateStoreMetadata(plan.Manifest, plan.Fingerprint, plan.SegmentCount, plan.Parts.Count);
        progress?.Report(new AzureOpenAIVectorStorePublicationProgress("Creating clean vector store", 0, plan.Documents.Length, 0, null) { TotalFiles = plan.Parts.Count });
        var replacementStore = await client.CreateStoreAsync(replacementName.Trim(), metadata, cancellationToken).ConfigureAwait(false);
        var partsByDocumentId = plan.Parts.Values.GroupBy(part => part.Document.DocumentId, StringComparer.Ordinal).ToDictionary(group => group.Key, group => group.ToArray(), StringComparer.Ordinal);
        long searchRecordCount = 0;
        var completedFiles = 0;
        var completedDocuments = 0;
        using var gate = new SemaphoreSlim(attachConcurrency, attachConcurrency);
        var attachTasks = plan.Documents.Select(AttachDocumentAsync).ToArray();
        await Task.WhenAll(attachTasks).ConfigureAwait(false);
        if (completedFiles != plan.Parts.Count || searchRecordCount != plan.SearchRecordCount)
        {
            throw new InvalidDataException($"Clean replacement attached {completedFiles} of {plan.Parts.Count} files and {searchRecordCount} of {plan.SearchRecordCount} records.");
        }

        var verifiedStore = await WaitForStoreAsync(replacementStore.Id, plan.Documents.Length, plan.Parts.Count, searchRecordCount, progress, cancellationToken).ConfigureAwait(false);
        ValidateCompletedStore(verifiedStore, plan.Fingerprint, plan.Documents.Length, plan.Parts.Count);
        progress?.Report(new AzureOpenAIVectorStorePublicationProgress("Completed", plan.Documents.Length, plan.Documents.Length, searchRecordCount, null) { CompletedFiles = plan.Parts.Count, TotalFiles = plan.Parts.Count });
        return new AzureOpenAIVectorStorePublication(replacementStore.Id, plan.Fingerprint, plan.Documents.Length, plan.SegmentCount, plan.SearchRecordCount, verifiedStore.UsageBytes) { FileCount = plan.Parts.Count };

        async Task AttachDocumentAsync(ManifestDocument document)
        {
            await gate.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                foreach (var part in partsByDocumentId[document.DocumentId])
                {
                    await client.AttachFileAsync(replacementStore.Id, new AzureOpenAIVectorStoreUploadedFile(completedFileIds[part.FileName], part.Attributes), cancellationToken).ConfigureAwait(false);
                    Interlocked.Add(ref searchRecordCount, part.SearchRecordCount);
                    Interlocked.Increment(ref completedFiles);
                }
                var completed = Interlocked.Increment(ref completedDocuments);
                progress?.Report(new AzureOpenAIVectorStorePublicationProgress("Attaching reusable files", completed, plan.Documents.Length, Interlocked.Read(ref searchRecordCount), document.FileTitle)
                {
                    CompletedFiles = Volatile.Read(ref completedFiles),
                    TotalFiles = plan.Parts.Count,
                });
            }
            finally
            {
                gate.Release();
            }
        }
    }

    /// <summary>Verifies that an existing store is completed and matches an expected corpus.</summary>
    /// <param name="vectorStoreId">Provider vector-store identifier.</param>
    /// <param name="expectedCorpusFingerprint">Expected lowercase corpus fingerprint.</param>
    /// <param name="expectedDocumentCount">Expected indexed document count.</param>
    /// <param name="cancellationToken">Token used to cancel verification.</param>
    /// <returns>Current verified store information.</returns>
    public async Task<AzureOpenAIVectorStoreInfo> VerifyAsync(string vectorStoreId, string expectedCorpusFingerprint, int expectedDocumentCount, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(vectorStoreId);
        ArgumentException.ThrowIfNullOrWhiteSpace(expectedCorpusFingerprint);
        if (expectedDocumentCount < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(expectedDocumentCount), "Expected document count must be positive.");
        }
        var store = await client.GetAsync(vectorStoreId, cancellationToken).ConfigureAwait(false);
        ValidateCompletedStore(store, expectedCorpusFingerprint, expectedDocumentCount, null);
        return store;
    }

    private async Task<AzureOpenAIVectorStoreInfo> WaitForStoreAsync(string vectorStoreId, int totalDocuments, int totalFiles, long searchRecordCount, IProgress<AzureOpenAIVectorStorePublicationProgress>? progress, CancellationToken cancellationToken)
    {
        for (var attempt = 0; attempt < MaximumPollAttempts; attempt++)
        {
            var store = await client.GetAsync(vectorStoreId, cancellationToken).ConfigureAwait(false);
            if (store.FailedFileCount > 0 || string.Equals(store.Status, "failed", StringComparison.OrdinalIgnoreCase) || string.Equals(store.Status, "cancelled", StringComparison.OrdinalIgnoreCase))
            {
                return store;
            }
            if (string.Equals(store.Status, "completed", StringComparison.OrdinalIgnoreCase) && store.CompletedFileCount == totalFiles)
            {
                return store;
            }

            progress?.Report(new AzureOpenAIVectorStorePublicationProgress($"Indexing files ({store.CompletedFileCount}/{totalFiles})", Math.Min(store.CompletedFileCount, totalDocuments), totalDocuments, searchRecordCount, null)
            {
                CompletedFiles = store.CompletedFileCount,
                TotalFiles = totalFiles,
            });
            await delayAsync(PollInterval, cancellationToken).ConfigureAwait(false);
        }

        throw new TimeoutException("Azure vector-store file indexing did not finish within one hour.");
    }

    private async Task<PublicationPlan> CreatePublicationPlanAsync(DocumentManifest manifest, INormalizedDocumentProvider documentProvider, int? maximumDocuments, IProgress<AzureOpenAIVectorStorePublicationProgress>? progress, CancellationToken cancellationToken)
    {
        if (manifest.Documents is null || manifest.DocumentCount != manifest.Documents.Count || !string.Equals(manifest.SchemaVersion, ManifestLoader.SupportedSchemaVersion, StringComparison.Ordinal))
        {
            throw new ArgumentException("Manifest is incomplete or has an unsupported schema.", nameof(manifest));
        }

        var selectedDocuments = manifest.Documents.Take(maximumDocuments ?? manifest.DocumentCount).ToArray();
        if (selectedDocuments.Length == 0)
        {
            throw new ArgumentException("Manifest must contain at least one document.", nameof(manifest));
        }
        var publicationManifest = manifest with { DocumentCount = selectedDocuments.Length, Documents = selectedDocuments };
        var fingerprint = SourceIndexBuilder.ComputeCorpusFingerprint(publicationManifest);
        var expectedSegmentCount = selectedDocuments.Sum(document => (long)document.SegmentCount);
        var expectedParts = new Dictionary<string, ResumePart>(StringComparer.Ordinal);
        long validatedSegmentCount = 0;
        long expectedSearchRecordCount = 0;
        for (var index = 0; index < selectedDocuments.Length; index++)
        {
            var sourceDocument = selectedDocuments[index];
            var markdown = await documentProvider.LoadAsync(sourceDocument, cancellationToken).ConfigureAwait(false);
            var corpusDocuments = formatter.FormatParts(sourceDocument, markdown, fingerprint);
            foreach (var corpusDocument in corpusDocuments)
            {
                if (!expectedParts.TryAdd(corpusDocument.FileName, new ResumePart(corpusDocument.FileName, sourceDocument, corpusDocument.Attributes, corpusDocument.SearchRecordCount)))
                {
                    throw new InvalidDataException($"Managed corpus produced duplicate deterministic filename '{corpusDocument.FileName}'.");
                }
                validatedSegmentCount += corpusDocument.SourceSegmentCount;
                expectedSearchRecordCount += corpusDocument.SearchRecordCount;
            }
            progress?.Report(new AzureOpenAIVectorStorePublicationProgress("Validating documents", index + 1, selectedDocuments.Length, expectedSearchRecordCount, sourceDocument.FileTitle)
            {
                CompletedFiles = expectedParts.Count,
                TotalFiles = expectedParts.Count,
            });
        }
        if (validatedSegmentCount != expectedSegmentCount)
        {
            throw new InvalidDataException($"Managed corpus contains {validatedSegmentCount} parsed source segments but the manifest records {expectedSegmentCount}.");
        }
        if (expectedParts.Count > MaximumVectorStoreFiles)
        {
            throw new InvalidDataException($"Managed corpus requires {expectedParts.Count} files, exceeding Azure's {MaximumVectorStoreFiles} file limit per vector store.");
        }
        return new PublicationPlan(publicationManifest, selectedDocuments, fingerprint, expectedSegmentCount, expectedSearchRecordCount, expectedParts);
    }

    private static IReadOnlyDictionary<string, string> CreateStoreMetadata(DocumentManifest manifest, string fingerprint, long segmentCount, int fileCount) => new Dictionary<string, string>(StringComparer.Ordinal)
    {
        [AzureOpenAIVectorStoreCorpusContract.StoreSchemaMetadata] = AzureOpenAIVectorStoreCorpusContract.StoreSchemaVersion,
        [AzureOpenAIVectorStoreCorpusContract.StoreFingerprintMetadata] = fingerprint,
        [AzureOpenAIVectorStoreCorpusContract.StoreManifestSchemaMetadata] = manifest.SchemaVersion,
        [AzureOpenAIVectorStoreCorpusContract.StoreDocumentCountMetadata] = manifest.DocumentCount.ToString(System.Globalization.CultureInfo.InvariantCulture),
        [AzureOpenAIVectorStoreCorpusContract.StoreFileCountMetadata] = fileCount.ToString(System.Globalization.CultureInfo.InvariantCulture),
        [AzureOpenAIVectorStoreCorpusContract.StoreSegmentCountMetadata] = segmentCount.ToString(System.Globalization.CultureInfo.InvariantCulture),
        [AzureOpenAIVectorStoreCorpusContract.StoreSourceProviderMetadata] = manifest.SourceProvider,
    };

    private static void ValidateCompletedStore(AzureOpenAIVectorStoreInfo store, string expectedFingerprint, int expectedDocumentCount, int? expectedFileCount)
    {
        if (!string.Equals(store.Status, "completed", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidDataException($"Azure vector store '{store.Id}' is not ready; current status is '{store.Status}'.");
        }
        ValidateStoreIdentity(store, expectedFingerprint, expectedDocumentCount, expectedFileCount);
        var fileCount = int.Parse(store.Metadata[AzureOpenAIVectorStoreCorpusContract.StoreFileCountMetadata], System.Globalization.CultureInfo.InvariantCulture);
        if (store.CompletedFileCount != fileCount || store.FailedFileCount != 0)
        {
            throw new InvalidDataException($"Azure vector store contains {store.CompletedFileCount} completed and {store.FailedFileCount} failed files; expected {fileCount} completed files.");
        }
    }

    private static void ValidateStoreIdentity(AzureOpenAIVectorStoreInfo store, string expectedFingerprint, int expectedDocumentCount, int? expectedFileCount)
    {
        if (!store.Metadata.TryGetValue(AzureOpenAIVectorStoreCorpusContract.StoreSchemaMetadata, out var schema) || !string.Equals(schema, AzureOpenAIVectorStoreCorpusContract.StoreSchemaVersion, StringComparison.Ordinal))
        {
            throw new InvalidDataException("Azure vector-store schema is missing or stale.");
        }
        if (!store.Metadata.TryGetValue(AzureOpenAIVectorStoreCorpusContract.StoreFingerprintMetadata, out var fingerprint) || !string.Equals(fingerprint, expectedFingerprint, StringComparison.Ordinal))
        {
            throw new InvalidDataException("Azure vector-store fingerprint does not match the expected corpus.");
        }
        if (!store.Metadata.TryGetValue(AzureOpenAIVectorStoreCorpusContract.StoreDocumentCountMetadata, out var documentCountText) || !int.TryParse(documentCountText, System.Globalization.NumberStyles.None, System.Globalization.CultureInfo.InvariantCulture, out var documentCount) || documentCount != expectedDocumentCount)
        {
            throw new InvalidDataException($"Azure vector store records {documentCountText ?? "no"} logical documents; expected {expectedDocumentCount}.");
        }
        if (!store.Metadata.TryGetValue(AzureOpenAIVectorStoreCorpusContract.StoreFileCountMetadata, out var fileCountText) || !int.TryParse(fileCountText, System.Globalization.NumberStyles.None, System.Globalization.CultureInfo.InvariantCulture, out var fileCount) || fileCount < documentCount || expectedFileCount is not null && fileCount != expectedFileCount.Value)
        {
            throw new InvalidDataException("Azure vector-store file-count metadata is missing or inconsistent.");
        }
    }

    private sealed record ResumePart(string FileName, ManifestDocument Document, IReadOnlyDictionary<string, string> Attributes, int SearchRecordCount);

    private sealed record PublicationPlan(DocumentManifest Manifest, ManifestDocument[] Documents, string Fingerprint, long SegmentCount, long SearchRecordCount, IReadOnlyDictionary<string, ResumePart> Parts);
}
