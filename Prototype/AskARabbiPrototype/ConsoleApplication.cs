using Spectre.Console;

namespace AskARabbiPrototype;

internal sealed class ConsoleApplication
{
    private readonly ConsoleCommandParser parser = new();
    private readonly CancellationToken cancellationToken;

    internal ConsoleApplication(CancellationToken cancellationToken)
    {
        this.cancellationToken = cancellationToken;
    }

    internal async Task<int> RunAsync(string[] arguments)
    {
        ArgumentNullException.ThrowIfNull(arguments);
        var parseResult = parser.Parse(arguments);
        if (parseResult is not { IsSuccess: true, Command: { } command })
        {
            ConsolePresentation.WriteError(parseResult.Error ?? "The command could not be parsed.");
            return 2;
        }

        if (command.Kind == ConsoleCommandKind.Help)
        {
            ConsolePresentation.PrintHelp();
            return 0;
        }

        if (command.Kind == ConsoleCommandKind.Interactive && System.Console.IsInputRedirected)
        {
            ConsolePresentation.PrintHelp();
            return 0;
        }

        ApplicationState? state = null;
        try
        {
            var location = ManifestPathResolver.Resolve(command.ManifestPath, command.RepositoryRoot, command.IndexPath);
            var stateLoader = new ApplicationStateLoader(cancellationToken);
            state = await stateLoader.LoadAsync(location, showStatus: command.Kind == ConsoleCommandKind.Interactive).ConfigureAwait(false);
            if (command.Kind != ConsoleCommandKind.Interactive)
            {
                return await new OneShotCommandExecutor(state, cancellationToken).ExecuteAsync(command).ConfigureAwait(false);
            }

            ConsolePresentation.PrintBanner(state);
            return await RunInteractiveAsync(state, stateLoader).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            ConsolePresentation.WriteError("Operation canceled.");
            return 1;
        }
        catch (Exception exception)
        {
            ConsolePresentation.WriteError(exception.Message);
            return 1;
        }
        finally
        {
            state?.Dispose();
        }
    }

    private async Task<int> RunInteractiveAsync(ApplicationState initialState, ApplicationStateLoader stateLoader)
    {
        var state = initialState;
        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                var action = AnsiConsole.Prompt(
                    new SelectionPrompt<TopLevelAction>()
                        .Title("[bold cyan]What would you like to do?[/]")
                        .UseConverter(FormatAction)
                        .DefaultValue(TopLevelAction.AIChat)
                        .AddChoices(TopLevelAction.AIChat, TopLevelAction.SourceSearch, TopLevelAction.Statistics, TopLevelAction.Exit));
                try
                {
                    switch (action)
                    {
                        case TopLevelAction.AIChat:
                            await new AIChatConsole(state, cancellationToken).RunAsync().ConfigureAwait(false);
                            break;
                        case TopLevelAction.SourceSearch:
                            state = await new SourceSearchConsole(stateLoader, cancellationToken).RunAsync(state).ConfigureAwait(false);
                            break;
                        case TopLevelAction.Statistics:
                            await ApplicationStatisticsConsole.PrintAsync(state, ConsoleOutputFormat.Table, cancellationToken).ConfigureAwait(false);
                            ConsolePresentation.Pause();
                            break;
                        case TopLevelAction.Exit:
                            AnsiConsole.MarkupLine("[grey]Session memory cleared. Goodbye.[/]");
                            return 0;
                        default:
                            throw new InvalidOperationException($"Unsupported top-level action: {action}.");
                    }
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    return 0;
                }
                catch (Exception exception)
                {
                    ConsolePresentation.WriteError(exception.Message);
                    ConsolePresentation.Pause();
                }
            }

            return 0;
        }
        finally
        {
            if (!ReferenceEquals(state, initialState))
            {
                state.Dispose();
            }
        }
    }

    private static string FormatAction(TopLevelAction action) => action switch
    {
        TopLevelAction.AIChat => "[bold green]Start AI chat[/] — ask a source-grounded question",
        TopLevelAction.SourceSearch => "[cyan]Search the source library[/]",
        TopLevelAction.Statistics => "Statistics",
        TopLevelAction.Exit => "[grey]Exit[/]",
        _ => action.ToString(),
    };

    private enum TopLevelAction
    {
        AIChat,
        SourceSearch,
        Statistics,
        Exit,
    }
}
