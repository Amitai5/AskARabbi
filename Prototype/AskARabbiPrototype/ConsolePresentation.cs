using System.Globalization;
using System.Text;
using System.Text.Json;
using AskARabbiLIB.Grounding;
using AskARabbiLIB.Models;
using AskARabbiLIB.Retrieval;
using Spectre.Console;

namespace AskARabbiPrototype;

internal static class ConsolePresentation
{
    internal static void PrintBanner(ApplicationState state)
    {
        ArgumentNullException.ThrowIfNull(state);
        AnsiConsole.Write(new FigletText("Ask A Rabbi").Color(Color.Cyan1));
        AnsiConsole.MarkupLine("[bold green]Conversational, source-grounded Jewish learning[/]");
        AnsiConsole.MarkupLine($"[grey]Loaded {state.SourceCatalog.SourceCount:N0} logical sources, {state.SourceCatalog.DocumentCount:N0} editions, and {state.SourceCatalog.SegmentCount:N0} searchable passages in {state.LoadMilliseconds:N2} ms; document search indexed in {state.SearchIndexMilliseconds:N2} ms.[/]");
        AnsiConsole.MarkupLine($"[grey]Sources: {Escape(string.Join(", ", state.SourceCatalog.Sources.Select(source => source.DisplayName)))}.[/]");
        AnsiConsole.MarkupLine("[grey]AI chat is non-persistent and fails closed when quotations, citations, or claim support cannot be validated.[/]");
        AnsiConsole.WriteLine();
    }

    internal static void PrintHelp()
    {
        AnsiConsole.Write(new FigletText("Ask A Rabbi").Color(Color.Cyan1));
        AnsiConsole.MarkupLine("Run without a command for the conversational chat and local source search. One-shot commands are available for scripts and agents.");
        var table = CreateTable("Command", "Purpose");
        table.AddRow($"[cyan]search[/] {Escape("[keywords] [options]")}", "Search and rank manifest documents");
        table.AddRow($"[cyan]facets[/] {Escape("[--format table|json]")}", "List document facets");
        table.AddRow($"[cyan]stats[/] {Escape("[--format table|json]")}", "Show corpus, memory, and segment-index status");
        table.AddRow($"[cyan]index build|verify|stats[/] {Escape("[--format table|json]")}", "Build or inspect the reproducible SQLite FTS5 index");
        table.AddRow($"[cyan]ask[/] {Escape("\"question\" [filters] [--profile file.json] [--format table|json]")}", "Retrieve evidence and request a validated grounded answer");
        table.AddRow("[cyan]help[/]", "Show this help");
        AnsiConsole.Write(table);
        AnsiConsole.MarkupLine("[bold]Search options:[/] --language, --collection, --category, --title, --version, --license, --match all|any, --min-segments, --max-segments, --skip, --limit 1-200, --format table|json");
        AnsiConsole.MarkupLine("[bold]Ask options:[/] --source, --language, --collection, --category, --work, --profile file.json, --format table|json");
        AnsiConsole.MarkupLine("[bold]Global options:[/] --manifest path --repository-root path --index path");
        AnsiConsole.MarkupLine("[bold]AI configuration:[/] AI__ProjectEndpoint, AI__ModelName, and AI__APIKey.");
        AnsiConsole.MarkupLine("[grey]Example: dotnet run --project Prototype/AskARabbiPrototype -- ask \"Why is poultry kept separate from milk?\" --language English[/]");
    }

    internal static async Task<T> RunWithStatusAsync<T>(string message, Func<Task<T>> operation)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(message);
        ArgumentNullException.ThrowIfNull(operation);
        if (System.Console.IsOutputRedirected)
        {
            return await operation().ConfigureAwait(false);
        }

        return await AnsiConsole.Status()
            .Spinner(Spinner.Known.Dots)
            .SpinnerStyle(Style.Parse("cyan"))
            .StartAsync(Markup.Escape(message), _ => operation())
            .ConfigureAwait(false);
    }

    internal static void PrintSearchResults(ManifestSearchResult result, double elapsedMilliseconds)
    {
        ArgumentNullException.ThrowIfNull(result);
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

    internal static void PrintFacets(ManifestFacetSummary facets, ConsoleOutputFormat outputFormat)
    {
        ArgumentNullException.ThrowIfNull(facets);
        if (outputFormat == ConsoleOutputFormat.Json)
        {
            System.Console.WriteLine(JsonSerializer.Serialize(facets, ConsoleSerialization.JsonOptions));
            return;
        }

        var table = CreateTable("Facet", "Value", "Documents");
        AddFacetRows(table, "Language", facets.Languages);
        AddFacetRows(table, "Collection", facets.Collections);
        AddFacetRows(table, "Category", facets.Categories);
        AddFacetRows(table, "License", facets.Licenses);
        AnsiConsole.Write(table);
    }

    internal static void PrintGroundedResult(GroundedAnswerResult result, string heading = "AskARabbi AI")
    {
        ArgumentNullException.ThrowIfNull(result);
        AnsiConsole.WriteLine();
        AnsiConsole.MarkupLine($"[bold green]{Markup.Escape(heading)}[/]");
        AnsiConsole.WriteLine();
        if (!result.IsSuccess || result.Answer is null)
        {
            var color = result.Status == GroundedAnswerStatus.InsufficientEvidence ? "yellow" : "red";
            AnsiConsole.MarkupLine($"[{color}]{Escape(result.Status.ToString())}: {Escape(result.ErrorMessage)}[/]");
            return;
        }

        var renderedQuotations = new HashSet<string>(StringComparer.Ordinal);
        for (var claimIndex = 0; claimIndex < result.Answer.Claims.Count; claimIndex++)
        {
            var claim = result.Answer.Claims[claimIndex];
            PrintGroundedStatement(claim.Text, claim.Citations, claim.Quotations, claimIndex == 0, renderedQuotations);
        }

        if (result.Answer.Disagreements.Count > 0)
        {
            AnsiConsole.MarkupLine("[bold magenta]Another perspective[/]");
            AnsiConsole.WriteLine();
            foreach (var disagreement in result.Answer.Disagreements)
            {
                PrintGroundedStatement(disagreement.Text, disagreement.Citations, disagreement.Quotations, false, renderedQuotations);
            }
        }

        if (result.Answer.Limitations.Count > 0)
        {
            AnsiConsole.MarkupLine($"[grey]What these sources do not fully answer:[/] {Escape(string.Join(' ', result.Answer.Limitations))}");
            AnsiConsole.WriteLine();
        }

        if (result.Answer.ClarifyingQuestion is not null)
        {
            AnsiConsole.MarkupLine($"[bold cyan]If you'd like to keep exploring:[/] {Escape(result.Answer.ClarifyingQuestion)}");
            AnsiConsole.WriteLine();
        }

        if (result.Answer.HumanGuidanceRecommended)
        {
            AnsiConsole.MarkupLine("[yellow]Because the practical answer may depend on your circumstances, talk it through with a qualified rabbi who knows your situation.[/]");
            AnsiConsole.WriteLine();
        }

        PrintInterpretiveNotice(result.Answer.InterpretiveNotice);
    }

    internal static void PrintEvidence(EvidencePacket packet)
    {
        ArgumentNullException.ThrowIfNull(packet);
        AnsiConsole.MarkupLine($"[bold cyan]Exact evidence packet[/] [grey]— {packet.Items.Count} segment(s), {packet.CharacterCount:N0} characters[/]");
        AnsiConsole.WriteLine();
        foreach (var item in packet.Items)
        {
            var excerptLabel = item.IsExcerpt ? $" [yellow]explicit excerpt of {item.OriginalCharacterCount:N0} characters[/]" : string.Empty;
            AnsiConsole.MarkupLine($"[bold blue]{Escape(item.EvidenceId)}[/] — {Escape(item.Source.Title)}, {Escape(item.Source.CanonicalReference)} ({Escape(item.Source.Language)}, {Escape(item.Source.Version)}){excerptLabel}");
            AnsiConsole.MarkupLine($"[grey]{Escape(item.PresentedText)}[/]");
            AnsiConsole.WriteLine();
        }
    }

    internal static void PrintTrace(GroundedAnswerTrace trace)
    {
        ArgumentNullException.ThrowIfNull(trace);
        AnsiConsole.MarkupLine("[bold cyan]Last-run trace[/]");
        AnsiConsole.MarkupLine($"[grey]Retrieval:[/] {trace.RetrievalLatency.TotalMilliseconds:N2} ms • [grey]Model:[/] {trace.ModelLatency.TotalMilliseconds:N2} ms");
        AnsiConsole.MarkupLine($"[grey]Candidates:[/] {trace.CandidateCount:N0} • [grey]Evidence:[/] {trace.EvidenceCount:N0} segments / {trace.EvidenceCharacterCount:N0} characters");
        AnsiConsole.MarkupLine($"[grey]Validation:[/] {Escape(trace.ValidationStatus.ToString())} • [grey]Repair attempted:[/] {(trace.RepairAttempted ? "Yes" : "No")}");
        AnsiConsole.MarkupLine($"[grey]Model:[/] {Escape(trace.Model)} • [grey]Response ID:[/] {Escape(trace.ResponseId)}");
        AnsiConsole.MarkupLine(trace.Usage is null
            ? "[grey]Token usage:[/] Unavailable"
            : $"[grey]Token usage:[/] {trace.Usage.InputTokens:N0} input / {trace.Usage.OutputTokens:N0} output / {trace.Usage.TotalTokens:N0} total");
    }

    internal static void PrintIndexStatistics(SourceIndexStatistics statistics, ConsoleOutputFormat outputFormat)
    {
        ArgumentNullException.ThrowIfNull(statistics);
        if (outputFormat == ConsoleOutputFormat.Json)
        {
            System.Console.WriteLine(JsonSerializer.Serialize(statistics, ConsoleSerialization.JsonOptions));
            return;
        }

        var table = CreateTable("Index metric", "Value");
        table.AddRow("Schema", Escape(statistics.SchemaVersion));
        table.AddRow("Fingerprint", Escape(statistics.CorpusFingerprint));
        table.AddRow("Documents", statistics.DocumentCount.ToString("N0", CultureInfo.CurrentCulture));
        table.AddRow("Segments", statistics.SegmentCount.ToString("N0", CultureInfo.CurrentCulture));
        table.AddRow("File size", FormatBytes(statistics.FileSizeBytes));
        AnsiConsole.Write(table);
    }

    internal static void PrintIndexVerification(SourceIndexVerification verification, ConsoleOutputFormat outputFormat)
    {
        ArgumentNullException.ThrowIfNull(verification);
        if (outputFormat == ConsoleOutputFormat.Json)
        {
            System.Console.WriteLine(JsonSerializer.Serialize(verification, ConsoleSerialization.JsonOptions));
            return;
        }

        AnsiConsole.MarkupLine(verification.IsValid
            ? $"[green]{Escape(verification.Message)}[/]"
            : $"[red]{Escape(verification.Message)}[/]");
        if (verification.Statistics is not null)
        {
            PrintIndexStatistics(verification.Statistics, ConsoleOutputFormat.Table);
        }
    }

    internal static void PrintDocumentMetadata(ManifestDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);
        AnsiConsole.Write(new Rule($"[bold cyan]{Escape(document.FileTitle)}[/]"));
        var table = CreateTable("Field", "Value");
        table.AddRow("Document ID", Escape(document.DocumentId));
        table.AddRow("Title", Escape(document.FileTitle));
        table.AddRow("Hebrew title", Escape(document.HebrewTitle));
        table.AddRow("Description", Escape(document.FileDescription));
        table.AddRow("Language", Escape($"{document.FileLanguage} ({document.FileLanguageCode})"));
        table.AddRow("Collection", Escape(document.Collection));
        table.AddRow("Categories", Escape(string.Join(" > ", document.Categories)));
        table.AddRow("Version", Escape(document.VersionTitle));
        table.AddRow("References", Escape(FormatReferenceRange(document)));
        table.AddRow("Segments", document.SegmentCount.ToString("N0", CultureInfo.CurrentCulture));
        table.AddRow("License", Escape(document.License));
        table.AddRow("License category", Escape(SourceLicensePolicy.GetDisplayName(document.LicenseCategory)));
        table.AddRow("Attribution required", document.RequiresAttribution ? "Yes" : "No");
        table.AddRow("ShareAlike required", document.RequiresShareAlike ? "Yes" : "No");
        table.AddRow("Original source", $"[link={EscapeLinkTarget(document.AttributionUrl)}]{Escape(document.VersionTitle)}[/]");
        table.AddRow("Normalized file", Escape(document.FilePath));
        table.AddRow("Raw file", Escape(document.RawFilePath));
        table.AddRow("Source URL", Escape(document.SourceUrl));
        AnsiConsole.Write(table);
    }

    internal static void PrintSourceInventory(DocumentSourceCatalog catalog, SourcePreferences preferences)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        ArgumentNullException.ThrowIfNull(preferences);
        var enabledKeys = preferences.EnabledSourceKeys.ToHashSet(StringComparer.OrdinalIgnoreCase);
        AnsiConsole.WriteLine();
        AnsiConsole.MarkupLine($"[bold cyan]Source inventory[/] [grey]— {enabledKeys.Count:N0} of {catalog.SourceCount:N0} enabled for each answer[/]");
        var table = CreateTable("Use", "Source", "Editions", "Passages", "Languages");
        foreach (var source in catalog.Sources)
        {
            var status = enabledKeys.Contains(source.Key) ? "[green]On[/]" : "[grey]Off[/]";
            table.AddRow(status, Escape(source.DisplayName), source.DocumentCount.ToString("N0", CultureInfo.CurrentCulture), source.SegmentCount.ToString("N0", CultureInfo.CurrentCulture), Escape(string.Join(", ", source.Languages)));
        }

        AnsiConsole.Write(table);
        AnsiConsole.MarkupLine("[grey]Type [cyan]/sources[/] before a question to change this selection.[/]");
    }

    internal static void WritePlainContent(string title, string content)
    {
        AnsiConsole.Write(new Rule($"[bold cyan]{Markup.Escape(title)}[/]"));
        AnsiConsole.Write(new Text(content));
        AnsiConsole.WriteLine();
    }

    internal static void WriteError(string message)
    {
        if (System.Console.IsOutputRedirected)
        {
            System.Console.Error.WriteLine($"Error: {message}");
            return;
        }

        AnsiConsole.MarkupLine($"[bold red]Error:[/] [red]{Escape(message)}[/]");
    }

    internal static void Pause()
    {
        AnsiConsole.Prompt(new TextPrompt<string>("[grey]Press Enter to continue[/]").AllowEmpty());
        AnsiConsole.WriteLine();
    }

    internal static Table CreateTable(params string[] columns)
    {
        var table = new Table().Border(TableBorder.Rounded).BorderColor(Color.Grey).Expand();
        foreach (var column in columns)
        {
            table.AddColumn(new TableColumn($"[bold cyan]{Markup.Escape(column)}[/]"));
        }

        return table;
    }

    internal static string FormatPreferences(SourcePreferences preferences, DocumentSourceCatalog catalog)
    {
        return $"sources: {preferences.EnabledSourceKeys.Count:N0}/{catalog.SourceCount:N0} enabled; language: {preferences.Language ?? "any"}; category: {preferences.Category ?? "any"}";
    }

    internal static string FormatReferenceRange(ManifestDocument document)
    {
        if (document.FirstReference is null)
        {
            return "No reference range";
        }

        return string.Equals(document.FirstReference, document.LastReference, StringComparison.Ordinal)
            ? document.FirstReference
            : $"{document.FirstReference} – {document.LastReference}";
    }

    internal static string FormatBytes(long bytes)
    {
        string[] units = ["B", "KiB", "MiB", "GiB"];
        var value = (double)bytes;
        var unitIndex = 0;
        while (value >= 1024 && unitIndex < units.Length - 1)
        {
            value /= 1024;
            unitIndex++;
        }

        return $"{value:N2} {units[unitIndex]}";
    }

    internal static string Escape(string? value) => Markup.Escape(string.IsNullOrEmpty(value) ? "—" : value);

    private static void PrintGroundedStatement(string text, IReadOnlyList<SourceCitation> citations, IReadOnlyList<GroundedQuotation> quotations, bool emphasize, ISet<string> renderedQuotations)
    {
        var inlineQuotations = quotations.Where(quotation => !string.IsNullOrEmpty(quotation.Text) && text.Contains(quotation.Text, StringComparison.Ordinal)).ToArray();
        foreach (var quotation in inlineQuotations)
        {
            renderedQuotations.Add(GetQuotationKey(quotation));
        }

        AnsiConsole.MarkupLine($"{FormatGroundedStatementText(text, inlineQuotations, emphasize)} {FormatInlineCitations(citations)}");
        AnsiConsole.WriteLine();
        foreach (var quotation in quotations)
        {
            if (renderedQuotations.Add(GetQuotationKey(quotation)))
            {
                PrintQuotation(quotation);
            }
        }
    }

    private static string FormatGroundedStatementText(string text, IReadOnlyList<GroundedQuotation> inlineQuotations, bool emphasize)
    {
        var ranges = FindInlineQuotationRanges(text, inlineQuotations);
        var builder = new StringBuilder(text.Length + (ranges.Count * 24) + (emphasize ? 10 : 0));
        if (emphasize)
        {
            builder.Append("[bold]");
        }

        var offset = 0;
        foreach (var range in ranges)
        {
            builder.Append(Escape(text[offset..range.Start]));
            builder.Append("[bold yellow]").Append(Escape(text[range.Start..range.End])).Append("[/]");
            offset = range.End;
        }

        builder.Append(Escape(text[offset..]));
        if (emphasize)
        {
            builder.Append("[/]");
        }

        return builder.ToString();
    }

    private static IReadOnlyList<(int Start, int End)> FindInlineQuotationRanges(string text, IReadOnlyList<GroundedQuotation> quotations)
    {
        var candidates = new List<(int Start, int End)>();
        foreach (var quotationText in quotations.Select(quotation => quotation.Text).Where(value => !string.IsNullOrEmpty(value)).Distinct(StringComparer.Ordinal))
        {
            var searchOffset = 0;
            while (searchOffset < text.Length)
            {
                var matchOffset = text.IndexOf(quotationText, searchOffset, StringComparison.Ordinal);
                if (matchOffset < 0)
                {
                    break;
                }

                var start = matchOffset;
                var end = matchOffset + quotationText.Length;
                if (start > 0 && end < text.Length && IsQuotationMarkPair(text[start - 1], text[end]))
                {
                    start--;
                    end++;
                }

                candidates.Add((start, end));
                searchOffset = matchOffset + quotationText.Length;
            }
        }

        var merged = new List<(int Start, int End)>();
        foreach (var candidate in candidates.OrderBy(candidate => candidate.Start).ThenByDescending(candidate => candidate.End))
        {
            if (merged.Count == 0 || candidate.Start > merged[^1].End)
            {
                merged.Add(candidate);
                continue;
            }

            if (candidate.End > merged[^1].End)
            {
                merged[^1] = (merged[^1].Start, candidate.End);
            }
        }

        return merged;
    }

    private static bool IsQuotationMarkPair(char opening, char closing) => (opening == '"' && closing == '"') || (opening == '“' && closing == '”');

    private static string GetQuotationKey(GroundedQuotation quotation) => $"{quotation.Source.EvidenceId}\u001f{quotation.Text}";

    private static void PrintQuotation(GroundedQuotation quotation)
    {
        AnsiConsole.MarkupLine($"[bold yellow]“{Escape(quotation.Text)}”[/]");
        AnsiConsole.MarkupLine($"[cyan]— [bold]{Escape(quotation.Source.CanonicalReference)}[/] [[{quotation.Source.Number}]][/]");
        AnsiConsole.WriteLine();
    }

    private static string FormatInlineCitations(IReadOnlyList<SourceCitation> citations) => string.Join(' ', citations.Select(FormatInlineCitation));

    private static string FormatInlineCitation(SourceCitation citation)
    {
        var license = citation.RequiresAttribution ? $" — {citation.License}" : string.Empty;
        if (System.Console.IsOutputRedirected)
        {
            return Escape($"[{citation.Number}] {citation.MarkdownSourceLink}{license}");
        }

        var sourceLabel = $"{citation.CanonicalReference} — {citation.Edition}";
        var attribution = citation.RequiresAttribution ? $" [grey]({Escape(citation.License)})[/]" : string.Empty;
        return $"[bold cyan][[{citation.Number}]][/] [link={EscapeLinkTarget(citation.SourceUrl)}]{Escape(sourceLabel)}[/]{attribution}";
    }

    private static void PrintInterpretiveNotice(string notice)
    {
        var normalized = notice.Replace("\r\n", "\n", StringComparison.Ordinal).Trim();
        AnsiConsole.MarkupLine($"[italic grey]{Escape(normalized)}[/]");
    }

    private static void AddFacetRows(Table table, string facetName, IReadOnlyDictionary<string, int> values)
    {
        foreach (var pair in values.OrderByDescending(pair => pair.Value).ThenBy(pair => pair.Key, StringComparer.OrdinalIgnoreCase))
        {
            table.AddRow(Escape(facetName), Escape(pair.Key), pair.Value.ToString("N0", CultureInfo.CurrentCulture));
        }
    }

    private static string EscapeLinkTarget(string value) => Markup.Escape(new Uri(value, UriKind.Absolute).AbsoluteUri);
}
