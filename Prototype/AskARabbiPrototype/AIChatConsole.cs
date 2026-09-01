using AskARabbiLIB.AI.Tools;
using AskARabbiLIB.Calendar;
using AskARabbiLIB.Grounding;
using AskARabbiLIB.Profiles;
using AskARabbiLIB.Retrieval;
using Spectre.Console;

namespace AskARabbiPrototype;

internal sealed class AIChatConsole
{
    private const int MaximumQuestionLength = 4_000;

    private readonly ApplicationState state;
    private readonly CancellationToken cancellationToken;
    private readonly TimeProvider timeProvider;

    internal AIChatConsole(ApplicationState state, CancellationToken cancellationToken, TimeProvider? timeProvider = null)
    {
        ArgumentNullException.ThrowIfNull(state);
        this.state = state;
        this.cancellationToken = cancellationToken;
        this.timeProvider = timeProvider ?? TimeProvider.System;
    }

    internal async Task RunAsync()
    {
        var indexConsole = new SegmentIndexConsole(state, cancellationToken);
        if (!await indexConsole.EnsureReadyInteractiveAsync().ConfigureAwait(false))
        {
            return;
        }

        var currentDate = DateOnly.FromDateTime(timeProvider.GetLocalNow().DateTime);
        var userProfile = UserProfileConsole.Prompt(state.Location.RepositoryRoot, currentDate);
        if (userProfile is null)
        {
            return;
        }

        var engine = AIEngineFactory.Create(state.Configuration);
        await using var retriever = new SqliteSourceRetriever(state.Location.SegmentIndexPath, state.Manifest);
        var prompts = GroundedPromptFileLoader.Load(state.Location.RepositoryRoot);
        var toolRegistry = new AIToolRegistry([new CalendarAITools(new HebrewCalendarService())]);
        var answerService = new GroundedAnswerService(retriever, engine, prompts, timeProvider: timeProvider, toolRegistry: toolRegistry);
        var session = new InMemoryGroundedSession();
        var preferences = SourcePreferences.CreateDefault(state.SourceCatalog);
        GroundedAnswerResult? lastResult = null;

        PrintIntroduction(userProfile, currentDate, preferences);
        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                AnsiConsole.WriteLine();
                var input = AnsiConsole.Prompt(new TextPrompt<string>("[bold blue]You[/] [grey]›[/]").AllowEmpty()).Trim();
                if (input.Length == 0)
                {
                    continue;
                }

                if (TryHandleCommand(input, session, userProfile, currentDate, ref preferences, ref lastResult, out var shouldLeave))
                {
                    if (shouldLeave)
                    {
                        return;
                    }

                    continue;
                }

                if (input.StartsWith("/", StringComparison.Ordinal))
                {
                    AnsiConsole.MarkupLine("[yellow]Unknown chat command. Type [cyan]/help[/] to see the available commands.[/]");
                    continue;
                }

                if (input.Length > MaximumQuestionLength)
                {
                    AnsiConsole.MarkupLine($"[red]Questions must contain no more than {MaximumQuestionLength:N0} characters.[/]");
                    continue;
                }

                var question = preferences.CreateQuestion(input, userProfile);
                lastResult = await ConsolePresentation.RunWithStatusAsync(
                    "Finding relevant approved sources and preparing a grounded answer...",
                    () => answerService.AnswerAsync(question, session.GetTurns(), cancellationToken))
                    .ConfigureAwait(false);
                ConsolePresentation.PrintGroundedResult(lastResult);
                if (lastResult is { IsSuccess: true, Answer: not null })
                {
                    session.Add(input, lastResult.Answer);
                }
            }
        }
        finally
        {
            session.Clear();
            lastResult = null;
        }
    }

    private bool TryHandleCommand(string input, InMemoryGroundedSession session, UserProfile userProfile, DateOnly currentDate, ref SourcePreferences preferences, ref GroundedAnswerResult? lastResult, out bool shouldLeave)
    {
        shouldLeave = false;
        switch (input.ToLowerInvariant())
        {
            case "/help":
                PrintHelp();
                return true;
            case "/sources":
                preferences = PromptSourcePreferences(preferences);
                AnsiConsole.MarkupLine($"[grey]Sources updated: {ConsolePresentation.Escape(ConsolePresentation.FormatPreferences(preferences, state.SourceCatalog))}[/]");
                ConsolePresentation.PrintSourceInventory(state.SourceCatalog, preferences);
                return true;
            case "/profile":
                AnsiConsole.MarkupLine($"[grey]Current profile — {ConsolePresentation.Escape(UserProfileConsole.FormatSummary(userProfile, currentDate))}[/]");
                if (userProfile.Bio is not null)
                {
                    AnsiConsole.MarkupLine($"[grey]Bio — {ConsolePresentation.Escape(userProfile.Bio)}[/]");
                }

                return true;
            case "/evidence":
                if (lastResult?.Evidence is null)
                {
                    AnsiConsole.MarkupLine("[yellow]No retrieval has run in this session.[/]");
                }
                else
                {
                    ConsolePresentation.PrintEvidence(lastResult.Evidence);
                }

                return true;
            case "/trace":
                if (lastResult is null)
                {
                    AnsiConsole.MarkupLine("[yellow]No run trace is available.[/]");
                }
                else
                {
                    ConsolePresentation.PrintTrace(lastResult.Trace);
                }

                return true;
            case "/clear":
                session.Clear();
                lastResult = null;
                AnsiConsole.MarkupLine("[green]The in-memory conversation, answer, evidence, and trace were cleared. Your selected profile remains active.[/]");
                return true;
            case "/back":
            case "/exit":
            case "/quit":
                shouldLeave = true;
                AnsiConsole.MarkupLine("[grey]Chat session memory cleared.[/]");
                return true;
            default:
                return false;
        }
    }

    private void PrintIntroduction(UserProfile userProfile, DateOnly currentDate, SourcePreferences preferences)
    {
        AnsiConsole.WriteLine();
        AnsiConsole.MarkupLine("[bold green]AskARabbi AI Chat[/]");
        AnsiConsole.MarkupLine("[grey]Ask naturally and continue with follow-up questions. Answers use validated quotations and source context.[/]");
        AnsiConsole.MarkupLine("[grey]Nothing in this conversation is persisted. Type [cyan]/help[/] for commands or [cyan]/back[/] to leave.[/]");
        AnsiConsole.MarkupLine($"[grey]Personalization — {ConsolePresentation.Escape(UserProfileConsole.FormatSummary(userProfile, currentDate))}[/]");
        AnsiConsole.MarkupLine("[grey]Only your calculated age—not your birth date—is sent to the model. Profile details personalize wording and never count as evidence.[/]");
        AnsiConsole.MarkupLine($"[grey]Source filters — {ConsolePresentation.Escape(ConsolePresentation.FormatPreferences(preferences, state.SourceCatalog))}[/]");
    }

    private SourcePreferences PromptSourcePreferences(SourcePreferences current)
    {
        var facets = state.SearchIndex.GetFacets();
        var catalog = state.SourceCatalog;
        AnsiConsole.MarkupLine($"[grey]Current: {ConsolePresentation.Escape(ConsolePresentation.FormatPreferences(current, catalog))}[/]");
        var enabledKeys = current.EnabledSourceKeys.ToHashSet(StringComparer.OrdinalIgnoreCase);
        var prompt = new MultiSelectionPrompt<DocumentSourceSummary>()
            .Title("Turn sources on or off for subsequent answers:")
            .Required()
            .PageSize(15)
            .InstructionsText("[grey](Press [blue]<space>[/] to toggle a source, then [green]<enter>[/] to accept.)[/]")
            .UseConverter(source => $"{ConsolePresentation.Escape(source.DisplayName)} [grey]({source.DocumentCount:N0} editions, {source.SegmentCount:N0} passages)[/]")
            .AddChoices(catalog.Sources);
        foreach (var source in catalog.Sources.Where(source => enabledKeys.Contains(source.Key)))
        {
            prompt.Select(source);
        }

        var selectedSources = AnsiConsole.Prompt(prompt);
        var language = PromptFacet("language", facets.Languages);
        var category = PromptFacet("category", facets.Categories);
        return new SourcePreferences(selectedSources.Select(source => source.Key).ToArray(), language, category);
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

    private static void PrintHelp()
    {
        AnsiConsole.MarkupLine("[cyan]/sources[/]  Choose approved sources and optional language/category filters");
        AnsiConsole.MarkupLine("[cyan]/profile[/]  Show the personalization context active for this chat");
        AnsiConsole.MarkupLine("[cyan]/evidence[/] Show the exact evidence packet used for the last response");
        AnsiConsole.MarkupLine("[cyan]/trace[/]    Show retrieval, model, token, and validation diagnostics");
        AnsiConsole.MarkupLine("[cyan]/clear[/]    Forget the current in-memory conversation");
        AnsiConsole.MarkupLine("[cyan]/back[/]     Forget the conversation and return to the main menu");
    }

    private sealed record FacetChoice(string? Value, int Count);
}
