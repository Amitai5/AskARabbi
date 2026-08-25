using System.Diagnostics;
using System.Globalization;
using System.Text.Json;
using AskARabbiLIB.Files;
using AskARabbiLIB.Models;
using Spectre.Console;

namespace AskARabbiPrototype;

internal sealed class SourceSearchConsole
{
    private readonly ApplicationStateLoader stateLoader;
    private readonly CancellationToken cancellationToken;

    internal SourceSearchConsole(ApplicationStateLoader stateLoader, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(stateLoader);
        this.stateLoader = stateLoader;
        this.cancellationToken = cancellationToken;
    }

    internal async Task<ApplicationState> RunAsync(ApplicationState initialState)
    {
        ArgumentNullException.ThrowIfNull(initialState);
        var state = initialState;
        AnsiConsole.Write(new Rule("[bold cyan]Source Search[/]"));
        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                var action = AnsiConsole.Prompt(
                    new SelectionPrompt<SearchAction>()
                        .Title("Search and inspect the local source library:")
                        .UseConverter(FormatAction)
                        .AddChoices(SearchAction.Search, SearchAction.BrowseFacets, SearchAction.ReloadManifest, SearchAction.Back));
                try
                {
                    switch (action)
                    {
                        case SearchAction.Search:
                            await RunSearchWizardAsync(state).ConfigureAwait(false);
                            break;
                        case SearchAction.BrowseFacets:
                            BrowseFacets(state);
                            break;
                        case SearchAction.ReloadManifest:
                            var reloadedState = await stateLoader.LoadAsync(state.Location, showStatus: true).ConfigureAwait(false);
                            state.Dispose();
                            state = reloadedState;
                            ConsolePresentation.PrintBanner(state);
                            break;
                        case SearchAction.Back:
                            return state;
                        default:
                            throw new InvalidOperationException($"Unsupported source-search action: {action}.");
                    }
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    throw;
                }
                catch (Exception exception)
                {
                    ConsolePresentation.WriteError(exception.Message);
                    ConsolePresentation.Pause();
                }
            }

            return state;
        }
        catch
        {
            if (!ReferenceEquals(state, initialState))
            {
                state.Dispose();
            }

            throw;
        }
    }

    private async Task RunSearchWizardAsync(ApplicationState state)
    {
        var facets = state.SearchIndex.GetFacets();
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
        var result = state.SearchIndex.Search(query);
        stopwatch.Stop();
        ConsolePresentation.PrintSearchResults(result, stopwatch.Elapsed.TotalMilliseconds);
        if (result.Hits.Count == 0)
        {
            ConsolePresentation.Pause();
            return;
        }

        var selection = PromptForResult(result.Hits);
        if (selection.Hit is not null)
        {
            await BrowseDocumentAsync(state, selection.Hit.Document).ConfigureAwait(false);
        }
    }

    private async Task BrowseDocumentAsync(ApplicationState state, ManifestDocument document)
    {
        SefariaDocumentFile? sourceFile = null;
        ConsolePresentation.PrintDocumentMetadata(document);
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
                var markdown = await ConsolePresentation.RunWithStatusAsync("Loading and verifying normalized Markdown...", () => state.FileLoader.LoadNormalizedMarkdownAsync(document, cancellationToken)).ConfigureAwait(false);
                ConsolePresentation.WritePlainContent("Normalized Markdown", markdown);
                ConsolePresentation.Pause();
                continue;
            }

            if (action == DocumentAction.ManifestMetadata)
            {
                ConsolePresentation.PrintDocumentMetadata(document);
                ConsolePresentation.Pause();
                continue;
            }

            sourceFile ??= await ConsolePresentation.RunWithStatusAsync("Loading and verifying original Sefaria JSON...", () => state.FileLoader.LoadRawFileAsync(document, cancellationToken)).ConfigureAwait(false);
            switch (action)
            {
                case DocumentAction.RawText:
                    ConsolePresentation.WritePlainContent("Raw Text", sourceFile.GetRawText());
                    break;
                case DocumentAction.SourceMetadata:
                    ConsolePresentation.WritePlainContent("Source Metadata", JsonSerializer.Serialize(sourceFile.Metadata, ConsoleSerialization.JsonOptions));
                    break;
                case DocumentAction.OriginalJson:
                    ConsolePresentation.WritePlainContent("Original Sefaria JSON", sourceFile.RawJson);
                    break;
                default:
                    throw new InvalidOperationException($"Unsupported document action: {action}.");
            }

            ConsolePresentation.Pause();
        }
    }

    private static void BrowseFacets(ApplicationState state)
    {
        var facets = state.SearchIndex.GetFacets();
        FacetGroup[] groups =
        [
            new("Languages", facets.Languages),
            new("Collections", facets.Collections),
            new("Categories", facets.Categories),
            new("Licenses", facets.Licenses),
        ];
        var selected = AnsiConsole.Prompt(new SelectionPrompt<FacetGroup>().Title("Choose a facet to inspect:").UseConverter(group => group.Name).AddChoices(groups));
        var table = ConsolePresentation.CreateTable("Value", "Documents");
        foreach (var pair in selected.Values.OrderByDescending(pair => pair.Value).ThenBy(pair => pair.Key, StringComparer.OrdinalIgnoreCase))
        {
            table.AddRow(ConsolePresentation.Escape(pair.Key), pair.Value.ToString("N0", CultureInfo.CurrentCulture));
        }

        AnsiConsole.Write(new Rule($"[bold cyan]{ConsolePresentation.Escape(selected.Name)}[/]"));
        AnsiConsole.Write(table);
        ConsolePresentation.Pause();
    }

    private static string? PromptFacet(string name, IReadOnlyDictionary<string, int> values)
    {
        var choices = new List<FacetChoice> { new(null, 0) };
        choices.AddRange(values.OrderByDescending(pair => pair.Value).ThenBy(pair => pair.Key, StringComparer.OrdinalIgnoreCase).Select(pair => new FacetChoice(pair.Key, pair.Value)));
        return AnsiConsole.Prompt(
            new SelectionPrompt<FacetChoice>()
                .Title($"Choose a {Markup.Escape(name)}:")
                .PageSize(12)
                .EnableSearch()
                .SearchPlaceholderText("[grey]Type to filter choices[/]")
                .UseConverter(choice => choice.Value is null ? "[grey]Any[/]" : $"{ConsolePresentation.Escape(choice.Value)} [grey]({choice.Count:N0})[/]")
                .AddChoices(choices)).Value;
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

    private static string FormatAction(SearchAction action) => action switch
    {
        SearchAction.Search => "[cyan]Search documents[/]",
        SearchAction.BrowseFacets => "Browse available filters",
        SearchAction.ReloadManifest => "Reload manifest",
        SearchAction.Back => "[grey]Back to main menu[/]",
        _ => action.ToString(),
    };

    private static string FormatDocumentAction(DocumentAction action) => action switch
    {
        DocumentAction.RawText => "View raw text only",
        DocumentAction.SourceMetadata => "View all source metadata",
        DocumentAction.OriginalJson => "View original Sefaria JSON",
        DocumentAction.NormalizedMarkdown => "View normalized Markdown",
        DocumentAction.ManifestMetadata => "View manifest metadata",
        DocumentAction.Back => "[grey]Back to Source Search[/]",
        _ => action.ToString(),
    };

    private static string FormatSearchResultChoice(SearchResultChoice choice)
    {
        return choice.Hit is null
            ? "[grey]Back to Source Search[/]"
            : $"[cyan]{choice.Number}.[/] {ConsolePresentation.Escape(choice.Hit.Document.FileTitle)} [grey]— {ConsolePresentation.Escape(choice.Hit.Document.FileLanguage)}, {ConsolePresentation.Escape(choice.Hit.Document.Collection)}[/]";
    }

    private static IReadOnlyCollection<string> ToFilter(string? value) => value is null ? [] : [value];

    private enum SearchAction
    {
        Search,
        BrowseFacets,
        ReloadManifest,
        Back,
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
}
