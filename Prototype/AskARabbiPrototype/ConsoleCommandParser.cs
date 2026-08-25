using System.Globalization;
using AskARabbiLIB.Grounding;
using AskARabbiLIB.Models;

namespace AskARabbiPrototype;

internal sealed class ConsoleCommandParser
{
    internal ConsoleCommandParseResult Parse(IReadOnlyList<string> arguments)
    {
        ArgumentNullException.ThrowIfNull(arguments);
        var remaining = arguments.ToList();
        var globalOptions = ExtractGlobalOptions(remaining);
        if (globalOptions.Error is not null)
        {
            return Failure(globalOptions.Error);
        }

        if (remaining.Count == 0)
        {
            return Success(CreateCommand(ConsoleCommandKind.Interactive, globalOptions));
        }

        var commandName = remaining[0].ToLowerInvariant();
        remaining.RemoveAt(0);
        return commandName switch
        {
            "search" => ParseSearch(remaining, globalOptions),
            "facets" => ParseSimpleOutputCommand(ConsoleCommandKind.Facets, remaining, globalOptions),
            "stats" => ParseSimpleOutputCommand(ConsoleCommandKind.Stats, remaining, globalOptions),
            "index" => ParseIndex(remaining, globalOptions),
            "ask" => ParseAsk(remaining, globalOptions),
            "help" or "--help" or "-h" => ParseArgumentlessCommand(ConsoleCommandKind.Help, remaining, globalOptions),
            _ => Failure($"Unknown command '{commandName}'. Use 'help' for available commands."),
        };
    }

    private static ConsoleCommandParseResult ParseSearch(IReadOnlyList<string> arguments, GlobalOptions globalOptions)
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
                return Failure(error);
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
                    if (!TryParseDefinedEnum(value, out matchMode))
                    {
                        return Failure("--match must be 'all' or 'any'.");
                    }

                    break;
                case "--min-segments":
                    if (!TryParseNonnegativeInteger(value, "--min-segments", out var parsedMinimum, out error))
                    {
                        return Failure(error);
                    }

                    minimumSegmentCount = parsedMinimum;
                    break;
                case "--max-segments":
                    if (!TryParseNonnegativeInteger(value, "--max-segments", out var parsedMaximum, out error))
                    {
                        return Failure(error);
                    }

                    maximumSegmentCount = parsedMaximum;
                    break;
                case "--skip":
                    if (!TryParseNonnegativeInteger(value, "--skip", out skip, out error))
                    {
                        return Failure(error);
                    }

                    break;
                case "--limit":
                    if (!int.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out limit) || limit is < 1 or > 200)
                    {
                        return Failure("--limit must be an integer between 1 and 200.");
                    }

                    break;
                case "--format":
                    if (!TryParseDefinedEnum(value, out outputFormat))
                    {
                        return Failure("--format must be 'table' or 'json'.");
                    }

                    break;
                default:
                    return Failure($"Unknown search option '{argument}'.");
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

        return Success(CreateCommand(ConsoleCommandKind.Search, globalOptions, outputFormat, query));
    }

    private static ConsoleCommandParseResult ParseAsk(IReadOnlyList<string> arguments, GlobalOptions globalOptions)
    {
        var questionParts = new List<string>();
        var languages = new List<string>();
        var collections = new List<string>();
        var categories = new List<string>();
        var workKeys = new List<string>();
        var sourceKeys = new List<string>();
        var outputFormat = ConsoleOutputFormat.Table;
        string? profileFileName = null;

        for (var index = 0; index < arguments.Count; index++)
        {
            var argument = arguments[index];
            if (!argument.StartsWith("--", StringComparison.Ordinal))
            {
                questionParts.Add(argument);
                continue;
            }

            if (!TryReadOptionValue(arguments, ref index, out var value, out var error))
            {
                return Failure(error);
            }

            switch (argument.ToLowerInvariant())
            {
                case "--language":
                    languages.Add(value);
                    break;
                case "--collection":
                    collections.Add(value);
                    break;
                case "--category":
                    categories.Add(value);
                    break;
                case "--work":
                    workKeys.Add(value);
                    break;
                case "--source":
                    sourceKeys.Add(value);
                    break;
                case "--format":
                    if (!TryParseDefinedEnum(value, out outputFormat))
                    {
                        return Failure("--format must be 'table' or 'json'.");
                    }

                    break;
                case "--profile":
                    profileFileName = value;
                    break;
                default:
                    return Failure($"Unknown ask option '{argument}'.");
            }
        }

        var questionText = string.Join(' ', questionParts).Trim();
        if (questionText.Length == 0)
        {
            return Failure("ask requires a nonempty question.");
        }

        var question = new GroundedQuestion
        {
            Question = questionText,
            Languages = languages,
            Collections = collections,
            Categories = categories,
            WorkKeys = workKeys,
            SourceKeys = sourceKeys,
        };

        return Success(CreateCommand(ConsoleCommandKind.Ask, globalOptions, outputFormat, groundedQuestion: question, profileFileName: profileFileName));
    }

    private static ConsoleCommandParseResult ParseIndex(IReadOnlyList<string> arguments, GlobalOptions globalOptions)
    {
        if (arguments.Count == 0)
        {
            return Failure("index requires one subcommand: build, verify, or stats.");
        }

        var kind = arguments[0].ToLowerInvariant() switch
        {
            "build" => ConsoleCommandKind.IndexBuild,
            "verify" => ConsoleCommandKind.IndexVerify,
            "stats" => ConsoleCommandKind.IndexStats,
            _ => (ConsoleCommandKind?)null,
        };

        return kind is null
            ? Failure($"Unknown index subcommand '{arguments[0]}'.")
            : ParseSimpleOutputCommand(kind.Value, arguments.Skip(1).ToArray(), globalOptions);
    }

    private static ConsoleCommandParseResult ParseSimpleOutputCommand(ConsoleCommandKind kind, IReadOnlyList<string> arguments, GlobalOptions globalOptions)
    {
        var outputFormat = ConsoleOutputFormat.Table;
        if (arguments.Count == 2 && string.Equals(arguments[0], "--format", StringComparison.OrdinalIgnoreCase))
        {
            if (!TryParseDefinedEnum(arguments[1], out outputFormat))
            {
                return Failure("--format must be 'table' or 'json'.");
            }
        }
        else if (arguments.Count != 0)
        {
            return Failure($"{kind.ToString().ToLowerInvariant()} accepts only '--format table|json'.");
        }

        return Success(CreateCommand(kind, globalOptions, outputFormat));
    }

    private static ConsoleCommandParseResult ParseArgumentlessCommand(ConsoleCommandKind kind, IReadOnlyList<string> arguments, GlobalOptions globalOptions)
    {
        return arguments.Count == 0
            ? Success(CreateCommand(kind, globalOptions))
            : Failure($"{kind.ToString().ToLowerInvariant()} does not accept arguments.");
    }

    private static GlobalOptions ExtractGlobalOptions(List<string> arguments)
    {
        string? manifestPath = null;
        string? repositoryRoot = null;
        string? indexPath = null;

        for (var index = 0; index < arguments.Count;)
        {
            var argument = arguments[index];
            var recognized = string.Equals(argument, "--manifest", StringComparison.OrdinalIgnoreCase)
                || string.Equals(argument, "--repository-root", StringComparison.OrdinalIgnoreCase)
                || string.Equals(argument, "--index", StringComparison.OrdinalIgnoreCase);
            if (!recognized)
            {
                index++;
                continue;
            }

            if (index + 1 >= arguments.Count || arguments[index + 1].StartsWith("--", StringComparison.Ordinal))
            {
                return new GlobalOptions(null, null, null, $"{argument} requires a path value.");
            }

            var value = arguments[index + 1];
            if (string.Equals(argument, "--manifest", StringComparison.OrdinalIgnoreCase))
            {
                if (manifestPath is not null)
                {
                    return new GlobalOptions(null, null, null, "--manifest can be specified only once.");
                }

                manifestPath = value;
            }
            else if (string.Equals(argument, "--repository-root", StringComparison.OrdinalIgnoreCase))
            {
                if (repositoryRoot is not null)
                {
                    return new GlobalOptions(null, null, null, "--repository-root can be specified only once.");
                }

                repositoryRoot = value;
            }
            else
            {
                if (indexPath is not null)
                {
                    return new GlobalOptions(null, null, null, "--index can be specified only once.");
                }

                indexPath = value;
            }

            arguments.RemoveRange(index, 2);
        }

        return new GlobalOptions(manifestPath, repositoryRoot, indexPath, null);
    }

    private static bool TryReadOptionValue(IReadOnlyList<string> arguments, ref int index, out string value, out string error)
    {
        if (index + 1 >= arguments.Count || arguments[index + 1].StartsWith("--", StringComparison.Ordinal))
        {
            value = string.Empty;
            error = $"{arguments[index]} requires a value.";
            return false;
        }

        value = arguments[++index];
        error = string.Empty;
        return true;
    }

    private static bool TryParseNonnegativeInteger(string value, string optionName, out int result, out string error)
    {
        if (!int.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out result) || result < 0)
        {
            error = $"{optionName} must be a nonnegative integer.";
            return false;
        }

        error = string.Empty;
        return true;
    }

    private static bool TryParseDefinedEnum<TEnum>(string value, out TEnum result) where TEnum : struct, Enum
    {
        return Enum.TryParse(value, true, out result) && Enum.IsDefined(result);
    }

    private static ConsoleCommand CreateCommand(ConsoleCommandKind kind, GlobalOptions options, ConsoleOutputFormat outputFormat = ConsoleOutputFormat.Table, ManifestSearchQuery? query = null, GroundedQuestion? groundedQuestion = null, string? profileFileName = null)
    {
        return new ConsoleCommand(kind, options.ManifestPath, options.RepositoryRoot, options.IndexPath, outputFormat, query, groundedQuestion, profileFileName);
    }

    private static ConsoleCommandParseResult Success(ConsoleCommand command) => new(command, null);

    private static ConsoleCommandParseResult Failure(string error) => new(null, error);

    private sealed record GlobalOptions(string? ManifestPath, string? RepositoryRoot, string? IndexPath, string? Error);
}
