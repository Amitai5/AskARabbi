using AskARabbiLIB.Grounding;
using AskARabbiLIB.Models;

namespace AskARabbiPrototype;

internal sealed record ConsoleCommand(ConsoleCommandKind Kind, string? ManifestPath = null, string? RepositoryRoot = null, string? IndexPath = null, ConsoleOutputFormat OutputFormat = ConsoleOutputFormat.Table, ManifestSearchQuery? Query = null, GroundedQuestion? GroundedQuestion = null, string? ProfileFileName = null);
