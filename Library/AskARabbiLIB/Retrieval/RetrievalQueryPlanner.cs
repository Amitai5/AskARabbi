using AskARabbiLIB.Search;

namespace AskARabbiLIB.Retrieval;

internal static class RetrievalQueryPlanner
{
    private const int MaximumConcepts = 8;

    private static readonly HashSet<string> StopWords = new(StringComparer.Ordinal)
    {
        "a", "about", "after", "allow", "allowed", "am", "an", "and", "are", "avoid", "avoiding", "be", "because", "before", "but", "by", "can", "cannot", "cant", "communities", "community", "could", "do", "does", "doesnt", "don", "dont", "for", "forbid", "forbidden", "from", "had", "has", "have", "how", "i", "if", "in", "is", "it", "its", "jewish", "jews", "judaism", "just", "many", "may", "me", "my", "not", "now", "of", "ok", "okay", "on", "or", "our", "people", "please", "prohibit", "prohibited", "reason", "rule", "rules", "say", "should", "so", "some", "text", "that", "the", "their", "then", "there", "these", "they", "this", "to", "today", "us", "want", "was", "we", "were", "what", "when", "where", "which", "who", "why", "will", "with", "would", "you", "your",
        "איך", "איפה", "או", "את", "האם", "הוא", "היא", "הם", "זה", "זאת", "למה", "מה", "מי", "מתי", "על", "עם", "של",
    };

    private static readonly RetrievalConceptDefinition[] Definitions =
    [
        new("shabbat", ["shabbat", "shabbos", "sabbath", "saturday"], 1_000, true),
        new("automation", ["automatic", "automatically", "automated", "automation", "clock", "clocks", "continue", "continued", "continues", "continuing", "flow", "flowing", "flows", "operate", "operated", "operates", "operating", "preprogrammed", "programmed", "run", "running", "runs", "start", "started", "starting", "starts", "timer", "timers"], 900, false),
        new("business", ["business", "businesses", "commerce", "commercial", "customer", "customers", "labor", "order", "orders", "payment", "payments", "profit", "profits", "revenue", "sale", "sales", "selling", "shop", "store", "work"], 850, false),
        new("technology", ["computer", "computers", "device", "devices", "machine", "machines", "online", "server", "servers", "software", "website", "websites"], 800, false),
        new("lamp", ["fire", "flame", "flames", "kindle", "kindled", "kindling", "lamp", "lamps", "light", "lighting", "lights"], 700, false),
        new("poultry", ["bird", "birds", "chicken", "chickens", "fowl", "poultry"], 600, false),
        new("dairy", ["cheese", "dairy", "milk"], 590, false),
        new("meat", ["flesh", "meat"], 580, false),
        new("combining", ["combine", "combined", "combines", "combining", "cook", "cooked", "cooking", "cooks", "mix", "mixed", "mixes", "mixing", "together"], 570, false),
        new("eating", ["consume", "consumed", "consumes", "consuming", "eat", "eaten", "eating", "eats"], 560, false),
        new("scripture", ["biblical", "scripture", "scriptural", "torah"], 500, false),
        new("rabbinic", ["rabbi", "rabbinic", "rabbinical", "rabbis"], 490, false),
        new("custom", ["custom", "customary", "customs", "practice", "practices"], 480, false),
    ];

    private static readonly IReadOnlyDictionary<string, RetrievalConceptDefinition> DefinitionByToken = CreateDefinitionLookup();

    internal static RetrievalQueryPlan Plan(string? queryText)
    {
        var tokens = SearchTextNormalizer.Tokenize(queryText);
        var tokenSet = tokens.ToHashSet(StringComparer.Ordinal);
        var concepts = Definitions
            .Where(definition => definition.Tokens.Any(tokenSet.Contains))
            .OrderByDescending(definition => definition.Priority)
            .Select(definition => new RetrievalConcept(definition.Key, definition.Tokens, definition.IsTopicAnchor))
            .ToList();

        var knownKeys = concepts.Select(concept => concept.Key).ToHashSet(StringComparer.Ordinal);
        foreach (var token in tokens)
        {
            if (concepts.Count == MaximumConcepts)
            {
                break;
            }
            if (token.Length < 2 || StopWords.Contains(token) || DefinitionByToken.TryGetValue(token, out var definition) && knownKeys.Contains(definition.Key))
            {
                continue;
            }

            concepts.Add(new RetrievalConcept(token, [token], false));
            knownKeys.Add(token);
        }

        return new RetrievalQueryPlan(concepts, concepts.FirstOrDefault(concept => concept.IsTopicAnchor));
    }

    internal static bool Matches(RetrievalConcept concept, IReadOnlySet<string> searchableTokens) => concept.Tokens.Any(searchableTokens.Contains);

    internal static IReadOnlySet<string> CreateSearchableTokens(SourceSegment segment)
    {
        ArgumentNullException.ThrowIfNull(segment);
        var searchableText = string.Join('\n', new[] { segment.Text, segment.Title, segment.HebrewTitle, segment.CanonicalReference, segment.Collection }.Concat(segment.Categories));
        return SearchTextNormalizer.Tokenize(searchableText).ToHashSet(StringComparer.Ordinal);
    }

    private static IReadOnlyDictionary<string, RetrievalConceptDefinition> CreateDefinitionLookup()
    {
        var lookup = new Dictionary<string, RetrievalConceptDefinition>(StringComparer.Ordinal);
        foreach (var definition in Definitions)
        {
            foreach (var token in definition.Tokens)
            {
                lookup.Add(token, definition);
            }
        }
        return lookup;
    }
}
