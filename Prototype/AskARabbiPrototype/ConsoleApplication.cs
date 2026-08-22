using System.Diagnostics;
using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using AskARabbiLIB;
using AskARabbiLIB.Files;
using AskARabbiLIB.Models;
using AskARabbiLIB.Search;
using Spectre.Console;

namespace AskARabbiPrototype;

internal sealed class ConsoleApplication
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) },
    };

    private readonly ConsoleCommandParser parser = new();
    private readonly CancellationToken cancellationToken;
    private ApplicationState? state;

    internal ConsoleApplication(CancellationToken cancellationToken)
    {
        this.cancellationToken = cancellationToken;
    }

    internal async Task<int> RunAsync(string[] arguments)
    {
        var parseResult = parser.Parse(arguments);
        if (!parseResult.IsSuccess)
        {
            WriteError(parseResult.Error!);
            return 2;
        }

        var initialCommand = parseResult.Command!;
        if (initialCommand.Kind == ConsoleCommandKind.Help)
        {
            PrintHelp();
            return 0;
        }
        if (initialCommand.Kind == ConsoleCommandKind.Interactive && System.Console.IsInputRedirected)
        {
            PrintHelp();
            return 0;
        }

        try
        {
            var location = ManifestPathResolver.Resolve(initialCommand.ManifestPath, initialCommand.RepositoryRoot);
            state = initialCommand.Kind == ConsoleCommandKind.Interactive
                ? await LoadStateWithStatusAsync(location).ConfigureAwait(false)
                : await LoadStateAsync(location).ConfigureAwait(false);

            if (initialCommand.Kind == ConsoleCommandKind.Interactive)
            {
                PrintBanner(state);
                return await RunInteractiveAsync().ConfigureAwait(false);
            }
            return ExecuteOneShot(initialCommand);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            WriteError("Operation canceled.");
            return 1;
        }
        catch (Exception exception) when (exception is IOException or JsonException or ArgumentException or InvalidOperationException or UnauthorizedAccessException)
        {
            WriteError(exception.Message);
            return 1;
        }
    }

    private async Task<int> RunInteractiveAsync()
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            var action = AnsiConsole.Prompt(
                new SelectionPrompt<MainMenuAction>()
                    .Title("[bold cyan]What would you like to do?[/]")
                    .PageSize(10)
                    .UseConverter(FormatMainMenuAction)
                    .AddChoices(MainMenuAction.Search, MainMenuAction.BrowseFacets, MainMenuAction.ViewStatistics, MainMenuAction.ReloadManifest, MainMenuAction.Exit));

            try
            {
                switch (action)
                {
                    case MainMenuAction.Search:
                        await RunSearchWizardAsync().ConfigureAwait(false);
                        break;
                    case MainMenuAction.BrowseFacets:
                        BrowseFacets();
                        break;
                    case MainMenuAction.ViewStatistics:
                        PrintStats(ConsoleOutputFormat.Table);
                        Pause();
                        break;
                    case MainMenuAction.ReloadManifest:
                        state = await LoadStateWithStatusAsync(state!.Location).ConfigureAwait(false);
                        PrintBanner(state);
                        break;
                    case MainMenuAction.Exit:
                        AnsiConsole.MarkupLine("[grey]Goodbye.[/]");
                        return 0;
                    default:
                        throw new InvalidOperationException($"Unsupported menu action: {action}.");
                }
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                return 0;
            }
            catch (Exception exception) when (exception is IOException or JsonException or ArgumentException or InvalidOperationException or UnauthorizedAccessException)
            {
                WriteError(exception.Message);
                Pause();
            }
        }
        return 0;
    }

    private async Task RunSearchWizardAsync()
    {
        var facets = state!.Index.GetFacets();
        AnsiConsole.Write(new Rule("[bold cyan]Search the Sefaria library[/]"));
        var keywords = AnsiConsole.Prompt(new TextPrompt<string>("[cyan]Keywords[/] [grey](optional)[/]:").AllowEmpty());
        var matchMode = KeywordMatchMode.All;
        if (keywords.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).Length > 1)
        {
            matchMode = AnsiConsole.Prompt(
                new SelectionPrompt<KeywordMatchMode>()
                    .Title("How should multiple keywords match?")
                    .UseConverter(value => value == KeywordMatchMode.All ? "All keywords" : "Any keyword")
                    .AddChoices(KeywordMatchMode.All, KeywordMatchMode.Any));
        }

        string? language = null;
        string? collection = null;
        string? category = null;
        if (AnsiConsole.Confirm("Add metadata filters?", false))
        {
            language = PromptFacet("language", facets.Languages);
            collection = PromptFacet("collection", facets.Collections);
            category = PromptFacet("category", facets.Categories);
        }

        var limit = AnsiConsole.Prompt(
            new TextPrompt<int>("Maximum results:")
                .DefaultValue(10)
                .ValidationErrorMessage("[red]Enter a number from 1 through 50.[/]")
                .Validate(value => value is >= 1 and <= 50));

        var query = new ManifestSearchQuery
        {
            Keywords = string.IsNullOrWhiteSpace(keywords) ? null : keywords,
            KeywordMatchMode = matchMode,
            Languages = ToFilter(language),
            Collections = ToFilter(collection),
            Categories = ToFilter(category),
            Limit = limit,
        };

        var stopwatch = Stopwatch.StartNew();
        var result = state.Index.Search(query);
        stopwatch.Stop();
        PrintSearchResults(result, stopwatch.Elapsed.TotalMilliseconds);
        if (result.Hits.Count == 0)
        {
            Pause();
            return;
        }

        var selection = PromptForResult(result.Hits);
        if (selection.Hit is not null)
        {
            await BrowseDocumentAsync(selection.Hit.Document).ConfigureAwait(false);
        }
    }

    private async Task BrowseDocumentAsync(ManifestDocument document)
    {
        SefariaDocumentFile? sourceFile = null;
        PrintDocumentMetadata(document);
        while (!cancellationToken.IsCancellationRequested)
        {
            var action = AnsiConsole.Prompt(
                new SelectionPrompt<DocumentAction>()
                    .Title("[bold cyan]Document actions[/]")
                    .UseConverter(FormatDocumentAction)
                    .AddChoices(DocumentAction.RawText, DocumentAction.SourceMetadata, DocumentAction.OriginalJson, DocumentAction.NormalizedMarkdown, DocumentAction.ManifestMetadata, DocumentAction.Back));
            if (action == DocumentAction.Back)
            {
                return;
            }

            if (action == DocumentAction.NormalizedMarkdown)
            {
                var markdown = await LoadWithStatusAsync("Loading and verifying normalized Markdown...", () => state!.FileLoader.LoadNormalizedMarkdownAsync(document, cancellationToken)).ConfigureAwait(false);
                WritePlainContent("Normalized Markdown", markdown);
                Pause();
                continue;
            }
            if (action == DocumentAction.ManifestMetadata)
            {
                PrintDocumentMetadata(document);
                Pause();
                continue;
            }

            sourceFile ??= await LoadWithStatusAsync("Loading and verifying original Sefaria JSON...", () => state!.FileLoader.LoadRawFileAsync(document, cancellationToken)).ConfigureAwait(false);
            switch (action)
            {
                case DocumentAction.RawText:
                    WritePlainContent("Raw Text", sourceFile.GetRawText());
                    break;
                case DocumentAction.SourceMetadata:
                    WritePlainContent("Source Metadata", JsonSerializer.Serialize(sourceFile.Metadata, JsonOptions));
                    break;
                case DocumentAction.OriginalJson:
                    WritePlainContent("Original Sefaria JSON", sourceFile.RawJson);
                    break;
                default:
                    throw new InvalidOperationException($"Unsupported document action: {action}.");
            }
            Pause();
        }
    }

    private void BrowseFacets()
    {
        var facets = state!.Index.GetFacets();
        var groups = new[]
        {
            new FacetGroup("Languages", facets.Languages),
            new FacetGroup("Collections", facets.Collections),
            new FacetGroup("Categories", facets.Categories),
            new FacetGroup("Licenses", facets.Licenses),
        };
        var selected = AnsiConsole.Prompt(
            new SelectionPrompt<FacetGroup>()
                .Title("Choose a facet to inspect:")
                .UseConverter(group => group.Name)
                .AddChoices(groups));

        var table = CreateTable("Value", "Documents");
        foreach (var pair in selected.Values.OrderByDescending(pair => pair.Value).ThenBy(pair => pair.Key, StringComparer.OrdinalIgnoreCase))
        {
            table.AddRow(Escape(pair.Key), pair.Value.ToString("N0", CultureInfo.CurrentCulture));
        }
        AnsiConsole.Write(new Rule($"[bold cyan]{Escape(selected.Name)}[/]"));
        AnsiConsole.Write(table);
        Pause();
    }

    private int ExecuteOneShot(ConsoleCommand command)
    {
        switch (command.Kind)
        {
            case ConsoleCommandKind.Search:
                ExecuteSearch(command);
                break;
            case ConsoleCommandKind.Facets:
                PrintFacets(command.OutputFormat);
                break;
            case ConsoleCommandKind.Stats:
                PrintStats(command.OutputFormat);
                break;
            default:
                WriteError($"Command '{command.Kind}' is unavailable in one-shot mode.");
                return 2;
        }
        return 0;
    }

    private void ExecuteSearch(ConsoleCommand command)
    {
        var query = command.Query ?? throw new InvalidOperationException("Search command is missing its query.");
        var stopwatch = Stopwatch.StartNew();
        var result = state!.Index.Search(query);
        stopwatch.Stop();
        if (command.OutputFormat == ConsoleOutputFormat.Json)
        {
            var response = new { result.TotalMatches, result.Skip, result.Limit, elapsedMilliseconds = stopwatch.Elapsed.TotalMilliseconds, result.Hits };
            System.Console.WriteLine(JsonSerializer.Serialize(response, JsonOptions));
            return;
        }
        PrintSearchResults(result, stopwatch.Elapsed.TotalMilliseconds);
    }

    private static void PrintSearchResults(ManifestSearchResult result, double elapsedMilliseconds)
    {
        AnsiConsole.MarkupLine($"[bold green]{result.TotalMatches:N0}[/] match(es); showing [bold]{result.Hits.Count:N0}[/] in [bold]{elapsedMilliseconds:N2} ms[/].");
        if (result.Hits.Count == 0)
        {
            AnsiConsole.MarkupLine("[yellow]No documents matched those criteria.[/]");
            return;
        }

        var table = CreateTable("#", "Score", "Title", "Language", "Collection", "Reference", "License");
        for (var index = 0; index < result.Hits.Count; index++)
        {
            var hit = result.Hits[index];
            table.AddRow(
                (index + 1).ToString(CultureInfo.InvariantCulture),
                hit.Score.ToString(CultureInfo.InvariantCulture),
                Escape(hit.Document.FileTitle),
                Escape(hit.Document.FileLanguage),
                Escape(hit.Document.Collection),
                Escape(FormatReferenceRange(hit.Document)),
                Escape(hit.Document.License));
        }
        AnsiConsole.Write(table);
    }

    private void PrintFacets(ConsoleOutputFormat outputFormat)
    {
        var facets = state!.Index.GetFacets();
        if (outputFormat == ConsoleOutputFormat.Json)
        {
            System.Console.WriteLine(JsonSerializer.Serialize(facets, JsonOptions));
            return;
        }

        var table = CreateTable("Facet", "Value", "Documents");
        AddFacetRows(table, "Language", facets.Languages);
        AddFacetRows(table, "Collection", facets.Collections);
        AddFacetRows(table, "Category", facets.Categories);
        AddFacetRows(table, "License", facets.Licenses);
        AnsiConsole.Write(table);
    }

    private void PrintStats(ConsoleOutputFormat outputFormat)
    {
        var statistics = new
        {
            state!.Manifest.SchemaVersion,
            state.Manifest.SourceProvider,
            state.Manifest.GeneratedAtUtc,
            state.Manifest.DocumentCount,
            state.LoadMilliseconds,
            state.IndexMilliseconds,
            approximateManagedMemoryBytes = GC.GetTotalMemory(false),
            state.Location.ManifestPath,
            state.Location.RepositoryRoot,
        };
        if (outputFormat == ConsoleOutputFormat.Json)
        {
            System.Console.WriteLine(JsonSerializer.Serialize(statistics, JsonOptions));
            return;
        }

        var table = CreateTable("Metric", "Value");
        table.AddRow("Schema", Escape(statistics.SchemaVersion));
        table.AddRow("Provider", Escape(statistics.SourceProvider));
        table.AddRow("Generated", Escape(statistics.GeneratedAtUtc.ToString("u", CultureInfo.InvariantCulture)));
        table.AddRow("Documents", statistics.DocumentCount.ToString("N0", CultureInfo.CurrentCulture));
        table.AddRow("Manifest load", $"{statistics.LoadMilliseconds:N2} ms");
        table.AddRow("Index build", $"{statistics.IndexMilliseconds:N2} ms");
        table.AddRow("Managed memory", FormatBytes(statistics.approximateManagedMemoryBytes));
        table.AddRow("Manifest", Escape(statistics.ManifestPath));
        table.AddRow("Repository", Escape(statistics.RepositoryRoot));
        AnsiConsole.Write(table);
    }

    private static void PrintDocumentMetadata(ManifestDocument document)
    {
        AnsiConsole.Write(new Rule($"[bold cyan]{Escape(document.FileTitle)}[/]"));
        var table = CreateTable("Field", "Value");
        table.AddRow("Title", Escape(document.FileTitle));
        table.AddRow("Hebrew title", Escape(document.HebrewTitle));
        table.AddRow("Description", Escape(document.FileDescription));
        table.AddRow("Language", Escape($"{document.FileLanguage} ({document.FileLanguageCode})"));
        table.AddRow("Collection", Escape(document.Collection));
        table.AddRow("Categories", Escape(string.Join(" > ", document.Categories)));
        table.AddRow("Version", Escape(document.VersionTitle));
        table.AddRow("References", Escape(FormatReferenceRange(document)));
        table.AddRow("Segments", document.SegmentCount.ToString("N0", CultureInfo.CurrentCulture));
        table.AddRow("License", Escape(document.License ?? "Unspecified"));
        table.AddRow("License status", Escape(document.LicenseStatus));
        table.AddRow("Normalized file", Escape(document.FilePath));
        table.AddRow("Raw file", Escape(document.RawFilePath));
        table.AddRow("Source URL", Escape(document.SourceUrl));
        AnsiConsole.Write(table);
    }

    private async Task<ApplicationState> LoadStateWithStatusAsync(ManifestLocation location)
    {
        if (System.Console.IsOutputRedirected)
        {
            return await LoadStateAsync(location).ConfigureAwait(false);
        }
        return await AnsiConsole.Status()
            .Spinner(Spinner.Known.Dots)
            .SpinnerStyle(Style.Parse("cyan"))
            .StartAsync("[cyan]Loading manifest and building search indexes...[/]", _ => LoadStateAsync(location)).ConfigureAwait(false);
    }

    private async Task<T> LoadWithStatusAsync<T>(string message, Func<Task<T>> operation)
    {
        if (System.Console.IsOutputRedirected)
        {
            return await operation().ConfigureAwait(false);
        }
        return await AnsiConsole.Status()
            .Spinner(Spinner.Known.Dots)
            .SpinnerStyle(Style.Parse("cyan"))
            .StartAsync(Markup.Escape(message), _ => operation()).ConfigureAwait(false);
    }

    private async Task<ApplicationState> LoadStateAsync(ManifestLocation location)
    {
        var loader = new ManifestLoader();
        var loadStopwatch = Stopwatch.StartNew();
        var manifest = await loader.LoadAsync(location.ManifestPath, cancellationToken).ConfigureAwait(false);
        loadStopwatch.Stop();
        var indexStopwatch = Stopwatch.StartNew();
        var index = ManifestSearchIndex.Create(manifest);
        indexStopwatch.Stop();
        var fileLoader = new SefariaDocumentFileLoader(location.RepositoryRoot);
        return new ApplicationState(location, manifest, index, fileLoader, loadStopwatch.Elapsed.TotalMilliseconds, indexStopwatch.Elapsed.TotalMilliseconds);
    }

    private static string? PromptFacet(string name, IReadOnlyDictionary<string, int> values)
    {
        var choices = new List<FacetChoice> { new(null, 0) };
        choices.AddRange(values.OrderByDescending(pair => pair.Value).ThenBy(pair => pair.Key, StringComparer.OrdinalIgnoreCase).Select(pair => new FacetChoice(pair.Key, pair.Value)));
        var selected = AnsiConsole.Prompt(
            new SelectionPrompt<FacetChoice>()
                .Title($"Choose a {Markup.Escape(name)}:")
                .PageSize(12)
                .EnableSearch()
                .SearchPlaceholderText("[grey]Type to filter choices[/]")
                .UseConverter(choice => choice.Value is null ? "[grey]Any[/]" : $"{Escape(choice.Value)} [grey]({choice.Count:N0})[/]")
                .AddChoices(choices));
        return selected.Value;
    }

    private static SearchResultChoice PromptForResult(IReadOnlyList<ManifestSearchHit> hits)
    {
        var choices = new List<SearchResultChoice> { new(0, null) };
        choices.AddRange(hits.Select((hit, index) => new SearchResultChoice(index + 1, hit)));
        return AnsiConsole.Prompt(
            new SelectionPrompt<SearchResultChoice>()
                .Title("Open a result?")
                .PageSize(15)
                .EnableSearch()
                .SearchPlaceholderText("[grey]Type to filter results[/]")
                .UseConverter(FormatSearchResultChoice)
                .AddChoices(choices));
    }

    private static void AddFacetRows(Table table, string facetName, IReadOnlyDictionary<string, int> values)
    {
        foreach (var pair in values.OrderByDescending(pair => pair.Value).ThenBy(pair => pair.Key, StringComparer.OrdinalIgnoreCase))
        {
            table.AddRow(Escape(facetName), Escape(pair.Key), pair.Value.ToString("N0", CultureInfo.CurrentCulture));
        }
    }

    private static Table CreateTable(params string[] columns)
    {
        var table = new Table().Border(TableBorder.Rounded).BorderColor(Color.Grey).Expand();
        foreach (var column in columns)
        {
            table.AddColumn(new TableColumn($"[bold cyan]{Markup.Escape(column)}[/]"));
        }
        return table;
    }

    private static void PrintBanner(ApplicationState applicationState)
    {
        AnsiConsole.Write(new FigletText("Ask A Rabbi").Color(Color.Cyan1));
        var message = $"Loaded [bold green]{applicationState.Manifest.DocumentCount:N0}[/] permissively licensed Sefaria documents in [bold]{applicationState.LoadMilliseconds:N2} ms[/]; indexed in [bold]{applicationState.IndexMilliseconds:N2} ms[/].\n[grey]Every result retains its exact license for attribution and share-alike compliance.[/]";
        AnsiConsole.Write(new Panel(message).Header("[bold]Manifest Library[/]").Border(BoxBorder.Rounded).BorderColor(Color.Cyan1).Expand());
    }

    private static void PrintHelp()
    {
        AnsiConsole.Write(new FigletText("Ask A Rabbi").Color(Color.Cyan1));
        AnsiConsole.MarkupLine("Run without a command for the guided interactive search experience. One-shot commands are available for scripts and agents.");
        var table = CreateTable("Command", "Purpose");
        table.AddRow($"[cyan]search[/] {Escape("[keywords] [options]")}", "Search and rank manifest documents");
        table.AddRow($"[cyan]facets[/] {Escape("[--format table|json]")}", "List language, collection, category, and license facets");
        table.AddRow($"[cyan]stats[/] {Escape("[--format table|json]")}", "Show manifest and in-memory index statistics");
        table.AddRow("[cyan]help[/]", "Show this help");
        AnsiConsole.Write(table);
        AnsiConsole.MarkupLine("[bold]Search options:[/] --language, --collection, --category, --title, --version, --license, --match all|any, --min-segments, --max-segments, --skip, --limit 1-200, --format table|json");
        AnsiConsole.MarkupLine("[bold]Global options:[/] --manifest path --repository-root path");
        AnsiConsole.MarkupLine("[grey]Example: dotnet run --project Prototype/AskARabbiPrototype -- search \"Shabbat fire\" --language English --collection Talmud --limit 10[/]");
    }

    private static void WritePlainContent(string title, string content)
    {
        AnsiConsole.Write(new Rule($"[bold cyan]{Markup.Escape(title)}[/]"));
        AnsiConsole.Write(new Text(content));
        AnsiConsole.WriteLine();
    }

    private static void WriteError(string message)
    {
        if (System.Console.IsOutputRedirected)
        {
            System.Console.Error.WriteLine($"Error: {message}");
            return;
        }
        var panel = new Panel(new Text(message)).Header("[bold red]Error[/]").Border(BoxBorder.Rounded).BorderColor(Color.Red);
        AnsiConsole.Write(panel);
    }

    private static void Pause()
    {
        AnsiConsole.Prompt(new TextPrompt<string>("[grey]Press Enter to continue[/]").AllowEmpty());
        AnsiConsole.WriteLine();
    }

    private static IReadOnlyCollection<string> ToFilter(string? value) => value is null ? Array.Empty<string>() : new[] { value };

    private static string FormatMainMenuAction(MainMenuAction action) => action switch
    {
        MainMenuAction.Search => "[cyan]Search documents[/]",
        MainMenuAction.BrowseFacets => "Browse available filters",
        MainMenuAction.ViewStatistics => "View manifest statistics",
        MainMenuAction.ReloadManifest => "Reload manifest",
        MainMenuAction.Exit => "[grey]Exit[/]",
        _ => action.ToString(),
    };

    private static string FormatDocumentAction(DocumentAction action) => action switch
    {
        DocumentAction.RawText => "View raw text only",
        DocumentAction.SourceMetadata => "View all source metadata",
        DocumentAction.OriginalJson => "View original Sefaria JSON",
        DocumentAction.NormalizedMarkdown => "View normalized Markdown",
        DocumentAction.ManifestMetadata => "View manifest metadata",
        DocumentAction.Back => "[grey]Back to main menu[/]",
        _ => action.ToString(),
    };

    private static string FormatSearchResultChoice(SearchResultChoice choice)
    {
        if (choice.Hit is null)
        {
            return "[grey]Back to main menu[/]";
        }
        return $"[cyan]{choice.Number}.[/] {Escape(choice.Hit.Document.FileTitle)} [grey]— {Escape(choice.Hit.Document.FileLanguage)}, {Escape(choice.Hit.Document.Collection)}[/]";
    }

    private static string FormatReferenceRange(ManifestDocument document)
    {
        if (document.FirstReference is null)
        {
            return "No reference range";
        }
        return string.Equals(document.FirstReference, document.LastReference, StringComparison.Ordinal)
            ? document.FirstReference
            : $"{document.FirstReference} – {document.LastReference}";
    }

    private static string FormatBytes(long bytes)
    {
        var units = new[] { "B", "KiB", "MiB", "GiB" };
        var value = (double)bytes;
        var unitIndex = 0;
        while (value >= 1024 && unitIndex < units.Length - 1)
        {
            value /= 1024;
            unitIndex++;
        }
        return $"{value:N2} {units[unitIndex]}";
    }

    private static string Escape(string? value) => Markup.Escape(string.IsNullOrEmpty(value) ? "—" : value);

    private enum MainMenuAction
    {
        Search,
        BrowseFacets,
        ViewStatistics,
        ReloadManifest,
        Exit,
    }

    private enum DocumentAction
    {
        RawText,
        SourceMetadata,
        OriginalJson,
        NormalizedMarkdown,
        ManifestMetadata,
        Back,
    }

    private sealed record FacetChoice(string? Value, int Count);

    private sealed record FacetGroup(string Name, IReadOnlyDictionary<string, int> Values);

    private sealed record SearchResultChoice(int Number, ManifestSearchHit? Hit);

    private sealed record ApplicationState(ManifestLocation Location, DocumentManifest Manifest, ManifestSearchIndex Index, SefariaDocumentFileLoader FileLoader, double LoadMilliseconds, double IndexMilliseconds);
}
