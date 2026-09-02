using System.Text.RegularExpressions;

namespace AskARabbiLIB.DvarTorah;

internal static class WeeklyDvarTorahCandidateValidator
{
    private const string DirectUrlPattern = @"\b(?:https?://|www\.)\S+";
    private const string SensitivePersonalDataPattern = @"(?:\b[A-Z0-9._%+-]+@[A-Z0-9.-]+\.[A-Z]{2,}\b|(?<!\d)(?:\+?1[\s.-]?)?(?:\(\d{3}\)|\d{3})[\s.-]\d{3}[\s.-]\d{4}(?!\d)|\b(?:\d{1,3}\.){3}\d{1,3}\b)";

    internal static WeeklyDvarTorahCandidateValidation Validate(WeeklyDvarTorahArticleDraft draft, IReadOnlyList<WeeklyDvarTorahEvidence> evidence, WeeklyDvarTorahContentOptions options)
    {
        ArgumentNullException.ThrowIfNull(draft);
        ArgumentNullException.ThrowIfNull(evidence);
        ArgumentNullException.ThrowIfNull(options);
        var errors = new List<string>();
        var evidenceById = evidence.ToDictionary(item => item.EvidenceId, StringComparer.Ordinal);
        ValidateText(draft.Title, 1, WeeklyDvarTorahDraft.MaximumTitleCharacters, "The title", errors);
        ValidateText(draft.Body, options.MinimumBodyCharacters, options.MaximumBodyCharacters, "The body", errors);
        ValidateText(draft.CentralTeaching, 40, 1_200, "The central teaching", errors);
        ValidateTags(draft.Tags, errors);
        ValidatePracticalActions(draft.PracticalActions, errors);
        ValidateNoSensitivePersonalData(draft, errors);

        var torahStatements = draft.TorahTeachings ?? [];
        var newsStatements = draft.CurrentEventFacts ?? [];
        var connections = draft.Connections ?? [];
        if (torahStatements.Count is < 4 or > 12)
        {
            errors.Add("The draft must contain between four and twelve distinct Torah teachings.");
        }
        if (newsStatements.Count is < 1 or > 3)
        {
            errors.Add("The draft must contain between one and three bounded current-event facts.");
        }
        if (connections.Count is < 1 or > 3)
        {
            errors.Add("The draft must contain between one and three Torah-to-current-events connections.");
        }

        var usedIds = new List<string>();
        foreach (var statement in torahStatements)
        {
            ValidateStatement(statement, evidenceById, kind => kind == WeeklyDvarTorahSourceKind.Torah, "Torah teaching", usedIds, errors);
        }
        foreach (var statement in newsStatements)
        {
            ValidateStatement(statement, evidenceById, kind => kind == WeeklyDvarTorahSourceKind.News, "current-event fact", usedIds, errors);
        }
        foreach (var statement in connections)
        {
            ValidateStatement(statement, evidenceById, _ => true, "connection", usedIds, errors);
            var kinds = statement?.EvidenceIds?.Where(evidenceById.ContainsKey).Select(id => evidenceById[id].Kind).ToHashSet() ?? [];
            if (!kinds.Contains(WeeklyDvarTorahSourceKind.Torah) || !kinds.Contains(WeeklyDvarTorahSourceKind.News))
            {
                errors.Add("Every connection must cite at least one Torah passage and one current-events source.");
            }
        }

        var uniqueIds = usedIds.Distinct(StringComparer.Ordinal).ToArray();
        var torahSourceCount = uniqueIds.Count(id => evidenceById.TryGetValue(id, out var item) && item.Kind == WeeklyDvarTorahSourceKind.Torah);
        var newsSourceCount = uniqueIds.Count(id => evidenceById.TryGetValue(id, out var item) && item.Kind == WeeklyDvarTorahSourceKind.News);
        var otherSourceCount = uniqueIds.Count(id => evidenceById.TryGetValue(id, out var item) && item.Kind == WeeklyDvarTorahSourceKind.Other);
        if (torahSourceCount < options.MinimumTorahEvidenceItems)
        {
            errors.Add($"The draft must cite at least {options.MinimumTorahEvidenceItems} distinct passages from the weekly Torah reading.");
        }
        var newsPublisherCount = uniqueIds
            .Where(id => evidenceById.TryGetValue(id, out var item) && item.Kind == WeeklyDvarTorahSourceKind.News)
            .Select(id => evidenceById[id].Publisher)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Count();
        if (newsPublisherCount < options.MinimumNewsPublishers)
        {
            errors.Add($"The current event must be corroborated by at least {options.MinimumNewsPublishers} independent publishers.");
        }

        var claimDenominator = torahStatements.Count + newsStatements.Count;
        var claimPercent = claimDenominator == 0 ? 0 : (int)Math.Floor(100d * torahStatements.Count / claimDenominator);
        var sourceDenominator = torahSourceCount + newsSourceCount + otherSourceCount;
        var sourcePercent = sourceDenominator == 0 ? 0 : (int)Math.Floor(100d * torahSourceCount / sourceDenominator);
        var groundingPercent = Math.Min(claimPercent, sourcePercent);
        if (groundingPercent < options.MinimumTorahGroundingPercent)
        {
            errors.Add($"Deterministic Torah grounding was {groundingPercent}%; at least {options.MinimumTorahGroundingPercent}% is required for both claims and source weight.");
        }

        ValidateBodyCitations(draft.Body, uniqueIds, evidenceById, errors);
        return new WeeklyDvarTorahCandidateValidation(errors, groundingPercent, uniqueIds);
    }

    private static void ValidateStatement(WeeklyDvarTorahSourcedStatementDraft? statement, IReadOnlyDictionary<string, WeeklyDvarTorahEvidence> evidence, Func<WeeklyDvarTorahSourceKind, bool> allowedKind, string label, ICollection<string> usedIds, ICollection<string> errors)
    {
        if (statement is null)
        {
            errors.Add($"Every {label} must be complete.");
            return;
        }
        if (string.IsNullOrWhiteSpace(statement.Text) || statement.Text.Length > 1_200)
        {
            errors.Add($"Every {label} must contain between one and 1,200 characters.");
        }

        var ids = statement.EvidenceIds ?? [];
        if (ids.Count is < 1 or > 8 || ids.Any(string.IsNullOrWhiteSpace) || ids.Distinct(StringComparer.Ordinal).Count() != ids.Count)
        {
            errors.Add($"Every {label} must cite between one and eight unique evidence IDs.");
            return;
        }
        foreach (var id in ids)
        {
            if (!evidence.TryGetValue(id, out var item))
            {
                errors.Add($"The {label} cites unknown evidence ID '{id}'.");
                continue;
            }
            if (!allowedKind(item.Kind))
            {
                errors.Add($"The {label} cites disallowed {item.Kind} evidence '{id}'.");
            }
            usedIds.Add(id);
        }

    }

    private static void ValidateBodyCitations(string? body, IReadOnlyList<string> usedIds, IReadOnlyDictionary<string, WeeklyDvarTorahEvidence> evidence, ICollection<string> errors)
    {
        if (string.IsNullOrWhiteSpace(body))
        {
            return;
        }
        foreach (var id in usedIds)
        {
            if (!body.Contains($"[{id}]", StringComparison.Ordinal))
            {
                errors.Add($"The article body does not contain an inline marker for cited evidence '{id}'.");
            }
        }

        foreach (Match match in Regex.Matches(body, @"\[(?<id>[TNO](?:\d{1,2}|[A-Z]{1,2}))\]", RegexOptions.CultureInvariant, TimeSpan.FromSeconds(1)))
        {
            var id = match.Groups["id"].Value;
            if (!evidence.ContainsKey(id))
            {
                errors.Add($"The article body contains unknown evidence marker '{id}'.");
            }
        }
    }

    private static void ValidateTags(IReadOnlyList<string>? tags, ICollection<string> errors)
    {
        if (tags is null || tags.Count is < 5 or > 12 || tags.Any(string.IsNullOrWhiteSpace))
        {
            errors.Add("The draft must contain between five and twelve non-blank search tags.");
            return;
        }
        var normalized = tags.Select(tag => tag.Trim().ToLowerInvariant()).ToArray();
        if (normalized.Any(tag => tag.Length > 60) || normalized.Distinct(StringComparer.Ordinal).Count() != normalized.Length)
        {
            errors.Add("Draft tags must be unique after normalization and contain at most sixty characters.");
        }
    }

    private static void ValidatePracticalActions(IReadOnlyList<string>? actions, ICollection<string> errors)
    {
        if (actions is null || actions.Count != 3 || actions.Any(action => string.IsNullOrWhiteSpace(action) || action.Length > 500))
        {
            errors.Add("The draft must provide exactly three concrete, non-blank practical actions of at most five hundred characters each.");
        }
    }

    private static void ValidateNoSensitivePersonalData(WeeklyDvarTorahArticleDraft draft, ICollection<string> errors)
    {
        var values = new List<string?> { draft.Title, draft.Body, draft.CentralTeaching };
        values.AddRange(draft.Tags ?? []);
        values.AddRange(draft.PracticalActions ?? []);
        foreach (var statement in (draft.TorahTeachings ?? []).Concat(draft.CurrentEventFacts ?? []).Concat(draft.Connections ?? []))
        {
            values.Add(statement?.Text);
        }

        if (values.Any(value => !string.IsNullOrWhiteSpace(value) && (Regex.IsMatch(value, SensitivePersonalDataPattern, RegexOptions.IgnoreCase | RegexOptions.CultureInvariant, TimeSpan.FromSeconds(1)) || Regex.IsMatch(value, DirectUrlPattern, RegexOptions.IgnoreCase | RegexOptions.CultureInvariant, TimeSpan.FromSeconds(1)))))
        {
            errors.Add("The draft must not contain contact details, IP addresses, or direct URLs.");
        }
    }

    private static void ValidateText(string? value, int minimumCharacters, int maximumCharacters, string label, ICollection<string> errors)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Trim().Length < minimumCharacters || value.Trim().Length > maximumCharacters)
        {
            errors.Add($"{label} must contain between {minimumCharacters:N0} and {maximumCharacters:N0} characters.");
        }
    }
}
