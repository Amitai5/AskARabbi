using System.Globalization;
using System.Text.Json;
using AskARabbiLIB.Models;
using AskARabbiLIB.Search;
using Microsoft.Data.Sqlite;

namespace AskARabbiLIB.Retrieval;

/// <summary>Retrieves source segments from the disk-backed SQLite FTS5 index.</summary>
public sealed class SqliteSourceRetriever : ISourceRetriever, IAsyncDisposable
{
    private readonly string? indexPath;
    private readonly SqliteConnection? sharedConnection;
    private readonly DocumentManifest manifest;
    private readonly SourceIndexBuilder indexBuilder = new();
    private readonly SemaphoreSlim sharedConnectionGate = new(1, 1);
    private readonly SemaphoreSlim verificationGate = new(1, 1);
    private bool verified;
    private int disposeState;

    /// <summary>Creates a disk-backed retriever that rejects missing or stale indexes.</summary>
    /// <param name="indexPath">SQLite segment index path.</param>
    /// <param name="manifest">Current manifest used to verify the corpus fingerprint.</param>
    public SqliteSourceRetriever(string indexPath, DocumentManifest manifest)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(indexPath);
        ArgumentNullException.ThrowIfNull(manifest);
        this.indexPath = Path.GetFullPath(indexPath);
        this.manifest = manifest;
    }

    /// <summary>Creates a retriever over an existing SQLite connection for controlled hosts and tests.</summary>
    /// <param name="connection">Caller-owned SQLite connection.</param>
    /// <param name="manifest">Current manifest used to verify the corpus fingerprint.</param>
    public SqliteSourceRetriever(SqliteConnection connection, DocumentManifest manifest)
    {
        ArgumentNullException.ThrowIfNull(connection);
        ArgumentNullException.ThrowIfNull(manifest);
        sharedConnection = connection;
        this.manifest = manifest;
    }

    /// <inheritdoc cref="ISourceRetriever.SearchAsync"/>
    public Task<IReadOnlyList<SourceRetrievalHit>> SearchAsync(SourceRetrievalQuery query, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);
        ValidateQuery(query);
        return UseConnectionAsync(connection => SearchCoreAsync(connection, query, cancellationToken), cancellationToken);
    }

    /// <inheritdoc cref="ISourceRetriever.GetContextAsync"/>
    public Task<IReadOnlyList<SourceSegment>> GetContextAsync(string documentId, int documentOrdinal, int radius, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(documentId);
        if (documentOrdinal < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(documentOrdinal), "Document ordinal cannot be negative.");
        }
        if (radius is < 0 or > 10)
        {
            throw new ArgumentOutOfRangeException(nameof(radius), "Context radius must be between 0 and 10.");
        }
        return UseConnectionAsync(connection => GetContextCoreAsync(connection, documentId, documentOrdinal, radius, cancellationToken), cancellationToken);
    }

    /// <summary>Marks the retriever as disposed without taking ownership of caller-provided connections.</summary>
    /// <returns>A completed disposal operation.</returns>
    public ValueTask DisposeAsync()
    {
        // Active operations may still release these gates during cooperative shutdown.
        // SemaphoreSlim has no unmanaged resources, so marking the instance is sufficient.
        Interlocked.Exchange(ref disposeState, 1);
        return ValueTask.CompletedTask;
    }

    private async Task<T> UseConnectionAsync<T>(Func<SqliteConnection, Task<T>> operation, CancellationToken cancellationToken)
    {
        ObjectDisposedException.ThrowIf(Volatile.Read(ref disposeState) != 0, this);
        await EnsureVerifiedAsync(cancellationToken).ConfigureAwait(false);
        if (sharedConnection is not null)
        {
            await sharedConnectionGate.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                if (sharedConnection.State != System.Data.ConnectionState.Open)
                {
                    await sharedConnection.OpenAsync(cancellationToken).ConfigureAwait(false);
                }
                return await operation(sharedConnection).ConfigureAwait(false);
            }
            finally
            {
                sharedConnectionGate.Release();
            }
        }

        await using var connection = new SqliteConnection(CreateReadOnlyConnectionString(GetIndexPath()));
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
        return await operation(connection).ConfigureAwait(false);
    }

    private async Task EnsureVerifiedAsync(CancellationToken cancellationToken)
    {
        if (verified)
        {
            return;
        }
        await verificationGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (verified)
            {
                return;
            }
            SourceIndexVerification verification;
            if (sharedConnection is not null)
            {
                await sharedConnectionGate.WaitAsync(cancellationToken).ConfigureAwait(false);
                try
                {
                    verification = await indexBuilder.VerifyAsync(sharedConnection, manifest, cancellationToken).ConfigureAwait(false);
                }
                finally
                {
                    sharedConnectionGate.Release();
                }
            }
            else
            {
                verification = await indexBuilder.VerifyAsync(GetIndexPath(), manifest, cancellationToken).ConfigureAwait(false);
            }
            if (!verification.IsValid)
            {
                throw new InvalidOperationException(verification.Message);
            }
            verified = true;
        }
        finally
        {
            verificationGate.Release();
        }
    }

    private static async Task<IReadOnlyList<SourceRetrievalHit>> SearchCoreAsync(SqliteConnection connection, SourceRetrievalQuery query, CancellationToken cancellationToken)
    {
        var hits = new Dictionary<string, SourceRetrievalHit>(StringComparer.Ordinal);
        if (!string.IsNullOrWhiteSpace(query.ExactCanonicalReference))
        {
            await ReadExactHitsAsync(connection, query, hits, cancellationToken).ConfigureAwait(false);
        }
        if (!string.IsNullOrWhiteSpace(query.QueryText))
        {
            await ReadKeywordHitsAsync(connection, query, hits, cancellationToken).ConfigureAwait(false);
        }
        return hits.Values
            .OrderByDescending(hit => hit.IsExactReference)
            .ThenByDescending(hit => hit.Score)
            .ThenBy(hit => hit.Segment.CanonicalReference, StringComparer.OrdinalIgnoreCase)
            .ThenBy(hit => hit.Segment.SegmentId, StringComparer.Ordinal)
            .Take(query.CandidateLimit)
            .ToArray();
    }

    private static async Task ReadExactHitsAsync(SqliteConnection connection, SourceRetrievalQuery query, IDictionary<string, SourceRetrievalHit> hits, CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        var filters = new List<string> { "s.canonical_reference_normalized = $reference" };
        command.Parameters.AddWithValue("$reference", SearchTextNormalizer.Normalize(query.ExactCanonicalReference));
        AddFilters(command, filters, query);
        command.Parameters.AddWithValue("$limit", query.CandidateLimit);
        command.CommandText = $"{SelectSegmentSql} WHERE {string.Join(" AND ", filters)} ORDER BY d.language_code = 'he' DESC, d.title COLLATE NOCASE, d.version_title COLLATE NOCASE LIMIT $limit;";
        await ReadHitsAsync(command, 1000d, true, hits, cancellationToken).ConfigureAwait(false);
    }

    private static async Task ReadKeywordHitsAsync(SqliteConnection connection, SourceRetrievalQuery query, IDictionary<string, SourceRetrievalHit> hits, CancellationToken cancellationToken)
    {
        var plan = RetrievalQueryPlanner.Plan(query.QueryText);
        if (plan.Concepts.Count == 0)
        {
            return;
        }

        var strictMatch = string.Join(" AND ", plan.Concepts.Select(FormatMatchGroup));
        await ReadFtsHitsAsync(connection, query, strictMatch, plan.TopicAnchor is null ? 3d : 4d, hits, cancellationToken).ConfigureAwait(false);

        if (plan.TopicAnchor is not null)
        {
            foreach (var support in plan.SupportingConcepts)
            {
                var anchoredMatch = $"({FormatMatchGroup(plan.TopicAnchor)} AND {FormatMatchGroup(support)})";
                await ReadFtsHitsAsync(connection, query, anchoredMatch, 3d, hits, cancellationToken).ConfigureAwait(false);
            }
            if (hits.Count < query.CandidateLimit)
            {
                await ReadFtsHitsAsync(connection, query, FormatMatchGroup(plan.TopicAnchor), 1d, hits, cancellationToken).ConfigureAwait(false);
            }
            return;
        }

        if (hits.Count >= query.CandidateLimit)
        {
            return;
        }

        if (plan.Concepts.Count > 2)
        {
            var pairMatches = new List<string>();
            for (var firstIndex = 0; firstIndex < plan.Concepts.Count - 1; firstIndex++)
            {
                for (var secondIndex = firstIndex + 1; secondIndex < plan.Concepts.Count; secondIndex++)
                {
                    pairMatches.Add($"({FormatMatchGroup(plan.Concepts[firstIndex])} AND {FormatMatchGroup(plan.Concepts[secondIndex])})");
                }
            }
            await ReadFtsHitsAsync(connection, query, string.Join(" OR ", pairMatches), 2d, hits, cancellationToken).ConfigureAwait(false);
        }
        if (hits.Count >= query.CandidateLimit)
        {
            return;
        }

        var broadMatch = string.Join(" OR ", plan.Concepts.Select(FormatMatchGroup));
        await ReadFtsHitsAsync(connection, query, broadMatch, 1d, hits, cancellationToken).ConfigureAwait(false);
    }

    private static async Task ReadFtsHitsAsync(SqliteConnection connection, SourceRetrievalQuery query, string matchExpression, double scoreTier, IDictionary<string, SourceRetrievalHit> hits, CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        var filters = new List<string> { "segment_search MATCH $match" };
        command.Parameters.AddWithValue("$match", matchExpression);
        AddFilters(command, filters, query);
        command.Parameters.AddWithValue("$limit", query.CandidateLimit);
        command.CommandText = $"{SelectFtsSegmentSql} WHERE {string.Join(" AND ", filters)} ORDER BY rank ASC, s.row_id ASC LIMIT $limit;";
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            var segment = ReadSegment(reader);
            var rank = reader.GetDouble(17);
            var hit = new SourceRetrievalHit(segment, scoreTier + Math.Max(double.Epsilon, -rank), false);
            if (!hits.TryGetValue(segment.SegmentId, out var existing) || existing.Score < hit.Score)
            {
                hits[segment.SegmentId] = hit;
            }
        }
    }

    private static string FormatMatchGroup(RetrievalConcept concept)
    {
        var alternatives = string.Join(" OR ", concept.Tokens.Select(token => $"\"{token}\""));
        return concept.Tokens.Count == 1 ? alternatives : $"({alternatives})";
    }

    private static async Task ReadHitsAsync(SqliteCommand command, double score, bool exact, IDictionary<string, SourceRetrievalHit> hits, CancellationToken cancellationToken)
    {
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            var segment = ReadSegment(reader);
            hits[segment.SegmentId] = new SourceRetrievalHit(segment, score, exact);
        }
    }

    private static async Task<IReadOnlyList<SourceSegment>> GetContextCoreAsync(SqliteConnection connection, string documentId, int documentOrdinal, int radius, CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = $"{SelectSegmentSql} WHERE s.document_id = $documentId AND s.document_ordinal BETWEEN $minimum AND $maximum ORDER BY s.document_ordinal;";
        command.Parameters.AddWithValue("$documentId", documentId);
        command.Parameters.AddWithValue("$minimum", Math.Max(0, documentOrdinal - radius));
        command.Parameters.AddWithValue("$maximum", checked(documentOrdinal + radius));
        var segments = new List<SourceSegment>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            segments.Add(ReadSegment(reader));
        }
        return segments;
    }

    private static void AddFilters(SqliteCommand command, ICollection<string> filters, SourceRetrievalQuery query)
    {
        AddValueFilter(command, filters, query.Languages, "language", "(d.language = {0} COLLATE NOCASE OR d.language_code = {0} COLLATE NOCASE)");
        AddValueFilter(command, filters, query.Collections, "collection", "d.collection = {0} COLLATE NOCASE");
        AddValueFilter(command, filters, query.WorkKeys, "workKey", "d.work_key = {0} COLLATE NOCASE");
        AddSourceFilter(command, filters, query.SourceKeys);
        var categories = NormalizeFilter(query.Categories, nameof(query.Categories));
        if (categories.Count > 0)
        {
            var categoryConditions = new List<string>();
            for (var index = 0; index < categories.Count; index++)
            {
                var parameterName = $"$category{index}";
                command.Parameters.AddWithValue(parameterName, categories[index]);
                categoryConditions.Add($"dc.category = {parameterName} COLLATE NOCASE");
            }
            filters.Add($"EXISTS (SELECT 1 FROM document_categories dc WHERE dc.document_id = d.document_id AND ({string.Join(" OR ", categoryConditions)}))");
        }
    }

    private static void AddValueFilter(SqliteCommand command, ICollection<string> filters, IReadOnlyCollection<string> values, string name, string conditionFormat)
    {
        var normalized = NormalizeFilter(values, name);
        if (normalized.Count == 0)
        {
            return;
        }
        var conditions = new List<string>();
        for (var index = 0; index < normalized.Count; index++)
        {
            var parameterName = $"${name}{index}";
            command.Parameters.AddWithValue(parameterName, normalized[index]);
            conditions.Add(string.Format(CultureInfo.InvariantCulture, conditionFormat, parameterName));
        }
        filters.Add($"({string.Join(" OR ", conditions)})");
    }

    private static void AddSourceFilter(SqliteCommand command, ICollection<string> filters, IReadOnlyCollection<string> sourceKeys)
    {
        var normalized = NormalizeFilter(sourceKeys, nameof(sourceKeys));
        if (normalized.Count == 0)
        {
            return;
        }

        var conditions = new List<string>(normalized.Count);
        for (var index = 0; index < normalized.Count; index++)
        {
            var sourceKey = normalized[index];
            if (!DocumentSourceCatalog.TryParseSourceKey(sourceKey, out var isWork, out var value))
            {
                throw new ArgumentException($"Source key '{sourceKey}' must start with 'work:' or 'collection:' and include a value.", nameof(sourceKeys));
            }
            var parameterName = $"$source{index}";
            command.Parameters.AddWithValue(parameterName, value);
            conditions.Add(isWork ? $"d.work_key = {parameterName} COLLATE NOCASE" : $"(d.work_key IS NULL AND d.collection = {parameterName} COLLATE NOCASE)");
        }
        filters.Add($"({string.Join(" OR ", conditions)})");
    }

    private static List<string> NormalizeFilter(IReadOnlyCollection<string> values, string name)
    {
        if (values is null)
        {
            throw new ArgumentException($"Filter '{name}' cannot be null.", name);
        }
        if (values.Any(string.IsNullOrWhiteSpace))
        {
            throw new ArgumentException($"Filter '{name}' cannot contain empty values.", name);
        }
        return values.Select(value => value.Trim()).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
    }

    private static SourceSegment ReadSegment(SqliteDataReader reader)
    {
        var categories = JsonSerializer.Deserialize<string[]>(reader.GetString(10)) ?? throw new InvalidDataException("Segment index contains invalid category metadata.");
        var license = reader.GetString(12);
        return new SourceSegment
        {
            SegmentId = reader.GetString(0),
            DocumentId = reader.GetString(1),
            CanonicalReference = reader.GetString(2),
            DocumentOrdinal = reader.GetInt32(3),
            Text = reader.GetString(4),
            Title = reader.GetString(5),
            HebrewTitle = reader.GetString(6),
            Language = reader.GetString(7),
            LanguageCode = reader.GetString(8),
            Collection = reader.GetString(9),
            Categories = categories,
            Version = reader.GetString(11),
            License = license,
            LicenseCategory = SourceLicensePolicy.Classify(license),
            SourceUrl = reader.GetString(13),
            FilePath = reader.GetString(14),
            WorkKey = reader.IsDBNull(15) ? null : reader.GetString(15),
            UsageNote = reader.IsDBNull(16) ? null : reader.GetString(16),
        };
    }

    private static void ValidateQuery(SourceRetrievalQuery query)
    {
        if (query.CandidateLimit is < 1 or > 200)
        {
            throw new ArgumentOutOfRangeException(nameof(query), "Candidate limit must be between 1 and 200.");
        }
        if (string.IsNullOrWhiteSpace(query.QueryText) && string.IsNullOrWhiteSpace(query.ExactCanonicalReference))
        {
            throw new ArgumentException("A query must contain text or an exact canonical reference.", nameof(query));
        }
        if (!string.IsNullOrWhiteSpace(query.QueryText) && SearchTextNormalizer.Tokenize(query.QueryText).Length == 0 && string.IsNullOrWhiteSpace(query.ExactCanonicalReference))
        {
            throw new ArgumentException("Query text must contain at least one letter or digit.", nameof(query));
        }
        NormalizeFilter(query.Languages, nameof(query.Languages));
        NormalizeFilter(query.Collections, nameof(query.Collections));
        NormalizeFilter(query.Categories, nameof(query.Categories));
        NormalizeFilter(query.WorkKeys, nameof(query.WorkKeys));
        foreach (var sourceKey in NormalizeFilter(query.SourceKeys, nameof(query.SourceKeys)))
        {
            if (!DocumentSourceCatalog.TryParseSourceKey(sourceKey, out _, out _))
            {
                throw new ArgumentException($"Source key '{sourceKey}' must start with 'work:' or 'collection:' and include a value.", nameof(query));
            }
        }
    }

    private static string CreateReadOnlyConnectionString(string path) => new SqliteConnectionStringBuilder
    {
        DataSource = path,
        Mode = SqliteOpenMode.ReadOnly,
        Cache = SqliteCacheMode.Shared,
        Pooling = false,
    }.ToString();

    private string GetIndexPath() => indexPath ?? throw new InvalidOperationException("A file-backed retriever does not have an index path.");

    private const string SelectSegmentSql = """
        SELECT s.segment_id, s.document_id, s.canonical_reference, s.document_ordinal, s.text,
               d.title, d.hebrew_title, d.language, d.language_code, d.collection,
               d.categories_json, d.version_title, d.license, d.source_url, d.file_path,
               d.work_key, d.usage_note
        FROM segments s
        INNER JOIN documents d ON d.document_id = s.document_id
        """;

    private const string SelectFtsSegmentSql = """
        SELECT s.segment_id, s.document_id, s.canonical_reference, s.document_ordinal, s.text,
               d.title, d.hebrew_title, d.language, d.language_code, d.collection,
               d.categories_json, d.version_title, d.license, d.source_url, d.file_path,
               d.work_key, d.usage_note,
               bm25(segment_search, 8.0, 5.0, 4.0, 4.0, 2.0) AS rank
        FROM segment_search
        INNER JOIN segments s ON s.row_id = segment_search.rowid
        INNER JOIN documents d ON d.document_id = s.document_id
        """;
}
