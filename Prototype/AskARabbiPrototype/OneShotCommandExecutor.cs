using System.Diagnostics;
using System.Text.Json;
using AskARabbiLIB.AI.Tools;
using AskARabbiLIB.Calendar;
using AskARabbiLIB.Grounding;
using AskARabbiLIB.Retrieval;

namespace AskARabbiPrototype;

internal sealed class OneShotCommandExecutor
{
    private readonly ApplicationState state;
    private readonly CancellationToken cancellationToken;
    private readonly TimeProvider timeProvider;

    internal OneShotCommandExecutor(ApplicationState state, CancellationToken cancellationToken, TimeProvider? timeProvider = null)
    {
        ArgumentNullException.ThrowIfNull(state);
        this.state = state;
        this.cancellationToken = cancellationToken;
        this.timeProvider = timeProvider ?? TimeProvider.System;
    }

    internal async Task<int> ExecuteAsync(ConsoleCommand command)
    {
        ArgumentNullException.ThrowIfNull(command);
        switch (command.Kind)
        {
            case ConsoleCommandKind.Search:
                ExecuteSearch(command);
                return 0;
            case ConsoleCommandKind.Facets:
                ConsolePresentation.PrintFacets(state.SearchIndex.GetFacets(), command.OutputFormat);
                return 0;
            case ConsoleCommandKind.Stats:
                await ApplicationStatisticsConsole.PrintAsync(state, command.OutputFormat, cancellationToken).ConfigureAwait(false);
                return 0;
            case ConsoleCommandKind.IndexBuild:
                var statistics = await new SegmentIndexConsole(state, cancellationToken).BuildAsync().ConfigureAwait(false);
                ConsolePresentation.PrintIndexStatistics(statistics, command.OutputFormat);
                return 0;
            case ConsoleCommandKind.IndexVerify:
            case ConsoleCommandKind.IndexStats:
                var verification = await new SegmentIndexConsole(state, cancellationToken).VerifyAsync().ConfigureAwait(false);
                ConsolePresentation.PrintIndexVerification(verification, command.OutputFormat);
                return verification.IsValid ? 0 : 1;
            case ConsoleCommandKind.Ask:
                return await ExecuteAskAsync(command).ConfigureAwait(false);
            default:
                ConsolePresentation.WriteError($"Command '{command.Kind}' is unavailable in one-shot mode.");
                return 2;
        }
    }

    private async Task<int> ExecuteAskAsync(ConsoleCommand command)
    {
        var verification = await new SegmentIndexConsole(state, cancellationToken).VerifyAsync().ConfigureAwait(false);
        if (!verification.IsValid)
        {
            ConsolePresentation.WriteError($"{verification.Message} Run 'index build' first.");
            return 1;
        }

        var engine = AIEngineFactory.Create(state.Configuration);
        await using var retriever = new SqliteSourceRetriever(state.Location.SegmentIndexPath, state.Manifest);
        var prompts = GroundedPromptFileLoader.Load(state.Location.RepositoryRoot);
        var toolRegistry = new AIToolRegistry([new CalendarAITools(new HebrewCalendarService())]);
        var service = new GroundedAnswerService(retriever, engine, prompts, timeProvider: timeProvider, toolRegistry: toolRegistry);
        var question = command.GroundedQuestion ?? throw new InvalidOperationException("Ask command is missing its question.");
        if (command.ProfileFileName is not null)
        {
            var currentDate = DateOnly.FromDateTime(timeProvider.GetLocalNow().DateTime);
            question = question with { UserProfile = UserProfileConsole.Load(state.Location.RepositoryRoot, command.ProfileFileName, currentDate) };
        }

        var result = await service.AnswerAsync(question, [], cancellationToken).ConfigureAwait(false);
        if (command.OutputFormat == ConsoleOutputFormat.Json)
        {
            System.Console.WriteLine(JsonSerializer.Serialize(result, ConsoleSerialization.JsonOptions));
        }
        else
        {
            ConsolePresentation.PrintGroundedResult(result);
            if (result.Evidence is not null)
            {
                ConsolePresentation.PrintEvidence(result.Evidence);
            }

            ConsolePresentation.PrintTrace(result.Trace);
        }

        return result.IsSuccess ? 0 : 1;
    }

    private void ExecuteSearch(ConsoleCommand command)
    {
        var query = command.Query ?? throw new InvalidOperationException("Search command is missing its query.");
        var stopwatch = Stopwatch.StartNew();
        var result = state.SearchIndex.Search(query);
        stopwatch.Stop();
        if (command.OutputFormat == ConsoleOutputFormat.Json)
        {
            var output = new
            {
                result.TotalMatches,
                result.Skip,
                result.Limit,
                ElapsedMilliseconds = stopwatch.Elapsed.TotalMilliseconds,
                result.Hits,
            };
            System.Console.WriteLine(JsonSerializer.Serialize(output, ConsoleSerialization.JsonOptions));
            return;
        }

        ConsolePresentation.PrintSearchResults(result, stopwatch.Elapsed.TotalMilliseconds);
    }
}
