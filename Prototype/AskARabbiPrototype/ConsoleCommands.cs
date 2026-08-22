using System.Globalization;
using AskARabbiLIB.Models;

namespace AskARabbiPrototype;

internal enum ConsoleCommandKind
{
    Interactive,
    Search,
    Facets,
    Stats,
    Help,
}

internal enum ConsoleOutputFormat
{
    Table,
    Json,
}

internal sealed record ConsoleCommand(ConsoleCommandKind Kind, string? ManifestPath = null, string? RepositoryRoot = null, ConsoleOutputFormat OutputFormat = ConsoleOutputFormat.Table, ManifestSearchQuery? Query = null);

internal sealed record ConsoleCommandParseResult(ConsoleCommand? Command, string? Error)
{
    internal bool IsSuccess => Command is not null && Error is null;
}

internal sealed class ConsoleCommandParser
{
    internal ConsoleCommandParseResult Parse(IReadOnlyList<string> arguments)
    {
        ArgumentNullException.ThrowIfNull(arguments);
        var remaining = arguments.ToList();
        var globalResult = ExtractGlobalOptions(remaining);
        if (globalResult.Error is not null)
        {
            return new ConsoleCommandParseResult(null, globalResult.Error);
        }
        if (remaining.Count == 0)
        {
            return Success(new ConsoleCommand(ConsoleCommandKind.Interactive, globalResult.ManifestPath, globalResult.RepositoryRoot));
        }

        var commandName = remaining[0].ToLowerInvariant();
        var commandArguments = remaining.Skip(1).ToArray();
        return commandName switch
        {
            "search" => ParseSearch(commandArguments, globalResult.ManifestPath, globalResult.RepositoryRoot),
            "facets" => ParseSimpleOutputCommand(ConsoleCommandKind.Facets, commandArguments, globalResult.ManifestPath, globalResult.RepositoryRoot),
            "stats" => ParseSimpleOutputCommand(ConsoleCommandKind.Stats, commandArguments, globalResult.ManifestPath, globalResult.RepositoryRoot),
            "help" or "--help" or "-h" => ParseArgumentlessCommand(ConsoleCommandKind.Help, commandArguments, globalResult.ManifestPath, globalResult.RepositoryRoot),
            _ => new ConsoleCommandParseResult(null, $"Unknown command '{remaining[0]}'. Use 'help' for available commands."),
        };
    }

    private static ConsoleCommandParseResult ParseSearch(IReadOnlyList<string> arguments, string? manifestPath, string? repositoryRoot)
    {
        var keywords = new List<string>();
        var languages = new List<string>();
        var collections = new List<string>();
        var categories = new List<string>();
        var titles = new List<string>();
        var versionTitles = new List<string>();
        var licenses = new List<string>();
        var matchMode = KeywordMatchMode.All;
        var outputFormat = ConsoleOutputFormat.Table;
        int? minimumSegmentCount = null;
        int? maximumSegmentCount = null;
        var skip = 0;
        var limit = 25;

        for (var index = 0; index < arguments.Count; index++)
        {
            var argument = arguments[index];
            if (!argument.StartsWith("--", StringComparison.Ordinal))
            {
                keywords.Add(argument);
                continue;
            }

            if (!TryReadOptionValue(arguments, ref index, out var value, out var error))
            {
                return new ConsoleCommandParseResult(null, error);
            }

            switch (argument.ToLowerInvariant())
            {
                case "--keywords":
                    keywords.Add(value);
                    break;
                case "--language":
                    languages.Add(value);
                    break;
                case "--collection":
                    collections.Add(value);
                    break;
                case "--category":
                    categories.Add(value);
                    break;
                case "--title":
                    titles.Add(value);
                    break;
                case "--version":
                    versionTitles.Add(value);
                    break;
                case "--license":
                    licenses.Add(value);
                    break;
                case "--match":
                    if (!Enum.TryParse(value, true, out matchMode))
                    {
                        return new ConsoleCommandParseResult(null, "--match must be 'all' or 'any'.");
                    }
                    break;
                case "--min-segments":
                    if (!TryParseNonnegativeInteger(value, "--min-segments", out minimumSegmentCount, out error))
                    {
                        return new ConsoleCommandParseResult(null, error);
                    }
                    break;
                case "--max-segments":
                    if (!TryParseNonnegativeInteger(value, "--max-segments", out maximumSegmentCount, out error))
                    {
                        return new ConsoleCommandParseResult(null, error);
                    }
                    break;
                case "--skip":
                    if (!TryParseNonnegativeInteger(value, "--skip", out var parsedSkip, out error))
                    {
                        return new ConsoleCommandParseResult(null, error);
                    }
                    skip = parsedSkip!.Value;
                    break;
                case "--limit":
                    if (!int.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out limit) || limit < 1 || limit > 200)
                    {
                        return new ConsoleCommandParseResult(null, "--limit must be an integer between 1 and 200.");
                    }
                    break;
                case "--format":
                    if (!Enum.TryParse(value, true, out outputFormat))
                    {
                        return new ConsoleCommandParseResult(null, "--format must be 'table' or 'json'.");
                    }
                    break;
                default:
                    return new ConsoleCommandParseResult(null, $"Unknown search option '{argument}'.");
            }
        }

        var query = new ManifestSearchQuery
        {
            Keywords = keywords.Count == 0 ? null : string.Join(' ', keywords),
            KeywordMatchMode = matchMode,
            Languages = languages,
            Collections = collections,
            Categories = categories,
            Titles = titles,
            VersionTitles = versionTitles,
            Licenses = licenses,
            MinimumSegmentCount = minimumSegmentCount,
            MaximumSegmentCount = maximumSegmentCount,
            Skip = skip,
            Limit = limit,
        };
        return Success(new ConsoleCommand(ConsoleCommandKind.Search, manifestPath, repositoryRoot, outputFormat, query));
    }

    private static ConsoleCommandParseResult ParseSimpleOutputCommand(ConsoleCommandKind kind, IReadOnlyList<string> arguments, string? manifestPath, string? repositoryRoot)
    {
        var outputFormat = ConsoleOutputFormat.Table;
        if (arguments.Count == 2 && string.Equals(arguments[0], "--format", StringComparison.OrdinalIgnoreCase) && Enum.TryParse(arguments[1], true, out ConsoleOutputFormat parsedFormat))
        {
            outputFormat = parsedFormat;
        }
        else if (arguments.Count != 0)
        {
            return new ConsoleCommandParseResult(null, $"{kind.ToString().ToLowerInvariant()} accepts only '--format table|json'.");
        }
        return Success(new ConsoleCommand(kind, manifestPath, repositoryRoot, outputFormat));
    }

    private static ConsoleCommandParseResult ParseArgumentlessCommand(ConsoleCommandKind kind, IReadOnlyList<string> arguments, string? manifestPath, string? repositoryRoot)
    {
        return arguments.Count == 0
            ? Success(new ConsoleCommand(kind, manifestPath, repositoryRoot))
            : new ConsoleCommandParseResult(null, $"{kind.ToString().ToLowerInvariant()} does not accept arguments.");
    }

    private static (string? ManifestPath, string? RepositoryRoot, string? Error) ExtractGlobalOptions(List<string> arguments)
    {
        string? manifestPath = null;
        string? repositoryRoot = null;
        for (var index = 0; index < arguments.Count;)
        {
            var argument = arguments[index];
            if (!string.Equals(argument, "--manifest", StringComparison.OrdinalIgnoreCase) && !string.Equals(argument, "--repository-root", StringComparison.OrdinalIgnoreCase))
            {
                index++;
                continue;
            }
            if (index + 1 >= arguments.Count || arguments[index + 1].StartsWith("--", StringComparison.Ordinal))
            {
                return (null, null, $"{argument} requires a path value.");
            }

            var value = arguments[index + 1];
            if (string.Equals(argument, "--manifest", StringComparison.OrdinalIgnoreCase))
            {
                if (manifestPath is not null)
                {
                    return (null, null, "--manifest can be specified only once.");
                }
                manifestPath = value;
            }
            else
            {
                if (repositoryRoot is not null)
                {
                    return (null, null, "--repository-root can be specified only once.");
                }
                repositoryRoot = value;
            }
            arguments.RemoveRange(index, 2);
        }
        return (manifestPath, repositoryRoot, null);
    }

    private static bool TryReadOptionValue(IReadOnlyList<string> arguments, ref int index, out string value, out string? error)
    {
        if (index + 1 >= arguments.Count || arguments[index + 1].StartsWith("--", StringComparison.Ordinal))
        {
            value = string.Empty;
            error = $"{arguments[index]} requires a value.";
            return false;
        }
        value = arguments[++index];
        error = null;
        return true;
    }

    private static bool TryParseNonnegativeInteger(string value, string optionName, out int? result, out string? error)
    {
        if (!int.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out var parsedValue) || parsedValue < 0)
        {
            result = null;
            error = $"{optionName} must be a nonnegative integer.";
            return false;
        }
        result = parsedValue;
        error = null;
        return true;
    }

    private static ConsoleCommandParseResult Success(ConsoleCommand command) => new(command, null);
}
