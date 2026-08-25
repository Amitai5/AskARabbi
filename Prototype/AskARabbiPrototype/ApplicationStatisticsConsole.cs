using System.Globalization;
using System.Text.Json;
using Spectre.Console;

namespace AskARabbiPrototype;

internal static class ApplicationStatisticsConsole
{
    internal static async Task PrintAsync(ApplicationState state, ConsoleOutputFormat outputFormat, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(state);
        var verification = await new SegmentIndexConsole(state, cancellationToken).VerifyAsync().ConfigureAwait(false);
        var statistics = new
        {
            state.Manifest.SchemaVersion,
            state.Manifest.SourceProvider,
            state.Manifest.GeneratedAtUtc,
            state.Manifest.DocumentCount,
            SourceCount = state.SourceCatalog.SourceCount,
            Sources = state.SourceCatalog.Sources,
            state.LoadMilliseconds,
            state.SearchIndexMilliseconds,
            ApproximateManagedMemoryBytes = GC.GetTotalMemory(false),
            state.Location.ManifestPath,
            state.Location.RepositoryRoot,
            state.Location.SegmentIndexPath,
            SegmentIndexValid = verification.IsValid,
            SegmentIndexMessage = verification.Message,
            SegmentIndex = verification.Statistics,
        };

        if (outputFormat == ConsoleOutputFormat.Json)
        {
            System.Console.WriteLine(JsonSerializer.Serialize(statistics, ConsoleSerialization.JsonOptions));
            return;
        }

        var table = ConsolePresentation.CreateTable("Metric", "Value");
        table.AddRow("Manifest schema", ConsolePresentation.Escape(statistics.SchemaVersion));
        table.AddRow("Provider", ConsolePresentation.Escape(statistics.SourceProvider));
        table.AddRow("Generated", ConsolePresentation.Escape(statistics.GeneratedAtUtc.ToString("u", CultureInfo.InvariantCulture)));
        table.AddRow("Documents", statistics.DocumentCount.ToString("N0", CultureInfo.CurrentCulture));
        table.AddRow("Logical sources", statistics.SourceCount.ToString("N0", CultureInfo.CurrentCulture));
        table.AddRow("Manifest load", $"{statistics.LoadMilliseconds:N2} ms");
        table.AddRow("Document search index", $"{statistics.SearchIndexMilliseconds:N2} ms");
        table.AddRow("Managed memory", ConsolePresentation.FormatBytes(statistics.ApproximateManagedMemoryBytes));
        table.AddRow("Segment index", verification.IsValid ? "[green]Valid[/]" : $"[yellow]{ConsolePresentation.Escape(verification.Message)}[/]");
        if (verification.Statistics is not null)
        {
            table.AddRow("Indexed segments", verification.Statistics.SegmentCount.ToString("N0", CultureInfo.CurrentCulture));
            table.AddRow("SQLite size", ConsolePresentation.FormatBytes(verification.Statistics.FileSizeBytes));
        }

        table.AddRow("Manifest", ConsolePresentation.Escape(statistics.ManifestPath));
        table.AddRow("SQLite", ConsolePresentation.Escape(statistics.SegmentIndexPath));
        AnsiConsole.Write(table);
    }
}
