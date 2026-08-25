using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using AskARabbiLIB.Models;
using AskARabbiLIB.Search;
using Microsoft.Data.Sqlite;

namespace AskARabbiLIB.Retrieval;

/// <summary>Builds and verifies the reproducible SQLite FTS5 segment index.</summary>
public sealed class SourceIndexBuilder
{
    /// <summary>Gets the SQLite index schema version produced and accepted by this build.</summary>
    public const string IndexSchemaVersion = "3";

    /// <summary>Gets the repository-relative path of the reproducible local segment index.</summary>
    public const string DefaultRelativePath = "Data/NormalizedData/Sefaria/Metadata/segment-search-v3.sqlite";

    private const string CreateSchemaSql = """
        PRAGMA foreign_keys = ON;
        CREATE TABLE metadata (
            key TEXT NOT NULL PRIMARY KEY,
            value TEXT NOT NULL
        );
        CREATE TABLE documents (
            document_id TEXT NOT NULL PRIMARY KEY,
            title TEXT NOT NULL,
            hebrew_title TEXT NOT NULL,
            language TEXT NOT NULL,
            language_code TEXT NOT NULL,
            collection TEXT NOT NULL,
            categories_json TEXT NOT NULL,
            version_title TEXT NOT NULL,
            license TEXT NOT NULL,
            source_url TEXT NOT NULL,
            file_path TEXT NOT NULL,
            work_key TEXT NULL,
            usage_note TEXT NULL,
            CHECK ((work_key IS NULL AND usage_note IS NULL) OR (work_key IS NOT NULL AND usage_note IS NOT NULL))
        );
        CREATE TABLE document_categories (
            document_id TEXT NOT NULL,
            category TEXT NOT NULL COLLATE NOCASE,
            PRIMARY KEY (document_id, category),
            FOREIGN KEY (document_id) REFERENCES documents(document_id) ON DELETE CASCADE
        );
        CREATE TABLE segments (
            row_id INTEGER NOT NULL PRIMARY KEY,
            segment_id TEXT NOT NULL UNIQUE,
            document_id TEXT NOT NULL,
            canonical_reference TEXT NOT NULL,
            canonical_reference_normalized TEXT NOT NULL,
            document_ordinal INTEGER NOT NULL,
            text TEXT NOT NULL,
            FOREIGN KEY (document_id) REFERENCES documents(document_id) ON DELETE CASCADE,
            UNIQUE (document_id, document_ordinal)
        );
        CREATE INDEX ix_segments_reference ON segments(canonical_reference_normalized);
        CREATE INDEX ix_segments_document_ordinal ON segments(document_id, document_ordinal);
        CREATE INDEX ix_documents_language ON documents(language COLLATE NOCASE);
        CREATE INDEX ix_documents_language_code ON documents(language_code COLLATE NOCASE);
        CREATE INDEX ix_documents_collection ON documents(collection COLLATE NOCASE);
        CREATE INDEX ix_documents_work_key ON documents(work_key COLLATE NOCASE);
        CREATE VIRTUAL TABLE segment_search USING fts5(
            normalized_text,
            canonical_reference,
            title,
            hebrew_title,
            version_title,
            content = '',
            tokenize = 'unicode61 remove_diacritics 2'
        );
        """;

    /// <summary>Builds an index into a temporary file and atomically replaces the requested index.</summary>
    /// <param name="manifest">Validated manifest defining the corpus.</param>
    /// <param name="documentProvider">Provider for checksum-verified normalized Markdown.</param>
    /// <param name="indexPath">Destination SQLite file.</param>
    /// <param name="progress">Optional progress observer.</param>
    /// <param name="cancellationToken">Token used to cancel the build.</param>
    /// <returns>Statistics for the completed index.</returns>
    public async Task<SourceIndexStatistics> BuildAsync(DocumentManifest manifest, INormalizedDocumentProvider documentProvider, string indexPath, IProgress<SourceIndexProgress>? progress = null, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(manifest);
        ArgumentNullException.ThrowIfNull(documentProvider);
        ArgumentException.ThrowIfNullOrWhiteSpace(indexPath);

        var fullIndexPath = Path.GetFullPath(indexPath);
        var directory = Path.GetDirectoryName(fullIndexPath) ?? throw new ArgumentException("The index path must have a parent directory.", nameof(indexPath));
        Directory.CreateDirectory(directory);
        var temporaryPath = Path.Combine(directory, $".{Path.GetFileName(fullIndexPath)}.{Guid.NewGuid():N}.tmp");
        try
        {
            var connectionString = new SqliteConnectionStringBuilder
            {
                DataSource = temporaryPath,
                Mode = SqliteOpenMode.ReadWriteCreate,
                Cache = SqliteCacheMode.Private,
                Pooling = false,
            }.ToString();
            await using (var connection = new SqliteConnection(connectionString))
            {
                await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
                await ConfigureBuildConnectionAsync(connection, cancellationToken).ConfigureAwait(false);
                await BuildAsync(manifest, documentProvider, connection, progress, cancellationToken).ConfigureAwait(false);
            }

            var verification = await VerifyAsync(temporaryPath, manifest, cancellationToken).ConfigureAwait(false);
            if (!verification.IsValid || verification.Statistics is null)
            {
                throw new InvalidDataException($"The completed segment index failed verification: {verification.Message}");
            }
            File.Move(temporaryPath, fullIndexPath, true);
            return verification.Statistics;
        }
        finally
        {
            if (File.Exists(temporaryPath))
            {
                File.Delete(temporaryPath);
            }
        }
    }

    /// <summary>Builds an index in an existing SQLite connection, primarily for hosts and hermetic tests.</summary>
    /// <param name="manifest">Validated manifest defining the corpus.</param>
    /// <param name="documentProvider">Provider for normalized Markdown.</param>
    /// <param name="connection">Open or unopened writable SQLite connection.</param>
    /// <param name="progress">Optional progress observer.</param>
    /// <param name="cancellationToken">Token used to cancel the build.</param>
    /// <returns>Statistics for the completed index.</returns>
    public async Task<SourceIndexStatistics> BuildAsync(DocumentManifest manifest, INormalizedDocumentProvider documentProvider, SqliteConnection connection, IProgress<SourceIndexProgress>? progress = null, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(manifest);
        ArgumentNullException.ThrowIfNull(documentProvider);
        ArgumentNullException.ThrowIfNull(connection);
        ValidateManifest(manifest);
        await EnsureOpenAsync(connection, cancellationToken).ConfigureAwait(false);

        await ResetSchemaAsync(connection, cancellationToken).ConfigureAwait(false);
        using var transaction = connection.BeginTransaction();
        await using var insertDocument = CreateInsertDocumentCommand(connection, transaction);
        await using var insertCategory = CreateInsertCategoryCommand(connection, transaction);
        await using var insertSegment = CreateInsertSegmentCommand(connection, transaction);
        await using var insertSearch = CreateInsertSearchCommand(connection, transaction);

        long segmentCount = 0;
        var parser = new NormalizedMarkdownSegmentParser();
        for (var documentIndex = 0; documentIndex < manifest.Documents.Count; documentIndex++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var document = manifest.Documents[documentIndex];
            var markdown = await documentProvider.LoadAsync(document, cancellationToken).ConfigureAwait(false);
            var segments = parser.Parse(document, markdown);
            await InsertDocumentAsync(insertDocument, document, cancellationToken).ConfigureAwait(false);
            foreach (var category in document.Categories.Distinct(StringComparer.OrdinalIgnoreCase))
            {
                await InsertCategoryAsync(insertCategory, document.DocumentId, category, cancellationToken).ConfigureAwait(false);
            }
            foreach (var segment in segments)
            {
                segmentCount++;
                await InsertSegmentAsync(insertSegment, segmentCount, segment, cancellationToken).ConfigureAwait(false);
                await InsertSearchAsync(insertSearch, segmentCount, segment, cancellationToken).ConfigureAwait(false);
            }
            progress?.Report(new SourceIndexProgress(documentIndex + 1, manifest.Documents.Count, segmentCount, document.FileTitle));
        }

        var fingerprint = ComputeCorpusFingerprint(manifest);
        await InsertMetadataAsync(connection, transaction, "indexSchemaVersion", IndexSchemaVersion, cancellationToken).ConfigureAwait(false);
        await InsertMetadataAsync(connection, transaction, "manifestSchemaVersion", manifest.SchemaVersion, cancellationToken).ConfigureAwait(false);
        await InsertMetadataAsync(connection, transaction, "sourceProvider", manifest.SourceProvider, cancellationToken).ConfigureAwait(false);
        await InsertMetadataAsync(connection, transaction, "corpusFingerprint", fingerprint, cancellationToken).ConfigureAwait(false);
        await InsertMetadataAsync(connection, transaction, "normalizedManifestSha256", manifest.SourceManifests.NormalizedSha256.ToLowerInvariant(), cancellationToken).ConfigureAwait(false);
        await InsertMetadataAsync(connection, transaction, "documentCount", manifest.DocumentCount.ToString(System.Globalization.CultureInfo.InvariantCulture), cancellationToken).ConfigureAwait(false);
        await InsertMetadataAsync(connection, transaction, "segmentCount", segmentCount.ToString(System.Globalization.CultureInfo.InvariantCulture), cancellationToken).ConfigureAwait(false);
        transaction.Commit();

        await using var optimize = connection.CreateCommand();
        optimize.CommandText = "PRAGMA optimize;";
        await optimize.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        return new SourceIndexStatistics(IndexSchemaVersion, fingerprint, manifest.DocumentCount, segmentCount, GetConnectionFileSize(connection));
    }

    /// <summary>Verifies an on-disk index against the current manifest and corpus fingerprint.</summary>
    /// <param name="indexPath">SQLite index path.</param>
    /// <param name="manifest">Current validated manifest.</param>
    /// <param name="cancellationToken">Token used to cancel verification.</param>
    /// <returns>A validation result with statistics when the index is current.</returns>
    public async Task<SourceIndexVerification> VerifyAsync(string indexPath, DocumentManifest manifest, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(indexPath);
        ArgumentNullException.ThrowIfNull(manifest);
        var fullPath = Path.GetFullPath(indexPath);
        if (!File.Exists(fullPath))
        {
            return new SourceIndexVerification(false, $"Segment index does not exist: {fullPath}", null);
        }

        try
        {
            var connectionString = new SqliteConnectionStringBuilder
            {
                DataSource = fullPath,
                Mode = SqliteOpenMode.ReadOnly,
                Cache = SqliteCacheMode.Shared,
                Pooling = false,
            }.ToString();
            await using var connection = new SqliteConnection(connectionString);
            await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
            var result = await VerifyAsync(connection, manifest, cancellationToken).ConfigureAwait(false);
            return result.Statistics is null ? result : result with { Statistics = result.Statistics with { FileSizeBytes = new FileInfo(fullPath).Length } };
        }
        catch (SqliteException exception)
        {
            return new SourceIndexVerification(false, $"Segment index could not be read: {exception.Message}", null);
        }
    }

    /// <summary>Verifies an index in an existing SQLite connection.</summary>
    /// <param name="connection">Open or unopened SQLite connection.</param>
    /// <param name="manifest">Current manifest.</param>
    /// <param name="cancellationToken">Token used to cancel verification.</param>
    /// <returns>A validation result with statistics when the index is current.</returns>
    public async Task<SourceIndexVerification> VerifyAsync(SqliteConnection connection, DocumentManifest manifest, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(connection);
        ArgumentNullException.ThrowIfNull(manifest);
        ValidateManifest(manifest);
        await EnsureOpenAsync(connection, cancellationToken).ConfigureAwait(false);
        try
        {
            var metadata = await ReadMetadataAsync(connection, cancellationToken).ConfigureAwait(false);
            if (!metadata.TryGetValue("indexSchemaVersion", out var schemaVersion) || !string.Equals(schemaVersion, IndexSchemaVersion, StringComparison.Ordinal))
            {
                return new SourceIndexVerification(false, $"Segment index schema is missing or stale; expected {IndexSchemaVersion}.", null);
            }
            var expectedFingerprint = ComputeCorpusFingerprint(manifest);
            if (!metadata.TryGetValue("corpusFingerprint", out var fingerprint) || !string.Equals(fingerprint, expectedFingerprint, StringComparison.Ordinal))
            {
                return new SourceIndexVerification(false, "Segment index corpus fingerprint does not match the current manifest.", null);
            }
            if (!TryReadCount(metadata, "documentCount", out var documentCount) || documentCount != manifest.DocumentCount)
            {
                return new SourceIndexVerification(false, "Segment index document count does not match the current manifest.", null);
            }
            var expectedSegmentCount = manifest.Documents.Sum(document => (long)document.SegmentCount);
            if (!TryReadCount(metadata, "segmentCount", out var segmentCount) || segmentCount != expectedSegmentCount)
            {
                return new SourceIndexVerification(false, "Segment index segment count does not match the current manifest.", null);
            }

            var actualDocumentCount = await ExecuteCountAsync(connection, "SELECT COUNT(*) FROM documents;", cancellationToken).ConfigureAwait(false);
            var actualSegmentCount = await ExecuteCountAsync(connection, "SELECT COUNT(*) FROM segments;", cancellationToken).ConfigureAwait(false);
            var actualSearchCount = await ExecuteCountAsync(connection, "SELECT COUNT(*) FROM segment_search;", cancellationToken).ConfigureAwait(false);
            if (actualDocumentCount != documentCount || actualSegmentCount != segmentCount || actualSearchCount != segmentCount)
            {
                return new SourceIndexVerification(false, "Segment index table counts do not match its recorded metadata.", null);
            }

            await using var integrity = connection.CreateCommand();
            integrity.CommandText = "PRAGMA quick_check;";
            var integrityResult = Convert.ToString(await integrity.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false), System.Globalization.CultureInfo.InvariantCulture);
            if (!string.Equals(integrityResult, "ok", StringComparison.OrdinalIgnoreCase))
            {
                return new SourceIndexVerification(false, $"SQLite integrity check failed: {integrityResult}", null);
            }

            return new SourceIndexVerification(true, "Segment index is current and valid.", new SourceIndexStatistics(schemaVersion, fingerprint, checked((int)documentCount), segmentCount, GetConnectionFileSize(connection)));
        }
        catch (SqliteException exception)
        {
            return new SourceIndexVerification(false, $"Segment index schema is invalid: {exception.Message}", null);
        }
    }

    /// <summary>Computes the deterministic corpus fingerprint stored in every segment index.</summary>
    /// <param name="manifest">Manifest whose immutable corpus identity should be calculated.</param>
    /// <returns>A lowercase SHA-256 fingerprint.</returns>
    public static string ComputeCorpusFingerprint(DocumentManifest manifest)
    {
        ArgumentNullException.ThrowIfNull(manifest);
        var builder = new StringBuilder();
        builder.Append(IndexSchemaVersion).Append('\n');
        builder.Append(manifest.SchemaVersion).Append('\n');
        builder.Append(manifest.SourceProvider).Append('\n');
        builder.Append(manifest.SourceManifests.RawSha256.ToLowerInvariant()).Append('\n');
        builder.Append(manifest.SourceManifests.NormalizedSha256.ToLowerInvariant()).Append('\n');
        foreach (var document in manifest.Documents.OrderBy(document => document.DocumentId, StringComparer.Ordinal))
        {
            builder.Append(document.DocumentId).Append('|').Append(document.Sha256.ToLowerInvariant()).Append('|').Append(document.SegmentCount).Append('|').Append(document.License).Append('|').Append(document.LicenseCategory).Append('|').Append(document.RequiresAttribution).Append('|').Append(document.RequiresShareAlike).Append('|').Append(document.AttributionUrl).Append('|').Append(document.WorkKey).Append('|').Append(document.UsageNote).Append('\n');
        }
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(builder.ToString()))).ToLowerInvariant();
    }

    private static void ValidateManifest(DocumentManifest manifest)
    {
        if (!string.Equals(manifest.SchemaVersion, ManifestLoader.SupportedSchemaVersion, StringComparison.Ordinal))
        {
            throw new ArgumentException($"Manifest schema must be {ManifestLoader.SupportedSchemaVersion}.", nameof(manifest));
        }
        if (manifest.Documents is null || manifest.DocumentCount != manifest.Documents.Count)
        {
            throw new ArgumentException("Manifest document count is inconsistent.", nameof(manifest));
        }
    }

    private static async Task ConfigureBuildConnectionAsync(SqliteConnection connection, CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = "PRAGMA journal_mode = DELETE; PRAGMA synchronous = NORMAL; PRAGMA temp_store = MEMORY; PRAGMA cache_size = -65536;";
        await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    private static async Task ResetSchemaAsync(SqliteConnection connection, CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = "DROP TABLE IF EXISTS segment_search; DROP TABLE IF EXISTS segments; DROP TABLE IF EXISTS document_categories; DROP TABLE IF EXISTS documents; DROP TABLE IF EXISTS metadata;" + CreateSchemaSql;
        await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    private static SqliteCommand CreateInsertDocumentCommand(SqliteConnection connection, SqliteTransaction transaction)
    {
        var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = "INSERT INTO documents(document_id, title, hebrew_title, language, language_code, collection, categories_json, version_title, license, source_url, file_path, work_key, usage_note) VALUES($documentId, $title, $hebrewTitle, $language, $languageCode, $collection, $categories, $version, $license, $sourceUrl, $filePath, $workKey, $usageNote);";
        command.Parameters.Add("$documentId", SqliteType.Text);
        command.Parameters.Add("$title", SqliteType.Text);
        command.Parameters.Add("$hebrewTitle", SqliteType.Text);
        command.Parameters.Add("$language", SqliteType.Text);
        command.Parameters.Add("$languageCode", SqliteType.Text);
        command.Parameters.Add("$collection", SqliteType.Text);
        command.Parameters.Add("$categories", SqliteType.Text);
        command.Parameters.Add("$version", SqliteType.Text);
        command.Parameters.Add("$license", SqliteType.Text);
        command.Parameters.Add("$sourceUrl", SqliteType.Text);
        command.Parameters.Add("$filePath", SqliteType.Text);
        command.Parameters.Add("$workKey", SqliteType.Text);
        command.Parameters.Add("$usageNote", SqliteType.Text);
        return command;
    }

    private static SqliteCommand CreateInsertCategoryCommand(SqliteConnection connection, SqliteTransaction transaction)
    {
        var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = "INSERT INTO document_categories(document_id, category) VALUES($documentId, $category);";
        command.Parameters.Add("$documentId", SqliteType.Text);
        command.Parameters.Add("$category", SqliteType.Text);
        return command;
    }

    private static SqliteCommand CreateInsertSegmentCommand(SqliteConnection connection, SqliteTransaction transaction)
    {
        var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = "INSERT INTO segments(row_id, segment_id, document_id, canonical_reference, canonical_reference_normalized, document_ordinal, text) VALUES($rowId, $segmentId, $documentId, $reference, $normalizedReference, $ordinal, $text);";
        command.Parameters.Add("$rowId", SqliteType.Integer);
        command.Parameters.Add("$segmentId", SqliteType.Text);
        command.Parameters.Add("$documentId", SqliteType.Text);
        command.Parameters.Add("$reference", SqliteType.Text);
        command.Parameters.Add("$normalizedReference", SqliteType.Text);
        command.Parameters.Add("$ordinal", SqliteType.Integer);
        command.Parameters.Add("$text", SqliteType.Text);
        return command;
    }

    private static SqliteCommand CreateInsertSearchCommand(SqliteConnection connection, SqliteTransaction transaction)
    {
        var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = "INSERT INTO segment_search(rowid, normalized_text, canonical_reference, title, hebrew_title, version_title) VALUES($rowId, $normalizedText, $reference, $title, $hebrewTitle, $version);";
        command.Parameters.Add("$rowId", SqliteType.Integer);
        command.Parameters.Add("$normalizedText", SqliteType.Text);
        command.Parameters.Add("$reference", SqliteType.Text);
        command.Parameters.Add("$title", SqliteType.Text);
        command.Parameters.Add("$hebrewTitle", SqliteType.Text);
        command.Parameters.Add("$version", SqliteType.Text);
        return command;
    }

    private static async Task InsertDocumentAsync(SqliteCommand command, ManifestDocument document, CancellationToken cancellationToken)
    {
        command.Parameters["$documentId"].Value = document.DocumentId;
        command.Parameters["$title"].Value = document.FileTitle;
        command.Parameters["$hebrewTitle"].Value = document.HebrewTitle;
        command.Parameters["$language"].Value = document.FileLanguage;
        command.Parameters["$languageCode"].Value = document.FileLanguageCode;
        command.Parameters["$collection"].Value = document.Collection;
        command.Parameters["$categories"].Value = JsonSerializer.Serialize(document.Categories);
        command.Parameters["$version"].Value = document.VersionTitle;
        command.Parameters["$license"].Value = document.License;
        command.Parameters["$sourceUrl"].Value = document.AttributionUrl;
        command.Parameters["$filePath"].Value = document.FilePath;
        command.Parameters["$workKey"].Value = (object?)document.WorkKey ?? DBNull.Value;
        command.Parameters["$usageNote"].Value = (object?)document.UsageNote ?? DBNull.Value;
        await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    private static async Task InsertCategoryAsync(SqliteCommand command, string documentId, string category, CancellationToken cancellationToken)
    {
        command.Parameters["$documentId"].Value = documentId;
        command.Parameters["$category"].Value = category;
        await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    private static async Task InsertSegmentAsync(SqliteCommand command, long rowId, SourceSegment segment, CancellationToken cancellationToken)
    {
        command.Parameters["$rowId"].Value = rowId;
        command.Parameters["$segmentId"].Value = segment.SegmentId;
        command.Parameters["$documentId"].Value = segment.DocumentId;
        command.Parameters["$reference"].Value = segment.CanonicalReference;
        command.Parameters["$normalizedReference"].Value = SearchTextNormalizer.Normalize(segment.CanonicalReference);
        command.Parameters["$ordinal"].Value = segment.DocumentOrdinal;
        command.Parameters["$text"].Value = segment.Text;
        await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    private static async Task InsertSearchAsync(SqliteCommand command, long rowId, SourceSegment segment, CancellationToken cancellationToken)
    {
        command.Parameters["$rowId"].Value = rowId;
        command.Parameters["$normalizedText"].Value = SearchTextNormalizer.Normalize(segment.Text);
        command.Parameters["$reference"].Value = segment.CanonicalReference;
        command.Parameters["$title"].Value = segment.Title;
        command.Parameters["$hebrewTitle"].Value = segment.HebrewTitle;
        command.Parameters["$version"].Value = segment.Version;
        await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    private static async Task InsertMetadataAsync(SqliteConnection connection, SqliteTransaction transaction, string key, string value, CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = "INSERT INTO metadata(key, value) VALUES($key, $value);";
        command.Parameters.AddWithValue("$key", key);
        command.Parameters.AddWithValue("$value", value);
        await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    private static async Task<Dictionary<string, string>> ReadMetadataAsync(SqliteConnection connection, CancellationToken cancellationToken)
    {
        var result = new Dictionary<string, string>(StringComparer.Ordinal);
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT key, value FROM metadata;";
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            result.Add(reader.GetString(0), reader.GetString(1));
        }
        return result;
    }

    private static async Task<long> ExecuteCountAsync(SqliteConnection connection, string sql, CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        return Convert.ToInt64(await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false), System.Globalization.CultureInfo.InvariantCulture);
    }

    private static bool TryReadCount(IReadOnlyDictionary<string, string> metadata, string key, out long value)
    {
        value = 0;
        return metadata.TryGetValue(key, out var text) && long.TryParse(text, System.Globalization.NumberStyles.None, System.Globalization.CultureInfo.InvariantCulture, out value);
    }

    private static async Task EnsureOpenAsync(SqliteConnection connection, CancellationToken cancellationToken)
    {
        if (connection.State != System.Data.ConnectionState.Open)
        {
            await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
        }
    }

    private static long GetConnectionFileSize(SqliteConnection connection)
    {
        var dataSource = connection.DataSource;
        return !string.IsNullOrWhiteSpace(dataSource) && !string.Equals(dataSource, ":memory:", StringComparison.OrdinalIgnoreCase) && File.Exists(dataSource) ? new FileInfo(dataSource).Length : 0;
    }
}
