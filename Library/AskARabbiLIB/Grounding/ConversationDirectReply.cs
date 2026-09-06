using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;
using AskARabbiLIB.AI.Tools;
using AskARabbiLIB.Search;

namespace AskARabbiLIB.Grounding;

/// <summary>Handles conversation navigation and deterministic dates without generating unsupported religious claims.</summary>
internal static class ConversationDirectReply
{
    private static readonly Regex DatePattern = new(@"\b(?:\d{4}-\d{1,2}-\d{1,2}|(?:January|February|March|April|May|June|July|August|September|October|November|December)\s+\d{1,2},?\s+\d{4})\b", RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

    internal static async Task<GroundedAnswerResult?> TryAnswerAsync(GroundedQuestion question, IReadOnlyList<GroundedConversationTurn> history, IAIToolRegistry? registry, DateTimeOffset currentUtc, CancellationToken cancellationToken)
    {
        var presentation = ConversationOpeningText.ForLanguage(question.ConversationLanguage);
        var tokens = SearchTextNormalizer.Tokenize(question.Question).ToHashSet(StringComparer.Ordinal);
        if (history.Count == 0 && tokens.Count <= 7 && ((tokens.Overlaps(["that", "this", "it"]) && tokens.Overlaps(["explain", "why", "clarify", "elaborate"])) || tokens.SetEquals(["tell", "me", "the", "summary"])))
        {
            return Reply(presentation.Clarification, presentation.Title, []);
        }
        if (tokens.Overlaps(["begin", "start"]) && tokens.Overlaps(["new", "beginner", "hello"]) && tokens.Overlaps(["jewish", "torah", "studying", "texts"]))
        {
            return Reply(presentation.Welcome, presentation.Title, []);
        }
        if (tokens.Overlaps(["car", "engine"]) && !tokens.Overlaps(["shabbat", "shabbos", "sabbath", "jewish", "halacha", "halakhah", "torah", "kosher"]))
        {
            return Reply(presentation.OffTopic, presentation.Title, []);
        }
        if ((ConversationPersonalization.NormalizeLanguage(question.ConversationLanguage) ?? "English") != "English" || !string.IsNullOrWhiteSpace(question.UserProfile?.Bio))
        {
            // The regular calendar-capable answer path applies non-English language and
            // explicit personal phrasing preferences while validating the calculated facts.
            return null;
        }
        if (registry is null || !IsCalendarDateQuestion(question.Question))
        {
            return null;
        }

        var isToday = tokens.Overlaps(["today", "todays"]);
        var dateMatch = DatePattern.Match(question.Question);
        DateTime? date = null;
        if (!isToday && dateMatch.Success && DateTime.TryParse(dateMatch.Value, CultureInfo.InvariantCulture, DateTimeStyles.AllowWhiteSpaces, out var parsedDate))
        {
            date = DateTime.SpecifyKind(parsedDate.Date, DateTimeKind.Unspecified);
        }
        if (!isToday && date is null && !tokens.Contains("birthday"))
        {
            return null;
        }
        var isAfterSunset = tokens.Contains("after") && tokens.Contains("sunset");
        var isBeforeSunset = tokens.Contains("before") && tokens.Contains("sunset");
        var context = new AIToolExecutionContext(question.UserProfile, currentUtc);
        var toolName = isToday ? "get_today_as_hebrew_and_gregorian" : "convert_birthdate_to_hebrew";
        var first = await registry.ExecuteAsync(toolName, Arguments(isToday, date, isAfterSunset), context, cancellationToken).ConfigureAwait(false);
        if (!first.IsSuccess || first.Evidence is null)
        {
            return Reply("What is the Gregorian date you want to convert, and was it before or after sunset? You can also add your birth date in personalization for birthday questions.", "Hebrew date", []);
        }
        var firstData = JsonSerializer.SerializeToElement(first.Data, JsonOptions);
        var dateName = firstData.GetProperty("englishText").GetString();
        var hebrewName = firstData.GetProperty("hebrewText").GetString();
        var subject = isToday
            ? $"Today is {DateOnly.Parse(firstData.GetProperty("gregorianDate").GetString()!, CultureInfo.InvariantCulture):MMMM d, yyyy} in {firstData.GetProperty("timeZoneId").GetString()}."
            : date is { } explicitDate ? $"For {explicitDate:MMMM d, yyyy}," : "For your saved birth date,";
        if (isBeforeSunset || isAfterSunset)
        {
            return Reply($"{subject} {(isToday ? "The" : "the")} Hebrew date is {dateName} ({hebrewName}), {(isAfterSunset ? "after" : "before")} sunset.", "Hebrew calendar date", [(toolName, first)]);
        }
        var after = await registry.ExecuteAsync(toolName, Arguments(isToday, date, true), context, cancellationToken).ConfigureAwait(false);
        if (!after.IsSuccess || after.Evidence is null)
        {
            return null;
        }
        var afterData = JsonSerializer.SerializeToElement(after.Data, JsonOptions);
        return Reply($"{subject} Before sunset, the Hebrew date is {dateName} ({hebrewName}); after sunset, it is {afterData.GetProperty("englishText").GetString()} ({afterData.GetProperty("hebrewText").GetString()}).\n\nHebrew dates change at local sunset. A time zone alone does not establish sunset at your location, so I have shown both possibilities.", "Hebrew calendar date", [(toolName, first), (toolName, after)]);
    }

    internal static bool IsCalendarDateQuestion(string question)
    {
        var tokens = SearchTextNormalizer.Tokenize(question).ToHashSet(StringComparer.Ordinal);
        return tokens.Overlaps(["date", "day", "birthday"]) && tokens.Overlaps(["hebrew", "jewish", "gregorian", "today", "todays"]) && !tokens.Overlaps(["parashah", "parashat", "portion", "story", "mitzvah"]);
    }

    private static BinaryData Arguments(bool isToday, DateTime? date, bool afterSunset) => BinaryData.FromString(isToday
        ? JsonSerializer.Serialize(new { occurredAfterSunset = afterSunset })
        : JsonSerializer.Serialize(new { birthDateTime = date, occurredAfterSunset = afterSunset }));

    internal static GroundedAnswerResult NoRegularParashah(AIToolExecutionResult result, string? holiday)
    {
        var data = JsonSerializer.SerializeToElement(result.Data, JsonOptions);
        var date = data.GetProperty("shabbatDate").Deserialize<DateOnly>();
        var occasion = string.IsNullOrWhiteSpace(holiday) ? "A festival reading" : holiday;
        return Reply($"The short answer is: {occasion} replaces the regular weekly portion on Shabbat, {date:MMMM d, yyyy}. There is no regular weekly parashah for that Shabbat.\n\nDid you mean the holiday reading, or the weekly portion for a different date?", "Festival Torah reading", [("find_parashah_for_week", result)]);
    }

    private static GroundedAnswerResult Reply(string text, string title, IReadOnlyList<(string Name, AIToolExecutionResult Result)> calculations)
    {
        var evidence = calculations.Select((calculation, index) => AIToolExecutionSession.CreateEvidence($"E{index + 1}", calculation.Name, calculation.Result.Evidence ?? throw new InvalidOperationException("Calendar evidence is required."))).ToArray();
        var citations = evidence.Select((item, index) => new SourceCitation(index + 1, item.EvidenceId, item.Source.SegmentId, item.Source.Title, item.Source.HebrewTitle, item.Source.CanonicalReference, item.Source.Version, item.Source.Language, item.Source.LanguageCode, item.Source.Collection, item.Source.Categories, item.Source.License, item.Source.LicenseCategory, item.Source.SourceUrl, item.Source.FilePath, false)).ToArray();
        var claim = new GroundedClaim(text, citations, null, null)
        {
            Quotations = evidence.Select((item, index) => new GroundedQuotation(item.PresentedText, "Calendar date and sunset convention", citations[index])).ToArray(),
        };
        return new GroundedAnswerResult
        {
            Status = GroundedAnswerStatus.Success,
            Answer = new GroundedAnswer([claim], [], [], null, false, citations) { InterpretiveNotice = string.Empty, SuggestedConversationTitle = title },
            Evidence = new EvidencePacket(evidence, evidence.Sum(item => item.PresentedText.Length)),
            Trace = new GroundedAnswerTrace(TimeSpan.Zero, TimeSpan.Zero, 0, evidence.Length, evidence.Sum(item => item.PresentedText.Length), null, GroundedValidationStatus.NotRun, false, null, "deterministic"),
        };
    }
}
