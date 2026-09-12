using System.Globalization;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using AskARabbiLIB.Lexicon;
using AskARabbiLIB.Persistence.Mongo;
using MongoDB.Driver;

namespace AskARabbi.DictionaryImporter;

internal static class Program
{
    // Importing a different edition requires review, a new revision, and a new fingerprint.
    private const string ApprovedFingerprint = "1f4847c7ab99e79e3f422e797c4a10f5ba2c17ea5807656d500933bc76fc1858";

    private static async Task<int> Main(string[] args)
    {
        if (args.Length is < 2 or > 3 || args[0] is not ("download" or "validate" or "import" or "search"))
        {
            Console.Error.WriteLine("Usage: download <new-directory> | validate <directory> | import <directory> --confirm-write | search <query>. MongoDB__ConnectionString, MongoDB__DatabaseName, and optionally MongoDB__LexiconCollectionName configure import/search.");
            return 2;
        }
        using var cancellation = new CancellationTokenSource();
        Console.CancelKeyPress += (_, eventArgs) => { eventArgs.Cancel = true; cancellation.Cancel(); };
        try
        {
            if (args[0] == "search")
            {
                var result = await CreateStore(CreateOptions()).SearchAsync(BdbCorpus.DictionaryId, BdbCorpus.Revision, args[1], 3, cancellation.Token).ConfigureAwait(false);
                WriteJson(result.Select(entry => new { entry.EntryId, entry.Headword, entry.SourceUrl, preview = entry.Text[..Math.Min(250, entry.Text.Length)] }));
                return 0;
            }
            var directory = Path.GetFullPath(args[1]);
            if (args[0] == "download")
            {
                await DownloadAsync(directory, cancellation.Token).ConfigureAwait(false);
            }
            var (articles, fingerprint) = await ReadAsync(directory, cancellation.Token).ConfigureAwait(false);
            if (!string.Equals(fingerprint, ApprovedFingerprint, StringComparison.Ordinal))
            {
                throw new InvalidDataException("The dataset does not match the reviewed BDB fingerprint. No database writes were made.");
            }
            if (args[0] == "import")
            {
                if (args.Length != 3 || args[2] != "--confirm-write")
                {
                    throw new ArgumentException("Import changes MongoDB. Run validate first, then pass --confirm-write explicitly.");
                }
                var options = CreateOptions();
                var store = CreateStore(options);
                var count = await store.ImportBdbAsync(articles, cancellation.Token).ConfigureAwait(false);
                WriteJson(new { published = true, dictionary = BdbCorpus.DictionaryId, revision = BdbCorpus.Revision, count, collection = options.LexiconCollectionName });
            }
            else
            {
                WriteJson(new { validated = true, approvedFingerprint = fingerprint == ApprovedFingerprint, revision = BdbCorpus.Revision, articles = articles.Count, fingerprint, totalCharacters = articles.Sum(entry => (long)entry.Text.Length), longestArticle = articles.Max(entry => entry.Text.Length) });
            }
            return 0;
        }
        catch (OperationCanceledException)
        {
            Console.Error.WriteLine("Dictionary operation cancelled. An incomplete first import is not published; rerun to resume.");
            return 130;
        }
        catch (MongoException)
        {
            // Driver messages can contain connection details; do not print credentials or endpoints.
            Console.Error.WriteLine("MongoDB operation failed. Check private-network access, collection provisioning, permissions, and Cosmos MongoDB compatibility. The import can be retried; no collection was deleted.");
            return 1;
        }
        catch (Exception exception) when (exception is ArgumentException or InvalidOperationException or IOException or JsonException or HttpRequestException or FormatException)
        {
            Console.Error.WriteLine(exception.Message);
            return 1;
        }
    }

    private static async Task DownloadAsync(string directory, CancellationToken cancellationToken)
    {
        if (Directory.Exists(directory) && Directory.EnumerateFileSystemEntries(directory).Any())
        {
            throw new IOException("Download requires an empty directory; existing files will not be overwritten.");
        }
        Directory.CreateDirectory(directory);
        using var client = new HttpClient(new HttpClientHandler { AllowAutoRedirect = false, AutomaticDecompression = DecompressionMethods.All }) { Timeout = TimeSpan.FromMinutes(2) };
        client.DefaultRequestHeaders.UserAgent.ParseAdd("AskARabbi-BDB-Importer/1.0");
        foreach (var file in SourceFiles().Append("metadata.json").Append("README.md"))
        {
            var relative = file == "README.md" ? file : file == "metadata.json" ? "eng/metadata.json" : "eng/json/" + file;
            var uri = $"https://raw.githubusercontent.com/BibleAquifer/BDBHebrewLexicon/{BdbCorpus.Revision}/{relative}";
            using var response = await client.GetAsync(uri, HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false);
            response.EnsureSuccessStatusCode();
            await using var input = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
            await using var output = new FileStream(Path.Combine(directory, file), FileMode.CreateNew, FileAccess.Write, FileShare.None, 81920, true);
            var buffer = new byte[81920];
            var total = 0;
            int read;
            while ((read = await input.ReadAsync(buffer, cancellationToken).ConfigureAwait(false)) > 0)
            {
                total += read;
                if (total > 10_000_000)
                {
                    throw new InvalidDataException("BDB download exceeds the per-file limit.");
                }
                await output.WriteAsync(buffer.AsMemory(0, read), cancellationToken).ConfigureAwait(false);
            }
            Console.Error.WriteLine($"Downloaded {file} ({total:N0} bytes).");
        }
    }

    private static async Task<(IReadOnlyList<LexiconEntry> Articles, string Fingerprint)> ReadAsync(string directory, CancellationToken cancellationToken)
    {
        var parser = new BdbEntryParser();
        var articles = new List<LexiconEntry>();
        var identifiers = new HashSet<string>(StringComparer.Ordinal);
        using var fingerprint = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        foreach (var file in SourceFiles())
        {
            var path = Path.Combine(directory, file);
            if (new FileInfo(path).Length > 10_000_000)
            {
                throw new InvalidDataException("BDB source exceeds the per-file limit.");
            }
            var bytes = await File.ReadAllBytesAsync(path, cancellationToken).ConfigureAwait(false);
            fingerprint.AppendData(Encoding.UTF8.GetBytes(file + "\n"));
            fingerprint.AppendData(bytes);
            foreach (var article in parser.Parse(bytes, file))
            {
                if (!identifiers.Add(article.EntryId))
                {
                    throw new InvalidDataException($"Duplicate BDB article identifier: {article.EntryId}.");
                }
                articles.Add(article);
            }
        }
        return (articles, Convert.ToHexStringLower(fingerprint.GetHashAndReset()));
    }

    private static IEnumerable<string> SourceFiles() => Enumerable.Range(0, 23).Select(index => index.ToString("D3", CultureInfo.InvariantCulture) + ".content.json");

    private static MongoDatabaseOptions CreateOptions() => new()
    {
        ConnectionString = Environment.GetEnvironmentVariable("MongoDB__ConnectionString") ?? string.Empty,
        DatabaseName = Environment.GetEnvironmentVariable("MongoDB__DatabaseName") ?? "askarabbi",
        LexiconCollectionName = Environment.GetEnvironmentVariable("MongoDB__LexiconCollectionName") ?? "lexiconEntries",
    };

    private static MongoLexiconStore CreateStore(MongoDatabaseOptions options)
    {
        options.Validate();
        var settings = MongoClientSettings.FromConnectionString(options.ConnectionString);
        settings.ServerSelectionTimeout = TimeSpan.FromSeconds(15);
        return new MongoLexiconStore(new MongoClient(settings).GetDatabase(options.DatabaseName), options);
    }

    private static void WriteJson(object result) => Console.WriteLine(JsonSerializer.Serialize(result, new JsonSerializerOptions { WriteIndented = true }));
}
