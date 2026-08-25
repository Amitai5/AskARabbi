using System.Diagnostics;
using AskARabbiLIB;
using AskARabbiLIB.Files;
using AskARabbiLIB.Retrieval;
using AskARabbiLIB.Search;
using Microsoft.Extensions.Configuration;
using Spectre.Console;

namespace AskARabbiPrototype;

internal sealed class ApplicationStateLoader
{
    private readonly CancellationToken cancellationToken;

    internal ApplicationStateLoader(CancellationToken cancellationToken)
    {
        this.cancellationToken = cancellationToken;
    }

    internal async Task<ApplicationState> LoadAsync(ManifestLocation location, bool showStatus)
    {
        ArgumentNullException.ThrowIfNull(location);
        if (!showStatus || System.Console.IsOutputRedirected)
        {
            return await LoadCoreAsync(location).ConfigureAwait(false);
        }

        return await AnsiConsole.Status()
            .Spinner(Spinner.Known.Dots)
            .SpinnerStyle(Style.Parse("cyan"))
            .StartAsync("[cyan]Loading manifest and building document search indexes...[/]", _ => LoadCoreAsync(location))
            .ConfigureAwait(false);
    }

    private async Task<ApplicationState> LoadCoreAsync(ManifestLocation location)
    {
        var loadStopwatch = Stopwatch.StartNew();
        var manifest = await new ManifestLoader().LoadAsync(location.ManifestPath, cancellationToken).ConfigureAwait(false);
        loadStopwatch.Stop();

        var sourceCatalog = DocumentSourceCatalog.Create(manifest);
        var indexStopwatch = Stopwatch.StartNew();
        var searchIndex = ManifestSearchIndex.Create(manifest);
        indexStopwatch.Stop();

        var configuration = new ConfigurationBuilder()
            .AddJsonFile(Path.Combine(location.RepositoryRoot, "appsettings.json"), optional: true, reloadOnChange: false)
            .AddEnvironmentVariables()
            .Build();

        return new ApplicationState
        {
            Location = location,
            Manifest = manifest,
            SourceCatalog = sourceCatalog,
            SearchIndex = searchIndex,
            FileLoader = new SefariaDocumentFileLoader(location.RepositoryRoot),
            Configuration = configuration,
            LoadMilliseconds = loadStopwatch.Elapsed.TotalMilliseconds,
            SearchIndexMilliseconds = indexStopwatch.Elapsed.TotalMilliseconds,
        };
    }
}
