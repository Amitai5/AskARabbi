using AskARabbiLIB.Retrieval;
using Spectre.Console;

namespace AskARabbiPrototype;

internal sealed class SegmentIndexConsole
{
    private readonly ApplicationState state;
    private readonly CancellationToken cancellationToken;
    private readonly SourceIndexBuilder builder = new();

    internal SegmentIndexConsole(ApplicationState state, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(state);
        this.state = state;
        this.cancellationToken = cancellationToken;
    }

    internal Task<SourceIndexVerification> VerifyAsync()
    {
        return builder.VerifyAsync(state.Location.SegmentIndexPath, state.Manifest, cancellationToken);
    }

    internal async Task<bool> EnsureReadyInteractiveAsync()
    {
        var verification = await ConsolePresentation.RunWithStatusAsync("Verifying the local segment index...", VerifyAsync).ConfigureAwait(false);
        if (verification.IsValid)
        {
            return true;
        }

        ConsolePresentation.WriteError(verification.Message);
        if (!AnsiConsole.Confirm("Build the reproducible local segment index now?", true))
        {
            return false;
        }

        var statistics = await BuildAsync().ConfigureAwait(false);
        ConsolePresentation.PrintIndexStatistics(statistics, ConsoleOutputFormat.Table);
        return true;
    }

    internal async Task<SourceIndexStatistics> BuildAsync()
    {
        var provider = new SefariaNormalizedDocumentProvider(state.FileLoader);
        if (System.Console.IsOutputRedirected)
        {
            return await builder.BuildAsync(state.Manifest, provider, state.Location.SegmentIndexPath, cancellationToken: cancellationToken).ConfigureAwait(false);
        }

        SourceIndexStatistics? result = null;
        await AnsiConsole.Progress()
            .AutoClear(false)
            .Columns(new TaskDescriptionColumn(), new ProgressBarColumn(), new PercentageColumn(), new SpinnerColumn())
            .StartAsync(async context =>
            {
                var task = context.AddTask("[cyan]Indexing normalized source segments[/]", maxValue: state.Manifest.DocumentCount);
                var progress = new SynchronousProgress<SourceIndexProgress>(value =>
                {
                    task.MaxValue = value.DocumentCount;
                    task.Value = value.DocumentsCompleted;
                    task.Description = $"[cyan]{Markup.Escape(value.CurrentTitle)}[/] [grey]({value.SegmentsCompleted:N0} segments)[/]";
                });
                result = await builder.BuildAsync(state.Manifest, provider, state.Location.SegmentIndexPath, progress, cancellationToken).ConfigureAwait(false);
                task.Value = task.MaxValue;
            })
            .ConfigureAwait(false);

        return result ?? throw new InvalidOperationException("Segment index build completed without statistics.");
    }
}
